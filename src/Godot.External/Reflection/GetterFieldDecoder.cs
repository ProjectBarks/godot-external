using System.Globalization;
using Iced.Intel;

namespace Godot.External.Reflection;

/// <summary>
/// Recovers a Godot field offset from the machine code of the property's <b>getter</b>, or refuses.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> Godot's <c>ClassDB</c> stores no field offsets — a
/// <c>ClassDB::ClassInfo::property_setget</c> entry holds only the setter and getter
/// <c>MethodBind*</c>. On Windows that is enough, because <c>platform/windows/detect.py</c> defines
/// <c>TYPED_METHOD_BIND</c>, so <c>MethodBindTRC&lt;T, R, P...&gt;</c> stores a pointer-to-member
/// typed on the real class; Godot's <c>Object</c> hierarchy is single-inheritance with no virtual
/// bases, so MSVC represents that as a plain 8-byte code address. The offset is not in the data — it
/// is in the code, and this class reads it out.
/// </para>
/// <para>
/// <b>Why the code survives optimisation.</b> The getter's address is taken and stored in the
/// <c>MethodBind</c>, so the out-of-line body must be emitted even when every call site is inlined.
/// <c>/OPT:ICF</c> folding is harmless: two getters only fold together if they are byte-identical,
/// which for a field load means they share the offset.
/// </para>
/// <para>
/// <b>What it refuses.</b> Classifying all 3,951 <c>ADD_PROPERTY</c> sites in the 4.5 source puts
/// ~84.5% at <c>return field;</c> / <c>return (T)field;</c> / <c>return field.sub;</c>, 11.2%
/// computed or multi-statement, and 4.4% delegating. The back 15.6% must produce no offset, and
/// <see cref="FieldOffsetDecodeStatus"/> is where that happens. See
/// <c>AbsentNeverWrongTests</c>: a missing offset costs one check, a confidently wrong one costs the
/// credibility of the whole table.
/// </para>
/// <para>
/// <b>This decodes; it does not attribute.</b> Given bytes, it says which field they touch. It
/// cannot say which class or property those bytes belong to — that is what the caller's
/// <c>MethodBind</c> supplies. The distinction is not academic: searching the 4.5 <em>debug</em>
/// template for the one function matching <c>movzx eax, byte [rcx+0x3c0]; ret</c> finds exactly one
/// hit, and following its single address-taken reference shows it is bound as
/// <c>get_debug_use_custom</c> on an unrelated class — not <c>CanvasItem::visible</c>, which is what
/// a match on that displacement would suggest. An offset without a name attached is not evidence.
/// </para>
/// <para>
/// <b>Windows/MSVC only</b>, by construction — see <see cref="NativeCallingConvention"/>.
/// </para>
/// </remarks>
public static class GetterFieldDecoder
{
    /// <summary>
    /// The shortest getter that could possibly be proved: <c>mov eax, dword ptr [rcx+disp8]</c>
    /// (3 bytes) plus <c>ret</c>. Anything shorter cannot hold both an accepted displacement and a
    /// return, so it is refused without decoding.
    /// </summary>
    /// <remarks>
    /// A <c>disp8</c> form is reachable because the displacement floor is <c>0x20</c> and
    /// <c>disp8</c> encodes up to <c>0x7f</c> — several real Godot fields land in that band.
    /// </remarks>
    public const int MinimumBodyBytes = 4;

    /// <summary>
    /// Decodes a getter body into a field offset, or a named refusal.
    /// </summary>
    /// <param name="body">
    /// Bytes read from the getter's entry point. May be longer than
    /// <see cref="GetterDecoderOptions.WindowBytes"/>; the excess is ignored.
    /// </param>
    /// <param name="options">Bounds and refusal criteria; <see cref="GetterDecoderOptions.Default"/> when omitted.</param>
    /// <param name="entryPoint">
    /// The address the bytes were read from. Purely cosmetic — it makes the decoded listing in
    /// <see cref="FieldOffsetDecodeResult.Reason"/> line up with a disassembler. Decoding does not
    /// depend on it, because no accepted instruction form is RIP-relative.
    /// </param>
    /// <returns>Never <see langword="null"/>. Check <see cref="FieldOffsetDecodeResult.IsDecoded"/>.</returns>
    public static FieldOffsetDecodeResult Decode(
        ReadOnlySpan<byte> body,
        GetterDecoderOptions? options = null,
        ulong entryPoint = 0)
    {
        GetterDecoderOptions opts = options ?? GetterDecoderOptions.Default;

        if (opts.CallingConvention != NativeCallingConvention.MsvcX64)
        {
            return FieldOffsetDecodeResult.Refuse(
                FieldOffsetDecodeStatus.UnsupportedCallingConvention,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} is not supported: the getter-code route depends on TYPED_METHOD_BIND, which " +
                    "Godot defines only on Windows, and on MSVC's RCX/RDX this-pointer placement",
                    opts.CallingConvention));
        }

        if (body.Length < MinimumBodyBytes)
        {
            return FieldOffsetDecodeResult.Refuse(
                FieldOffsetDecodeStatus.EmptyBody,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "body is {0} byte(s); the shortest provable getter is {1}",
                    body.Length,
                    MinimumBodyBytes));
        }

        int windowLength = Math.Min(body.Length, opts.WindowBytes);
        byte[] window = body[..windowLength].ToArray();

        return DecodeWindow(window, opts, entryPoint);
    }

    private static FieldOffsetDecodeResult DecodeWindow(byte[] window, GetterDecoderOptions opts, ulong entryPoint)
    {
        Decoder decoder = Decoder.Create(64, new ByteArrayCodeReader(window), entryPoint);
        InstructionInfoFactory infoFactory = new();

        // The this-pointer register is only this until something overwrites it. Godot's own
        // String getters do exactly that -- the 4.5 release get_text loads the field into RCX,
        // after which [rcx+D] addresses the String, not the object. Without this, a body that
        // reloads through a clobbered register can hand back a displacement into an unrelated
        // structure, which is the one failure this module must not have.
        bool rcxIsThis = true;
        bool rdxIsThis = true;

        HashSet<(Register Base, long Displacement)> distinct = [];
        (Register Base, long Displacement, int Size, bool Float)? firstAccess = null;
        long? rejectedDisplacement = null;

        bool collecting = true;
        bool sawReturn = false;
        bool leftFunction = false;
        bool hitBadBytes = false;
        int decodedBytes = 0;

        while (decodedBytes < window.Length)
        {
            decoder.Decode(out Instruction instruction);

            if (instruction.IsInvalid)
            {
                // Running out of window mid-instruction is not the same finding as hitting bytes
                // that are not code: the first says "the body is longer than we looked", the second
                // says "this address is not a function". Conflating them would report every long
                // method as a bad pointer.
                hitBadBytes = decoder.LastError != DecoderError.NoMoreBytes;
                break;
            }

            decodedBytes += instruction.Length;

            if (collecting)
            {
                CollectOperands(
                    instruction,
                    opts,
                    rcxIsThis,
                    rdxIsThis,
                    distinct,
                    ref firstAccess,
                    ref rejectedDisplacement);

                // Ordered after the read on purpose: within one instruction the memory operand is
                // read using the OLD register value, so `mov rcx, [rdx+0x800]` still contributes
                // rdx+0x800 and only then retires RCX.
                RetireClobberedRegisters(infoFactory, instruction, ref rcxIsThis, ref rdxIsThis);
            }

            switch (instruction.FlowControl)
            {
                case FlowControl.Return:
                    sawReturn = true;
                    collecting = false;
                    break;

                case FlowControl.Call:
                case FlowControl.IndirectCall:
                case FlowControl.UnconditionalBranch:
                case FlowControl.IndirectBranch:
                case FlowControl.Interrupt:
                case FlowControl.Exception:
                    if (collecting)
                    {
                        leftFunction = true;
                        collecting = false;
                    }

                    break;

                default:
                    // Next and ConditionalBranch: stay in the straight-line region. Reading operands
                    // from a not-taken path can only add candidates, and extra candidates refuse.
                    break;
            }

            if (sawReturn)
            {
                break;
            }
        }

        // The ret gate comes first on purpose. Without a return in the window there is no evidence
        // that any load reaches the caller, so the offset would be a guess about a function the
        // decoder never saw the end of -- however unambiguous the loads inside it looked.
        if (!sawReturn)
        {
            return hitBadBytes
                ? FieldOffsetDecodeResult.Refuse(
                    FieldOffsetDecodeStatus.UndecodableBody,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "stopped decoding after {0} of {1} byte(s) with no ret; these bytes are not a " +
                        "function entry point",
                        decodedBytes,
                        window.Length))
                : FieldOffsetDecodeResult.Refuse(
                    FieldOffsetDecodeStatus.NoReturnInWindow,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "no ret within {0} byte(s); this is a real function body, not a field getter",
                        window.Length));
        }

        return Publish(distinct, firstAccess, rejectedDisplacement, leftFunction, opts);
    }

    private static void RetireClobberedRegisters(
        InstructionInfoFactory factory,
        in Instruction instruction,
        ref bool rcxIsThis,
        ref bool rdxIsThis)
    {
        InstructionInfo info = factory.GetInfo(instruction);

        foreach (UsedRegister used in info.GetUsedRegisters())
        {
            if (used.Access is not (OpAccess.Write or OpAccess.ReadWrite or OpAccess.CondWrite
                or OpAccess.ReadCondWrite))
            {
                continue;
            }

            // Writing ECX/CX/CL all land in RCX, so normalise before comparing.
            switch (used.Register.GetFullRegister())
            {
                case Register.RCX:
                    rcxIsThis = false;
                    break;

                case Register.RDX:
                    rdxIsThis = false;
                    break;

                default:
                    break;
            }
        }
    }

    private static void CollectOperands(
        in Instruction instruction,
        GetterDecoderOptions opts,
        bool rcxIsThis,
        bool rdxIsThis,
        HashSet<(Register Base, long Displacement)> distinct,
        ref (Register Base, long Displacement, int Size, bool Float)? firstAccess,
        ref long? rejectedDisplacement)
    {
        for (int i = 0; i < instruction.OpCount; i++)
        {
            if (instruction.GetOpKind(i) != OpKind.Memory)
            {
                continue;
            }

            Register baseRegister = instruction.MemoryBase;
            if (baseRegister is not (Register.RCX or Register.RDX))
            {
                continue;
            }

            if (baseRegister == Register.RCX ? !rcxIsThis : !rdxIsThis)
            {
                continue;
            }

            // An index register means an element of something, not a member of this.
            if (instruction.MemoryIndex != Register.None)
            {
                continue;
            }

            long displacement = (long)instruction.MemoryDisplacement64;

            if (displacement < opts.MinimumDisplacement || displacement > opts.MaximumDisplacement)
            {
                // +0 is MSVC zeroing the caller's hidden return buffer at the top of every String
                // getter; it is structural, not a rejected field, so it is not worth reporting.
                if (displacement != 0)
                {
                    rejectedDisplacement ??= displacement;
                }

                continue;
            }

            distinct.Add((baseRegister, displacement));

            if (firstAccess is null && instruction.Mnemonic != Mnemonic.Lea)
            {
                int size = instruction.MemorySize.GetSize();
                if (size > 0)
                {
                    firstAccess = (baseRegister, displacement, size, IsFloatingPoint(instruction.MemorySize));
                }
            }
        }
    }

    private static FieldOffsetDecodeResult Publish(
        HashSet<(Register Base, long Displacement)> distinct,
        (Register Base, long Displacement, int Size, bool Float)? firstAccess,
        long? rejectedDisplacement,
        bool leftFunction,
        GetterDecoderOptions opts)
    {
        if (distinct.Count == 0)
        {
            return rejectedDisplacement is { } rejected
                ? FieldOffsetDecodeResult.Refuse(
                    FieldOffsetDecodeStatus.DisplacementOutOfRange,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "only this-relative access was +0x{0:x}, outside the accepted band " +
                        "[0x{1:x}, 0x{2:x}]",
                        rejected,
                        opts.MinimumDisplacement,
                        opts.MaximumDisplacement))
                : FieldOffsetDecodeResult.Refuse(
                    FieldOffsetDecodeStatus.NoThisRelativeAccess,
                    "no rcx/rdx-relative field access before the first call or return; the getter is " +
                    "computed, delegating, or returns a constant");
        }

        if (distinct.Count > 1)
        {
            IEnumerable<string> rendered = distinct
                .OrderBy(d => d.Displacement)
                .Select(d => string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}+0x{1:x}",
                    d.Base == Register.RCX ? "rcx" : "rdx",
                    d.Displacement));

            return FieldOffsetDecodeResult.Refuse(
                FieldOffsetDecodeStatus.AmbiguousAccesses,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "{0} distinct this-relative fields touched ({1}); none can be attributed to the property",
                    distinct.Count,
                    string.Join(", ", rendered)));
        }

        if (firstAccess is not { } access)
        {
            return FieldOffsetDecodeResult.Refuse(
                FieldOffsetDecodeStatus.NoThisRelativeAccess,
                "the only this-relative reference formed an address (lea) without loading it, so the " +
                "displacement is not provably the returned field");
        }

        if (leftFunction && opts.RequireLeafBody)
        {
            return FieldOffsetDecodeResult.Refuse(
                FieldOffsetDecodeStatus.LeafBodyRequired,
                string.Format(
                    CultureInfo.InvariantCulture,
                    "body touches exactly +0x{0:x} but calls out before returning, and RequireLeafBody is set",
                    access.Displacement));
        }

        return FieldOffsetDecodeResult.Success(
            checked((int)access.Displacement),
            leftFunction ? GetterShape.HelperCall : GetterShape.LeafLoad,
            access.Size,
            access.Float,
            access.Base == Register.RDX);
    }

    private static bool IsFloatingPoint(MemorySize size) => size is
        MemorySize.Float16 or
        MemorySize.Float32 or
        MemorySize.Float64 or
        MemorySize.Float80;
}
