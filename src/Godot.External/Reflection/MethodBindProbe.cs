using Godot.External.Abi;

namespace Godot.External.Reflection;

/// <summary>
/// Finds the getter's code address inside a <c>MethodBind</c>, without hardcoding
/// <c>sizeof(MethodBind)</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>MethodBindTRC&lt;T, R, P...&gt;::method</c> is the first member after the <c>MethodBind</c>
/// base, so its offset is exactly <c>sizeof(MethodBind)</c> — a number that <b>shifts between
/// builds</b>, because <c>MethodBind::arg_names</c> is compiled in only under
/// <c>DEBUG_ENABLED</c>. Hardcoding it would work on one template and read a neighbouring field on
/// the other.
/// </para>
/// <para>
/// So it is probed instead: read the first few qwords and keep the ones that land inside the main
/// module's executable range. Nothing else in a <c>MethodBind</c> is a code pointer — the vtable
/// slot points into <c>.rdata</c>, and the rest are heap pointers, ints and <c>StringName</c>
/// handles — so a single hit identifies the slot and simultaneously proves the base address was
/// right. <b>Two hits refuse</b>: an ambiguity here would pick a code address at random, and the
/// decoder downstream would happily decode whatever function that was.
/// </para>
/// <para>
/// <b>Validated live.</b> Confirmed on all eight grid cells (4.3 and 4.5, release and debug, both
/// bindings) against <c>Label::get_text</c> and <c>CanvasItem::is_visible</c> resolved by name
/// through a <c>ClassDB</c> walk: exactly one probed slot lands in <c>.text</c> in every case, and
/// the decoded displacement matches the calibrated offset. See docs/analysis.md §16.5.
/// </para>
/// </remarks>
internal static class MethodBindProbe
{
    /// <summary>
    /// How many qwords from the start of the <c>MethodBind</c> to inspect.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Measured, not assumed.</b> <c>sizeof(MethodBind)</c> reads <c>0x48</c> on the release
    /// templates and <c>0x58</c> on the debug ones — identically on 4.3 and 4.5 — so the method
    /// pointer sits at <b>slot 9</b> or <b>slot 11</b>. Twelve reaches both with one slot to spare.
    /// </para>
    /// <para>
    /// <b>This shipped as 8, which refused 100% of probes on every grid cell.</b> Eight slots stop
    /// one short of even the release layout, so the probe found no code address and reported "this
    /// is not a typed MethodBind" for binds that were perfectly valid. §13.7 predicted the
    /// debug/release shift (<c>arg_names</c> is <c>DEBUG_ENABLED</c>-only) and the default never
    /// accommodated it.
    /// </para>
    /// <para>
    /// <b>The window is still a window, not a size.</b> Hardcoding 9 or 11 would be the mistake
    /// §13.7 warns against: probing for the slot whose value lands in the main module's
    /// <c>.text</c> is self-validating, because a hit simultaneously proves the base address was
    /// right. Staying small is what keeps a spurious second hit unlikely — measured, slots 0-11
    /// hold exactly one <c>.text</c> pointer on all eight cells, with the neighbouring
    /// <c>MethodBind</c>'s vtable (which points into <c>.rdata</c>, not <c>.text</c>) the nearest
    /// thing to a competitor.
    /// </para>
    /// </remarks>
    public const int DefaultProbeSlots = 12;

    /// <summary>
    /// Locates the single code pointer stored in a <c>MethodBind</c>.
    /// </summary>
    /// <param name="source">Target memory.</param>
    /// <param name="methodBindAddress">Address of the <c>MethodBind</c> object.</param>
    /// <param name="executableRange">
    /// The main module's executable span. Supplied by the caller because this module does not read
    /// PE headers; a range that is too wide weakens the check rather than breaking it.
    /// </param>
    /// <param name="codeAddress">The recovered getter entry point; zero on any refusal.</param>
    /// <param name="slotIndex">Which qword it was found in, i.e. <c>sizeof(MethodBind) / 8</c>.</param>
    /// <param name="reason">Diagnosis. Empty on success.</param>
    /// <param name="probeSlots">How many qwords to inspect; see <see cref="DefaultProbeSlots"/>.</param>
    /// <returns><see langword="false"/> unless exactly one probed slot held a code address.</returns>
    public static bool TryFindMethodPointer(
        IByteSource source,
        ulong methodBindAddress,
        CodeRegion executableRange,
        out ulong codeAddress,
        out int slotIndex,
        out string reason,
        int probeSlots = DefaultProbeSlots)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(probeSlots);

        codeAddress = 0;
        slotIndex = -1;

        if (!source.IsSupportedTarget())
        {
            reason = "target is not 64-bit; the typed-MethodBind route is Windows x86-64 only";
            return false;
        }

        if (executableRange.IsEmpty)
        {
            reason = "no executable range supplied, so a probed qword cannot be told from a heap pointer";
            return false;
        }

        int hits = 0;

        for (int slot = 0; slot < probeSlots; slot++)
        {
            ulong at = methodBindAddress + (ulong)(slot * ByteSourceExtensions.PointerWidth);

            if (!source.TryReadPointer(at, out ulong value))
            {
                // A short read part-way through is not fatal: MethodBind is comfortably larger than
                // the probe window, so an unreadable slot means the base address was wrong.
                reason = $"read failed at slot {slot}; {methodBindAddress:x} is not a readable MethodBind";
                return false;
            }

            if (!executableRange.Contains(value))
            {
                continue;
            }

            hits++;
            if (hits > 1)
            {
                reason = $"slots {slotIndex} and {slot} both hold code addresses; the method pointer " +
                    "cannot be identified and nothing is published";
                codeAddress = 0;
                slotIndex = -1;
                return false;
            }

            codeAddress = value;
            slotIndex = slot;
        }

        if (hits == 0)
        {
            reason = $"no code address in the first {probeSlots} qword(s); this is not a typed MethodBind";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
