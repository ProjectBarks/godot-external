using System.Globalization;
using Godot.External.Abi;
using Godot.External.Reflection;

namespace Godot.External.Calibrator.Reflection;

/// <summary>
/// Resolves a <c>MethodBind*</c> inside a <c>ClassInfo</c> <b>by name</b>, and then proves the answer
/// belongs to the class it was asked about.
/// </summary>
/// <remarks>
/// <para>
/// <b>The map is found structurally, not at a hardcoded offset.</b> Every qword of the
/// <c>ClassInfo</c> is tested for the full <c>HashMap</c> shape — a <c>head_element</c> whose
/// <c>prev</c> is null, a <c>tail_element</c> whose <c>next</c> is null and which the chain actually
/// ends on, and a <c>num_elements</c> word that <em>equals</em> the walked length. The weaker version
/// of this test ("a pointer to something whose <c>prev</c> is null") accepts a bucket array, which is
/// how a 39-element theme map once passed for <c>method_map</c>.
/// </para>
/// <para>
/// <b>And the bind is then re-verified by value.</b> A scan window over one <c>ClassInfo</c> reaches
/// its neighbours in the pool, so a name match inside the window is a statement about the window and
/// not about the object: before this check existed, <c>Control</c>'s window happily yielded
/// <c>Label</c>'s <c>get_text</c> bind. <see cref="TryResolve"/> therefore reads the candidate's own
/// <c>StringName instance_class</c> and <c>StringName name</c> members back out and requires both to
/// match, which is what makes the resulting <see cref="GetterAttribution"/> a claim about the engine.
/// </para>
/// <para>
/// <c>ClassDbLayout.ClassInfoMethodMap</c> is deliberately <em>not</em> used as the address to read.
/// It is a measured constant per version and it is right, but reading a fixed offset would make the
/// lookup depend on a number this route does not otherwise need — and the structural search is what
/// discovered that the number moved at 4.6 in the first place.
/// </para>
/// </remarks>
internal sealed class MethodBindResolver
{
    /// <summary>How far into a <c>ClassInfo</c> to look for intrusive map heads.</summary>
    /// <remarks>
    /// A 4.5 <c>ClassInfo</c>'s last validated map head sits at <c>+0x150</c>; the window is far wider
    /// so that a version which grows members still finds <c>method_map</c>. Its width is safe only
    /// <em>because</em> of the by-value bind verification: without that, a wide window is exactly how
    /// a neighbouring <c>ClassInfo</c>'s bind gets attributed to this class.
    /// </remarks>
    public const int ClassInfoScanBytes = 0x800;

    /// <summary>Qwords of a <c>MethodBind</c> searched for its own name <c>StringName</c>s.</summary>
    public const int BindNameSlots = 16;

    private const int MaxMapElements = 8192;

    private readonly IByteSource _source;
    private readonly ClassDbLayout _layout;

    /// <summary>Creates a resolver over one target.</summary>
    public MethodBindResolver(IByteSource source, ClassDbLayout layout)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(layout);

        _source = source;
        _layout = layout;
    }

    /// <summary>
    /// Finds <paramref name="className"/>'s own <paramref name="method"/> bind inside
    /// <paramref name="classInfo"/>.
    /// </summary>
    /// <param name="classInfo">Address of the <c>ClassInfo</c> value of a <c>ClassDB::classes</c> entry.</param>
    /// <param name="className">Class the bind must name as its <c>instance_class</c>.</param>
    /// <param name="method">Method the bind must name as its <c>name</c>.</param>
    /// <param name="names">Reader for the interned <c>StringName</c>s, with a measured layout.</param>
    /// <param name="bind">The verified <c>MethodBind*</c>; zero on any refusal.</param>
    /// <param name="evidence">How it was verified, or why it was refused.</param>
    /// <returns>
    /// <see langword="false"/> when the class does not bind that method <em>itself</em> — which is the
    /// correct answer for an inherited method, and is why <c>Control::get_text</c> resolves to nothing
    /// even though a <c>Control</c> instance answers it.
    /// </returns>
    public bool TryResolve(
        ulong classInfo,
        string className,
        string method,
        InternedNameReader names,
        out ulong bind,
        out string evidence)
    {
        ArgumentNullException.ThrowIfNull(names);

        bind = 0;
        int chainsSeen = 0;

        for (int offset = 0; offset < ClassInfoScanBytes; offset += ByteSourceExtensions.PointerWidth)
        {
            if (!TryReadMapChain(classInfo + (ulong)offset, out IReadOnlyList<ulong> chain, out int countAt))
            {
                continue;
            }

            int named = 0;
            ulong hit = 0;
            foreach (ulong element in chain)
            {
                if (ClassDbElementWalk.TryReadKeyPointer(_source, element, _layout, out ulong key)
                    && names.TryRead(key, out string name)
                    && name.Length > 0)
                {
                    named++;
                    if (string.Equals(name, method, StringComparison.Ordinal))
                    {
                        hit = element;
                    }
                }
            }

            // A map whose keys mostly do not read as names is not StringName-keyed, so a "match" in it
            // would be a coincidence between two unrelated qwords.
            if (named * 2 < chain.Count)
            {
                continue;
            }

            chainsSeen++;

            if (hit == 0)
            {
                continue;
            }

            ulong valueSlot = hit + (ulong)_layout.ElementData + ByteSourceExtensions.PointerWidth;
            if (!_source.TryReadPointer(valueSlot, out ulong candidate) || candidate == 0)
            {
                continue;
            }

            if (!VerifyBindNames(candidate, className, method, names, out string verified))
            {
                continue;
            }

            bind = candidate;
            evidence = string.Format(
                CultureInfo.InvariantCulture,
                "{0}; found in the map at ClassInfo+0x{1:x} ({2} entries, num_elements at head+0x{3:x})",
                verified,
                offset,
                chain.Count,
                countAt);
            return true;
        }

        evidence = string.Format(
            CultureInfo.InvariantCulture,
            "no MethodBind named \"{0}\" in {1}'s own maps ({2} StringName-keyed chain(s) searched)",
            method,
            className,
            chainsSeen);
        return false;
    }

    /// <summary>
    /// Accepts <paramref name="mapAddress"/> as a <c>HashMap</c> only when its head, its tail and its
    /// element count all agree with a fully traversed chain.
    /// </summary>
    private bool TryReadMapChain(ulong mapAddress, out IReadOnlyList<ulong> chain, out int countAt)
    {
        chain = [];
        countAt = -1;

        if (!_source.TryReadPointer(mapAddress, out ulong head) || head == 0 || (head & 7) != 0)
        {
            return false;
        }

        if (!_source.TryReadPointer(head + (ulong)_layout.ElementPrevious, out ulong previous) || previous != 0)
        {
            return false;
        }

        if (!_source.TryReadPointer(mapAddress + (ulong)ByteSourceExtensions.PointerWidth, out ulong tail)
            || tail == 0
            || (tail & 7) != 0
            || !_source.TryReadPointer(tail + (ulong)_layout.ElementNext, out ulong tailNext)
            || tailNext != 0)
        {
            return false;
        }

        if (!ClassDbElementWalk.TryEnumerate(_source, head, _layout, out IReadOnlyList<ulong> walked, out _, MaxMapElements)
            || walked.Count == 0
            || walked[^1] != tail)
        {
            return false;
        }

        // num_elements sits a fixed distance past head_element on every version (tail at +8,
        // capacity_index at +0x10, num_elements at +0x14), but it is CONFIRMED here rather than read:
        // a word that equals the walked length is the whole reason this address is a HashMap.
        for (int at = 16; at <= 40; at += 4)
        {
            if (_source.TryReadUInt32(mapAddress + (ulong)at, out uint count) && count == (uint)walked.Count)
            {
                countAt = at;
                chain = walked;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Reads the candidate bind's own <c>instance_class</c> and <c>name</c> back out and requires both
    /// to match. This is what attaches a name to the offset the decoder later publishes.
    /// </summary>
    private bool VerifyBindNames(
        ulong bind,
        string className,
        string method,
        InternedNameReader names,
        out string evidence)
    {
        int classSlot = -1;
        int methodSlot = -1;

        for (int slot = 0; slot < BindNameSlots; slot++)
        {
            ulong at = bind + (ulong)(slot * ByteSourceExtensions.PointerWidth);
            if (!_source.TryReadPointer(at, out ulong value) || value == 0 || (value & 7) != 0)
            {
                continue;
            }

            if (!names.TryRead(value, out string name) || name.Length == 0)
            {
                continue;
            }

            if (classSlot < 0 && string.Equals(name, className, StringComparison.Ordinal))
            {
                classSlot = slot;
            }

            if (methodSlot < 0 && string.Equals(name, method, StringComparison.Ordinal))
            {
                methodSlot = slot;
            }
        }

        if (classSlot < 0 || methodSlot < 0)
        {
            evidence = string.Format(
                CultureInfo.InvariantCulture,
                "MethodBind 0x{0:x} does not name itself {1}::{2} (instance_class {3}, name {4})",
                bind,
                className,
                method,
                classSlot < 0 ? "not found" : "ok",
                methodSlot < 0 ? "not found" : "ok");
            return false;
        }

        evidence = string.Format(
            CultureInfo.InvariantCulture,
            "bind names verified by value: instance_class@+0x{0:x}=\"{1}\", name@+0x{2:x}=\"{3}\"",
            classSlot * ByteSourceExtensions.PointerWidth,
            className,
            methodSlot * ByteSourceExtensions.PointerWidth,
            method);
        return true;
    }
}
