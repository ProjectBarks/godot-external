using Godot.External.Reflection;

namespace Godot.External.Tests;

/// <summary>
/// The getter-code offset route, tested against <b>real bytes lifted from the shipped Godot Windows
/// x86-64 export templates</b> rather than hand-written encodings.
/// </summary>
/// <remarks>
/// <para>
/// Every <c>byte[]</c> below was read out of <c>windows_release_x86_64.exe</c> or
/// <c>windows_debug_x86_64.exe</c> under <c>%APPDATA%\Godot\export_templates\4.5.stable\</c> at the
/// stated <c>.text</c> RVA (image base <c>0x140000000</c>, <c>.text</c> at RVA <c>0x1000</c>). Hand-
/// written test encodings would test the decoder against my model of MSVC rather than against MSVC,
/// which is the whole risk being managed here.
/// </para>
/// <para>
/// The refusal tests carry the weight. docs/analysis.md §8.9 and <c>AbsentNeverWrongTests</c> set the
/// property: where it is unsure, it says nothing. A decoder that got 3/3 on the trivial cases and
/// invented an answer for <c>is_visible_in_tree</c> would be worse than useless, because the wrong
/// value is indistinguishable from the right ones at the call site.
/// </para>
/// </remarks>
public sealed class GetterFieldDecoderTests
{
    // ---- 4.5-stable release template -------------------------------------------------------

    /// <summary>
    /// <c>CanvasItem::is_visible</c> at RVA <c>0x139f520</c>. Identified <em>by name</em>: its single
    /// address-taken reference sits in the <c>_bind_methods</c> block that also materialises the
    /// <c>"is_visible"</c> method-name string, so this is not an offset that merely happens to match.
    /// <code>movzx eax, byte ptr [rcx + 0x370]; ret</code>
    /// </summary>
    private static readonly byte[] CanvasItemIsVisibleRelease =
        [0x0F, 0xB6, 0x81, 0x70, 0x03, 0x00, 0x00, 0xC3];

    /// <summary>
    /// The <c>String</c>-returning getter at RVA <c>0x1483bb0</c>, whose field is <c>+0x800</c> — the
    /// value docs/analysis.md §4.6 records for <c>Label.getText</c> on the release template.
    /// MSVC's hidden-return-pointer shape: RCX is the caller's result buffer, <c>this</c> is RDX.
    /// <code>push rbx; sub rsp,0x20; mov qword[rcx],0; mov rbx,rcx; mov rcx,[rdx+0x800]; …; ret</code>
    /// </summary>
    private static readonly byte[] StringGetter0x800Release =
    [
        0x53, 0x48, 0x83, 0xEC, 0x20, 0x48, 0xC7, 0x01, 0x00, 0x00, 0x00, 0x00, 0x48, 0x89, 0xCB,
        0x48, 0x8B, 0x8A, 0x00, 0x08, 0x00, 0x00, 0x48, 0x85, 0xC9, 0x74, 0x13, 0x48, 0x89, 0x0B,
        0xE8, 0xAD, 0xDE, 0x6B, 0x01, 0x84, 0xC0, 0x75, 0x07, 0x48, 0xC7, 0x03, 0x00, 0x00, 0x00,
        0x00, 0x48, 0x89, 0xD8, 0x48, 0x83, 0xC4, 0x20, 0x5B, 0xC3,
    ];

    /// <summary>
    /// The <c>String</c> getter at RVA <c>0x1663590</c> — <c>+0xa78</c>, §4.6's release
    /// <c>RichTextLabel.getText</c>. A second codegen shape: the refcount increment is inlined as a
    /// <c>lock cmpxchg</c> loop instead of a call, so the body never leaves the function, and it
    /// loads <c>[rdx+0xa78]</c> <b>twice</b>.
    /// </summary>
    private static readonly byte[] StringGetter0xa78Release =
    [
        0x48, 0x8B, 0x82, 0x78, 0x0A, 0x00, 0x00, 0x4C, 0x8D, 0x40, 0xF0, 0x48, 0xC7, 0x01, 0x00,
        0x00, 0x00, 0x00, 0x48, 0x85, 0xC0, 0x74, 0x23, 0x49, 0x8B, 0x00, 0x48, 0x85, 0xC0, 0x74,
        0x1B, 0x4C, 0x8D, 0x48, 0x01, 0xF0, 0x4D, 0x0F, 0xB1, 0x08, 0x75, 0xED, 0x48, 0x83, 0xF8,
        0xFF, 0x74, 0x0A, 0x48, 0x8B, 0x82, 0x78, 0x0A, 0x00, 0x00, 0x48, 0x89, 0x01, 0x48, 0x89,
        0xC8, 0xC3,
    ];

    /// <summary><c>movzx eax, byte ptr [rcx + 0x8d1]; ret</c> at RVA <c>0x145d210</c> — a bool getter.</summary>
    private static readonly byte[] BoolGetter0x8d1Release =
        [0x0F, 0xB6, 0x81, 0xD1, 0x08, 0x00, 0x00, 0xC3];

    /// <summary><c>mov eax, dword ptr [rcx + 0x800]; ret</c> at RVA <c>0x1755be0</c>.</summary>
    private static readonly byte[] Int32Getter0x800Release =
        [0x8B, 0x81, 0x00, 0x08, 0x00, 0x00, 0xC3];

    /// <summary><c>mov rax, qword ptr [rcx + 0x800]; ret</c> at RVA <c>0x17591a0</c>.</summary>
    private static readonly byte[] PointerGetter0x800Release =
        [0x48, 0x8B, 0x81, 0x00, 0x08, 0x00, 0x00, 0xC3];

    /// <summary><c>movss xmm0, dword ptr [rcx + 0x800]; ret</c> at RVA <c>0x1657f00</c>.</summary>
    private static readonly byte[] FloatGetter0x800Release =
        [0xF3, 0x0F, 0x10, 0x81, 0x00, 0x08, 0x00, 0x00, 0xC3];

    /// <summary>
    /// <c>CanvasItem::is_visible_in_tree</c> at RVA <c>0x139f500</c>: reads <c>visible</c> at
    /// <c>+0x370</c> and <c>parent_visible_in_tree</c> at <c>+0x371</c>. Two fields, one return —
    /// nothing here says which one the property is, so it must refuse.
    /// </summary>
    private static readonly byte[] IsVisibleInTreeRelease =
    [
        0x0F, 0xB6, 0x81, 0x70, 0x03, 0x00, 0x00, 0x84, 0xC0, 0x74, 0x07, 0x0F, 0xB6, 0x81, 0x71,
        0x03, 0x00, 0x00, 0xC3,
    ];

    /// <summary>
    /// A real multi-hundred-byte method at RVA <c>0x1780bb0</c> (a <c>set_visible</c>-shaped body).
    /// It touches <c>+0x131</c>, <c>+0x178</c> and <c>+0x918</c> and reaches no <c>ret</c> inside the
    /// window. Note <c>mov rcx, [rcx+0x178]</c>: after that, <c>+0x918</c> is an offset into a
    /// <em>different</em> object.
    /// </summary>
    private static readonly byte[] LargeMethodBodyRelease =
    [
        0x55, 0x57, 0x56, 0x53, 0x48, 0x83, 0xEC, 0x58, 0x48, 0x89, 0xCB, 0x89, 0xD6, 0x38, 0x91,
        0x31, 0x01, 0x00, 0x00, 0x0F, 0x84, 0xFE, 0x00, 0x00, 0x00, 0x88, 0x91, 0x31, 0x01, 0x00,
        0x00, 0x48, 0x8B, 0x89, 0x78, 0x01, 0x00, 0x00, 0x48, 0x85, 0xC9, 0x0F, 0x84, 0x9B, 0x00,
        0x00, 0x00, 0x84, 0xD2, 0x75, 0x6D, 0x48, 0x8B, 0x81, 0x18, 0x09, 0x00, 0x00, 0x48, 0x85,
        0xC0, 0x74, 0x61, 0x31,
    ];

    // ---- 4.5-stable debug template ----------------------------------------------------------

    /// <summary>
    /// RVA <c>0x11bc100</c> in the <b>debug</b> template: byte-for-byte the same shape as
    /// <see cref="StringGetter0x800Release"/> with the displacement changed to <c>+0x808</c>. It is
    /// the only <c>String</c> getter in that template anywhere near §4.6's claimed debug
    /// <c>Label.getText</c> value of <c>0x848</c> — see <see cref="OffsetCrossCheckTests"/>.
    /// </summary>
    private static readonly byte[] StringGetter0x808Debug =
    [
        0x53, 0x48, 0x83, 0xEC, 0x20, 0x48, 0xC7, 0x01, 0x00, 0x00, 0x00, 0x00, 0x48, 0x89, 0xCB,
        0x48, 0x8B, 0x8A, 0x08, 0x08, 0x00, 0x00, 0x48, 0x85, 0xC9, 0x74, 0x13, 0x48, 0x89, 0x0B,
        0xE8, 0xFD, 0xE3, 0x76, 0x01, 0x84, 0xC0, 0x75, 0x07, 0x48, 0xC7, 0x03, 0x00, 0x00, 0x00,
        0x00, 0x48, 0x89, 0xD8, 0x48, 0x83, 0xC4, 0x20, 0x5B, 0xC3,
    ];

    /// <summary>
    /// RVA <c>0x13a62d0</c> in the debug template: a third <c>String</c> shape that <em>tests</em> the
    /// field for null and then delegates the copy to a helper, forming the field address with
    /// <c>add rdx, 0xa80</c>. The only this-relative memory operand is the <c>cmp</c>, so the offset
    /// is still recoverable — as a <see cref="GetterShape.HelperCall"/>, not a leaf.
    /// </summary>
    private static readonly byte[] DelegatingStringGetter0xa80Debug =
    [
        0x53, 0x48, 0x83, 0xEC, 0x20, 0x48, 0x83, 0xBA, 0x80, 0x0A, 0x00, 0x00, 0x00, 0x48, 0xC7,
        0x01, 0x00, 0x00, 0x00, 0x00, 0x48, 0x89, 0xCB, 0x74, 0x0C, 0x48, 0x81, 0xC2, 0x80, 0x0A,
        0x00, 0x00, 0xE8, 0xDB, 0xFE, 0xFF, 0xFF, 0x48, 0x89, 0xD8, 0x48, 0x83, 0xC4, 0x20, 0x5B,
        0xC3,
    ];

    // ---- the 3/3 --------------------------------------------------------------------------

    [Fact]
    public void TheThreeValidatedGettersDecodeToTheirKnownOffsets()
    {
        // The claim the whole module rests on, stated as one test so it cannot pass partially.
        Assert.Equal(0x370, GetterFieldDecoder.Decode(CanvasItemIsVisibleRelease).Offset);
        Assert.Equal(0x800, GetterFieldDecoder.Decode(StringGetter0x800Release).Offset);
        Assert.Equal(0xa78, GetterFieldDecoder.Decode(StringGetter0xa78Release).Offset);
    }

    [Fact]
    public void AByValueGetterReportsRcxAndItsAccessWidth()
    {
        FieldOffsetDecodeResult result = GetterFieldDecoder.Decode(CanvasItemIsVisibleRelease);

        Assert.True(result.IsDecoded);
        Assert.Equal(GetterShape.LeafLoad, result.Shape);
        Assert.False(result.UsedHiddenReturnPointer);
        Assert.Equal(1, result.AccessSize);
        Assert.False(result.IsFloatingPointAccess);
    }

    [Fact]
    public void AStringGetterReportsTheHiddenReturnPointerShape()
    {
        FieldOffsetDecodeResult result = GetterFieldDecoder.Decode(StringGetter0x800Release);

        // RCX holds the caller's result buffer here, so `this` is RDX. A caller asking for a String
        // property and getting UsedHiddenReturnPointer == false followed the wrong MethodBind, no
        // matter how plausible the offset looks.
        Assert.True(result.UsedHiddenReturnPointer);
        Assert.Equal(8, result.AccessSize);

        // It calls CowData::_ref before returning, so the evidence is an inference, not a closed
        // proof — and the result says so rather than flattening the distinction.
        Assert.Equal(GetterShape.HelperCall, result.Shape);
    }

    [Fact]
    public void RepeatedLoadsOfTheSameFieldAreOneCandidate()
    {
        // 0x1663590 loads [rdx+0xa78] twice. A literal "exactly one this-relative memory operand"
        // rule refuses this real, correct getter; the rule that works is exactly one distinct
        // (register, displacement) pair.
        FieldOffsetDecodeResult result = GetterFieldDecoder.Decode(StringGetter0xa78Release);

        Assert.Equal(0xa78, result.Offset);
        Assert.Equal(GetterShape.LeafLoad, result.Shape);
        Assert.True(result.UsedHiddenReturnPointer);
    }

    // ---- access widths --------------------------------------------------------------------

    [Theory]
    [InlineData(1, false)]
    [InlineData(4, false)]
    [InlineData(8, false)]
    [InlineData(4, true)]
    public void EachScalarCodegenShapeIsDecodedWithItsWidth(int expectedSize, bool expectedFloat)
    {
        byte[] body = (expectedSize, expectedFloat) switch
        {
            (1, false) => BoolGetter0x8d1Release,
            (4, false) => Int32Getter0x800Release,
            (8, false) => PointerGetter0x800Release,
            _ => FloatGetter0x800Release,
        };

        FieldOffsetDecodeResult result = GetterFieldDecoder.Decode(body);

        Assert.True(result.IsDecoded, result.Reason);
        Assert.Equal(expectedSize, result.AccessSize);
        Assert.Equal(expectedFloat, result.IsFloatingPointAccess);
        Assert.False(result.UsedHiddenReturnPointer);
    }

    [Fact]
    public void TheBoolGetterAtAnUnrelatedOffsetStillDecodes()
        => Assert.Equal(0x8d1, GetterFieldDecoder.Decode(BoolGetter0x8d1Release).Offset);

    // ---- refusals -------------------------------------------------------------------------

    [Fact]
    public void TwoFieldsInOneBodyRefuse()
    {
        FieldOffsetDecodeResult result = GetterFieldDecoder.Decode(IsVisibleInTreeRelease);

        Assert.Equal(FieldOffsetDecodeStatus.AmbiguousAccesses, result.Status);
        Assert.Null(result.Offset);

        // 0x370 IS the right answer for CanvasItem::visible, and this body contains it — but this
        // body is is_visible_in_tree, and taking the first load would be guessing that the first
        // field read is the one returned. Here it happens to be; in `return a ? b : c` it is not.
        Assert.Contains("0x370", result.Reason, StringComparison.Ordinal);
        Assert.Contains("0x371", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ARealMethodBodyRefusesForWantOfAReturn()
    {
        FieldOffsetDecodeResult result = GetterFieldDecoder.Decode(LargeMethodBodyRelease);

        Assert.Equal(FieldOffsetDecodeStatus.NoReturnInWindow, result.Status);
        Assert.Null(result.Offset);
    }

    [Fact]
    public void AClobberedThisRegisterStopsBeingThis()
    {
        // LargeMethodBodyRelease does `mov rcx, [rcx+0x178]` and then reads [rcx+0x918]. Without
        // liveness tracking that 0x918 is collected as a field of the object, which is false. The
        // ret gate would refuse this body anyway, so assert the mechanism directly with a window
        // wide enough to reach the reload but a body that DOES return.
        byte[] reloadThenRead =
        [
            0x48, 0x8B, 0x89, 0x78, 0x01, 0x00, 0x00, // mov rcx, [rcx+0x178]
            0x48, 0x8B, 0x81, 0x18, 0x09, 0x00, 0x00, // mov rax, [rcx+0x918]   <- NOT a field of this
            0xC3,                                     // ret
        ];

        FieldOffsetDecodeResult result = GetterFieldDecoder.Decode(reloadThenRead);

        // Only +0x178 survives as this-relative, so the body reads exactly one field of `this` and
        // decodes to it. The failure this guards against is reporting 0x918.
        Assert.Equal(0x178, result.Offset);
        Assert.DoesNotContain("0x918", result.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void ABodyBelowTheDisplacementFloorRefuses()
    {
        // mov rax, [rcx+0x8]; ret — header territory. The floor exists because +0x0 and +0x8 are
        // the vtable and the CowData refcount word, which every object has and no property is.
        byte[] headerRead = [0x48, 0x8B, 0x41, 0x08, 0xC3, 0x90, 0x90, 0x90];

        FieldOffsetDecodeResult result = GetterFieldDecoder.Decode(headerRead);

        Assert.Equal(FieldOffsetDecodeStatus.DisplacementOutOfRange, result.Status);
        Assert.Null(result.Offset);
    }

    [Fact]
    public void AGetterReturningAConstantRefuses()
    {
        // xor eax, eax; ret — a great many `return false;` overrides look exactly like this.
        byte[] constant = [0x31, 0xC0, 0xC3, 0x90, 0x90, 0x90, 0x90, 0x90];

        FieldOffsetDecodeResult result = GetterFieldDecoder.Decode(constant);

        Assert.Equal(FieldOffsetDecodeStatus.NoThisRelativeAccess, result.Status);
    }

    [Fact]
    public void ARipRelativeGlobalIsNotAField()
    {
        // mov eax, [rip+0x11223344]; ret — a static/global, not a member. The base register filter
        // catches it, but the case is worth pinning: engine singletons read exactly like this.
        byte[] globalRead = [0x8B, 0x05, 0x44, 0x33, 0x22, 0x11, 0xC3, 0x90];

        Assert.Equal(FieldOffsetDecodeStatus.NoThisRelativeAccess, GetterFieldDecoder.Decode(globalRead).Status);
    }

    [Fact]
    public void AnIndexedAccessIsNotAField()
    {
        // mov rax, [rcx+rdx*8+0x100]; ret — an element of something, not a member.
        byte[] indexed = [0x48, 0x8B, 0x84, 0xD1, 0x00, 0x01, 0x00, 0x00, 0xC3];

        Assert.Equal(FieldOffsetDecodeStatus.NoThisRelativeAccess, GetterFieldDecoder.Decode(indexed).Status);
    }

    [Fact]
    public void GarbageBytesAreRefusedAsUndecodable()
    {
        // The realistic version of this is a MethodBind probe that picked the wrong qword and handed
        // over a data address. It must not decode to anything.
        byte[] notCode = [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF];

        FieldOffsetDecodeResult result = GetterFieldDecoder.Decode(notCode);

        Assert.False(result.IsDecoded);
        Assert.Null(result.Offset);
    }

    [Fact]
    public void AnEmptyOrTruncatedBodyRefuses()
    {
        Assert.Equal(FieldOffsetDecodeStatus.EmptyBody, GetterFieldDecoder.Decode([]).Status);
        Assert.Equal(FieldOffsetDecodeStatus.EmptyBody, GetterFieldDecoder.Decode([0x0F, 0xB6, 0x81]).Status);

        // A body cut off mid-instruction is "we did not read enough", not "these are not code" —
        // the same distinction a short read from a live process needs. Either way: no offset.
        FieldOffsetDecodeResult truncated = GetterFieldDecoder.Decode([0x0F, 0xB6, 0x81, 0x70]);

        Assert.Equal(FieldOffsetDecodeStatus.NoReturnInWindow, truncated.Status);
        Assert.Null(truncated.Offset);
    }

    [Fact]
    public void ANonWindowsAbiIsRefusedRatherThanMisparsed()
    {
        // Linux templates do not define TYPED_METHOD_BIND: the pointer-to-member is an Itanium
        // {ptr, adjustment} pair and `this` is RDI. Decoding those bytes under MSVC rules would
        // read a real function as a different real function.
        GetterDecoderOptions sysV = new() { CallingConvention = NativeCallingConvention.SystemVX64 };

        FieldOffsetDecodeResult result = GetterFieldDecoder.Decode(CanvasItemIsVisibleRelease, sysV);

        Assert.Equal(FieldOffsetDecodeStatus.UnsupportedCallingConvention, result.Status);
        Assert.Null(result.Offset);
    }

    // ---- options -------------------------------------------------------------------------

    [Fact]
    public void RequireLeafBodyRejectsEveryStringGetterAndKeepsScalars()
    {
        GetterDecoderOptions strict = new() { RequireLeafBody = true };

        Assert.Equal(0x370, GetterFieldDecoder.Decode(CanvasItemIsVisibleRelease, strict).Offset);
        Assert.Equal(
            FieldOffsetDecodeStatus.LeafBodyRequired,
            GetterFieldDecoder.Decode(StringGetter0x800Release, strict).Status);
    }

    [Fact]
    public void ADelegatingGetterYieldsItsOffsetButOnlyAsAnInference()
    {
        FieldOffsetDecodeResult result = GetterFieldDecoder.Decode(DelegatingStringGetter0xa80Debug);

        Assert.Equal(0xa80, result.Offset);
        Assert.Equal(GetterShape.HelperCall, result.Shape);
        Assert.True(result.UsedHiddenReturnPointer);
    }

    [Fact]
    public void ShrinkingTheWindowTurnsALongGetterIntoARefusal()
    {
        // Not a bug — a smaller window is a stricter claim, and the failure direction is refusal.
        GetterDecoderOptions tiny = new() { WindowBytes = 0x10 };

        Assert.Equal(
            FieldOffsetDecodeStatus.NoReturnInWindow,
            GetterFieldDecoder.Decode(StringGetter0x800Release, tiny).Status);
        Assert.Equal(0x370, GetterFieldDecoder.Decode(CanvasItemIsVisibleRelease, tiny).Offset);
    }

    [Fact]
    public void TrailingPaddingAfterTheReturnIsIgnored()
    {
        // Callers read a fixed-size window, so a short getter always arrives with the next
        // function's alignment padding attached.
        byte[] padded =
        [
            0x0F, 0xB6, 0x81, 0x70, 0x03, 0x00, 0x00, 0xC3,
            0x0F, 0x1F, 0x84, 0x00, 0x00, 0x00, 0x00, 0x00,
            0xCC, 0xCC, 0xCC, 0xCC,
        ];

        Assert.Equal(0x370, GetterFieldDecoder.Decode(padded).Offset);
    }

    [Fact]
    public void TheDebugTemplateTwinDecodesToItsOwnOffset()
    {
        FieldOffsetDecodeResult result = GetterFieldDecoder.Decode(StringGetter0x808Debug);

        Assert.Equal(0x808, result.Offset);
        Assert.True(result.UsedHiddenReturnPointer);
    }
}
