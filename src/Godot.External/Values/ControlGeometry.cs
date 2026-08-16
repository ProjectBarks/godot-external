using Godot.External.Abi;

namespace Godot.External.Values;

/// <summary>
/// Reads <c>Control</c> geometry, and — more importantly — computes global position the way that
/// actually works.
/// </summary>
/// <remarks>
/// <para>
/// docs/analysis.md §4.6: <c>getGlobalPosition</c> is two float reads of a <b>cached field</b> with
/// no arithmetic anywhere in it, and it returned <c>[0,0]</c> live for Controls that plainly had
/// positions (§12.3). Composition up the parent chain is the correct approach and the one scry's own
/// <c>computeGlobalPosition</c> uses.
/// </para>
/// <para>
/// Vectors and <c>offset[4]</c> are fetched in <b>one</b> remote read each. Reading x and y
/// separately can tear across a mutation and produce two plausible halves of different samples —
/// the same silent class of failure as §12.4e's short walk, with no error to key off.
/// </para>
/// </remarks>
internal static class ControlGeometry
{
    /// <summary>
    /// Depth bound for the parent walk. Real scene trees are shallow; a chain longer than this is a
    /// corrupt or reused pointer (§8.8: Godot frees nodes and reuses allocations).
    /// </summary>
    public const int MaxAncestorDepth = 256;

    /// <summary>
    /// Smallest non-zero coordinate magnitude treated as real. Sub-thousandth-of-a-pixel positions
    /// do not occur in UI layout, but reading a <c>Control</c> field off a non-<c>Control</c> does
    /// produce tiny denormal-ish floats — §12.4c saw <c>2.6e-38</c> from exactly that mistake.
    /// </summary>
    public const double MinPlausibleMagnitude = 1e-3;

    /// <summary>Largest coordinate magnitude treated as real; far beyond any sane viewport.</summary>
    public const double MaxPlausibleMagnitude = 1e7;

    /// <summary>
    /// Reads a two-component <c>real_t</c> field (position, size, scale, cached global position) in
    /// a single remote read.
    /// </summary>
    public static bool TryReadVector2(
        IByteSource source,
        GodotAbiProfile profile,
        ulong controlAddress,
        GodotField field,
        out GodotVector2 value)
    {
        ArgumentNullException.ThrowIfNull(profile);

        value = GodotVector2.Zero;
        if (!source.IsSupportedTarget())
        {
            return false;
        }

        Span<byte> block = stackalloc byte[2 * 8];
        int width = 2 * profile.RealSize;

        if (!source.TryRead(profile.FieldAddress(controlAddress, field), block[..width]))
        {
            return false;
        }

        value = new GodotVector2(
            ByteSourceExtensions.DecodeReal(block, 0, profile.Precision),
            ByteSourceExtensions.DecodeReal(block, 1, profile.Precision));

        return true;
    }

    /// <summary>
    /// Reads <c>Control::Data::offset[4]</c> (the four anchor offsets) in a single remote read.
    /// </summary>
    public static bool TryReadOffsets(
        IByteSource source,
        GodotAbiProfile profile,
        ulong controlAddress,
        Span<double> destination)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (destination.Length < 4)
        {
            throw new ArgumentException("Control::Data::offset has four elements.", nameof(destination));
        }

        if (!source.IsSupportedTarget())
        {
            return false;
        }

        Span<byte> block = stackalloc byte[4 * 8];
        int width = 4 * profile.RealSize;

        if (!source.TryRead(profile.FieldAddress(controlAddress, GodotField.ControlOffsets), block[..width]))
        {
            return false;
        }

        for (int i = 0; i < 4; i++)
        {
            destination[i] = ByteSourceExtensions.DecodeReal(block, i, profile.Precision);
        }

        return true;
    }

    /// <summary>Reads <c>CanvasItem::visible</c> — a single byte.</summary>
    public static bool TryReadVisible(
        IByteSource source,
        GodotAbiProfile profile,
        ulong canvasItemAddress,
        out bool visible)
    {
        ArgumentNullException.ThrowIfNull(profile);

        visible = false;

        // Width-independent read, but the offset it uses is not: every shipped profile describes an
        // x64 layout, so an unsupported target is refused here too rather than half-answered.
        return source.IsSupportedTarget()
            && source.TryReadBoolean(
                profile.FieldAddress(canvasItemAddress, GodotField.CanvasItemVisible),
                out visible);
    }

    /// <summary>
    /// Reads the engine's <b>cached</b> global position. Provided for cross-checking only —
    /// <see cref="TryComposeGlobalPosition"/> is the value to use.
    /// </summary>
    /// <remarks>
    /// Stale <c>[0,0]</c> is the documented, observed failure (§12.3: <c>MainMenuTextButtons</c> and
    /// <c>ContinueButton</c>). It is stale rather than wrong-looking, so nothing downstream can
    /// detect the problem from the value alone.
    /// </remarks>
    public static bool TryReadCachedGlobalPosition(
        IByteSource source,
        GodotAbiProfile profile,
        ulong controlAddress,
        out GodotVector2 value)
        => TryReadVector2(source, profile, controlAddress, GodotField.ControlGlobalPosition, out value);

    /// <summary>
    /// Computes global position by summing local <c>pos_cache</c> from
    /// <paramref name="controlAddress"/> up the parent chain, stopping at the first node
    /// <paramref name="includeAncestor"/> rejects.
    /// </summary>
    /// <param name="source">Target memory.</param>
    /// <param name="profile">Supplies every offset used here; none are hardcoded.</param>
    /// <param name="controlAddress">Native <c>Control*</c> to start from. Also passed through
    /// <paramref name="includeAncestor"/> — the starting node is not trusted either.</param>
    /// <param name="value">The composed position. Meaningless unless the method returns true.</param>
    /// <param name="includeAncestor">
    /// <b>Required — there is deliberately no default.</b> Called for the starting node and for every
    /// candidate ancestor; return
    /// <see langword="false"/> for anything that is not a <c>Control</c>.
    /// <see cref="GodotField.ControlPosition"/> is a <c>Control</c> field, and reading it off a plain
    /// <c>Node</c> container or the root <c>Window</c> — which every real parent chain passes through
    /// — <em>succeeds</em> and returns garbage (§12.4c: denormals such as <c>2.6e-38</c>). There is
    /// no type information at this layer, so walking to the root unconditionally would silently
    /// corrupt the sum while still reporting success. An optional gate would make the convenient
    /// overload the unsafe one, so there isn't one.
    /// </param>
    /// <param name="ancestorCount">How many ancestors contributed — useful for diagnostics.</param>
    /// <returns>
    /// <see langword="false"/> if the starting node is rejected, any read failed, a component was
    /// implausible, the chain looped, or it exceeded <see cref="MaxAncestorDepth"/>. A partial sum is
    /// never returned as if it were an answer.
    /// </returns>
    /// <remarks>
    /// This is translation-only composition, matching scry's <c>computeGlobalPosition</c>: a scaled
    /// or rotated ancestor is not accounted for. That is a known limit of the technique, not an
    /// oversight — handling it needs the full <c>Transform2D</c> chain, which no validated offset
    /// exists for. <see cref="GodotField.ControlScale"/> is available if a caller wants to detect
    /// (and refuse) the scaled case.
    /// </remarks>
    public static bool TryComposeGlobalPosition(
        IByteSource source,
        GodotAbiProfile profile,
        ulong controlAddress,
        out GodotVector2 value,
        out int ancestorCount,
        Func<ulong, bool> includeAncestor)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(includeAncestor);

        value = GodotVector2.Zero;
        ancestorCount = 0;

        if (controlAddress == 0 || !includeAncestor(controlAddress))
        {
            return false;
        }

        GodotVector2 sum = GodotVector2.Zero;
        HashSet<ulong> visited = [];
        ulong current = controlAddress;

        while (true)
        {
            if (!visited.Add(current))
            {
                return false; // parent chain loops: the tree is not a tree
            }

            if (visited.Count > MaxAncestorDepth)
            {
                return false;
            }

            if (!TryReadVector2(source, profile, current, GodotField.ControlPosition, out GodotVector2 local))
            {
                return false;
            }

            // Backstop for a gate that let a non-Control through: §12.5's "validate by plausibility",
            // since there is no type check to lean on.
            if (!IsPlausibleCoordinate(local.X) || !IsPlausibleCoordinate(local.Y))
            {
                return false;
            }

            sum += local;

            if (!source.TryReadPointer(profile.FieldAddress(current, GodotField.NodeParent), out ulong parent))
            {
                return false;
            }

            if (parent == 0 || !includeAncestor(parent))
            {
                break;
            }

            ancestorCount++;
            current = parent;
        }

        value = sum;
        return true;
    }

    /// <summary>
    /// Whether a coordinate looks like layout rather than like bytes reinterpreted as a float.
    /// Zero is always plausible — it is the overwhelmingly common real value.
    /// </summary>
    public static bool IsPlausibleCoordinate(double value)
    {
        if (value == 0)
        {
            return true;
        }

        double magnitude = Math.Abs(value);
        return double.IsFinite(value)
            && magnitude >= MinPlausibleMagnitude
            && magnitude <= MaxPlausibleMagnitude;
    }
}
