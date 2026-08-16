namespace Godot.External.Abi;

/// <summary>
/// One cell of the Godot compat matrix: engine version × export template × <c>real_t</c> precision,
/// plus the offsets that combination implies.
/// </summary>
/// <remarks>
/// <para>
/// A profile is <b>data</b>, not code. docs/analysis.md §12.5 showed the offsets can be re-derived
/// at connect time from ground truth the runtime supplies about itself, so a shipped profile is a
/// fast path and a self-check. The intended sequence is: pick a profile, calibrate, diff, and treat
/// disagreement as a loud startup diagnostic rather than a silent fallback (§8.9, assertion 4).
/// </para>
/// <para>
/// Lifetime: a profile is <b>process-tier</b> state (§8.8) — it depends only on the loaded binary,
/// so it outlives snapshots and scene epochs. Native <c>Node*</c> values derived <em>with</em> it do
/// not: Godot can free a node and reuse the allocation, so a stale pointer can address a different,
/// entirely plausible-looking node. Never cache addresses alongside a profile.
/// </para>
/// </remarks>
public sealed record GodotAbiProfile
{
    /// <summary>
    /// Engine version <em>without</em> any <c>-debug</c> suffix, e.g. <c>"4.5.1"</c>. The suffix is
    /// not part of the version — it selects <see cref="Template"/> (§4.6).
    /// </summary>
    public required string EngineVersion { get; init; }

    /// <summary>Export template. This, not the version, is what the layout branches on (§4.6).</summary>
    public required GodotBuildTemplate Template { get; init; }

    /// <summary>Width of <c>real_t</c> in this build.</summary>
    public required GodotPrecision Precision { get; init; }

    /// <summary>The offsets themselves.</summary>
    public required GodotOffsetTable Offsets { get; init; }

    /// <summary>
    /// Trust in this profile <b>as a whole</b>: the weakest of its parts, never the strongest. See
    /// <see cref="AbiConfidence"/> for why the ordering matters and
    /// <see cref="WithCalibratedOffset"/> for how overriding a field affects it.
    /// </summary>
    public AbiConfidence Confidence { get; init; } = AbiConfidence.Unvalidated;

    /// <summary>
    /// Bitmask of fields whose values came from the calibrator rather than from this profile's
    /// shipped table — per-field provenance, so a diagnostic can say <em>which</em> offsets were
    /// overridden and which are still just the table's word.
    /// </summary>
    /// <remarks>
    /// A mask rather than a set: <see cref="GodotField"/> has well under 64 members, and a value-typed
    /// mask keeps record equality meaningful (two profiles with identical data compare equal, which a
    /// collection member would break).
    /// </remarks>
    public ulong CalibratedFieldMask { get; init; }

    /// <summary>Free-text provenance, surfaced in diagnostics.</summary>
    public string? Notes { get; init; }

    /// <summary>Size of one <c>real_t</c>, i.e. the stride between vector components and array elements.</summary>
    public int RealSize => ByteSourceExtensions.RealSize(Precision);

    /// <summary>Whether <paramref name="field"/>'s value was measured on the live target.</summary>
    public bool IsCalibrated(GodotField field) => (CalibratedFieldMask & MaskOf(field)) != 0;

    /// <summary>Absolute address of <paramref name="field"/> on the object at <paramref name="objectAddress"/>.</summary>
    public ulong FieldAddress(ulong objectAddress, GodotField field)
        => objectAddress + (ulong)(long)Offsets[field];

    /// <summary>
    /// Absolute address of component <paramref name="index"/> of a <c>real_t</c> vector or array
    /// field — <c>y</c> of a Vector2, or <c>offset[i]</c>. The stride is <see cref="RealSize"/>,
    /// which is why the release table records <c>0x3f8</c>/<c>0x3fc</c> as a single base.
    /// </summary>
    public ulong ComponentAddress(ulong objectAddress, GodotField field, int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        return objectAddress + (ulong)(long)(Offsets[field] + (index * RealSize));
    }

    /// <summary>
    /// Returns a copy with one offset replaced by a calibrated value.
    /// </summary>
    /// <remarks>
    /// Confidence can only fall. A <see cref="AbiConfidence.LiveValidated"/> table that has been
    /// partly overwritten is no longer the table that was validated end to end, so it drops to
    /// <see cref="AbiConfidence.Calibrated"/>; an <see cref="AbiConfidence.Unvalidated"/> base
    /// <b>stays</b> unvalidated, because deriving one of its eighteen offsets says nothing about the
    /// other seventeen. Without that rule, calibrating a single field on the known-defective debug
    /// table would launder it to the top of the trust order.
    /// </remarks>
    public GodotAbiProfile WithCalibratedOffset(GodotField field, int offset) => this with
    {
        Offsets = Offsets.With(field, offset),
        Confidence = Weakest(Confidence, AbiConfidence.Calibrated),
        CalibratedFieldMask = CalibratedFieldMask | MaskOf(field),
    };

    /// <summary>
    /// Returns a copy whose offsets were <em>wholly</em> produced by the calibrator. Every field is
    /// marked calibrated, and the result claims exactly <see cref="AbiConfidence.Calibrated"/> — it
    /// neither inherits a shipped table's validation nor is dragged down by it, because none of the
    /// old numbers survive.
    /// </summary>
    public GodotAbiProfile WithCalibratedOffsets(GodotOffsetTable offsets)
    {
        ArgumentNullException.ThrowIfNull(offsets);

        return this with
        {
            Offsets = offsets,
            Confidence = AbiConfidence.Calibrated,
            CalibratedFieldMask = AllFieldsMask,
        };
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        string calibrated = CalibratedFieldMask == 0
            ? string.Empty
            : $", {System.Numerics.BitOperations.PopCount(CalibratedFieldMask)} calibrated";

        return $"Godot {EngineVersion} {Template} {Precision} ({Confidence}{calibrated})";
    }

    /// <summary>Mask with every <see cref="GodotField"/> bit set.</summary>
    internal static ulong AllFieldsMask { get; } = BuildAllFieldsMask();

    private static ulong MaskOf(GodotField field) => 1ul << (int)field;

    private static AbiConfidence Weakest(AbiConfidence left, AbiConfidence right)
        => (AbiConfidence)Math.Min((int)left, (int)right);

    private static ulong BuildAllFieldsMask()
    {
        ulong mask = 0;
        foreach (GodotField field in Enum.GetValues<GodotField>())
        {
            // A 65th field would silently alias bit 0; fail loudly at type-load instead.
            ArgumentOutOfRangeException.ThrowIfGreaterThan((int)field, 63, nameof(field));
            mask |= MaskOf(field);
        }

        return mask;
    }
}
