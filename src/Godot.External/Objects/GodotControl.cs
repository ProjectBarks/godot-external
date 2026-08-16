using Godot.External.Abi;
using Godot.External.Bridge;
using Godot.External.Scene;
using Godot.External.Values;

namespace Godot.External.Objects;

/// <summary>
/// A Godot <c>Control</c>: size, position, scale, <c>offset[4]</c>, visibility, and a global position
/// that is actually correct.
/// </summary>
/// <remarks>
/// <para>
/// The scalar offsets behind these reads are the live-validated release column: 30/30 across five
/// menu Controls (docs/analysis.md §12.3) and 60/60 across thirteen combat-UI nodes (§12.4c), with
/// distinctive values matching exactly (<c>BgContainer</c> size <c>[2560,1200]</c>, <c>offset[4]</c>
/// <c>[-960,-516,1600,684]</c>). They are read through the epoch's profile, never inlined here.
/// </para>
/// <para>
/// <b>Nothing on this class checks that the node is a <c>Control</c>.</b> That check belongs to
/// whoever produced the handle — <see cref="GodotNode.TryAsControl"/> performs it,
/// <see cref="SceneEpoch.ControlUnchecked"/> deliberately does not. §12.4c measured what the
/// unchecked path returns: denormal garbage such as <c>2.6e-38</c>, identical to scry's, because the
/// engine has no type check either.
/// </para>
/// </remarks>
internal class GodotControl : GodotNode
{
    internal GodotControl(SceneEpoch epoch, NativePtr address)
        : base(epoch, address)
    {
    }

    /// <summary><c>Control::Data::size_cache</c> — the node's size in pixels.</summary>
    public bool TryGetSize(out GodotVector2 size)
        => ControlGeometry.TryReadVector2(Source, Profile, Address.Address, GodotField.ControlSize, out size);

    /// <summary>Size, or <see langword="null"/> on a failed read.</summary>
    public GodotVector2? Size => TryGetSize(out GodotVector2 v) ? v : null;

    /// <summary><c>Control::Data::pos_cache</c> — position relative to the parent <c>Control</c>.</summary>
    public bool TryGetPosition(out GodotVector2 position)
        => ControlGeometry.TryReadVector2(Source, Profile, Address.Address, GodotField.ControlPosition, out position);

    /// <summary>Local position, or <see langword="null"/> on a failed read.</summary>
    public GodotVector2? Position => TryGetPosition(out GodotVector2 v) ? v : null;

    /// <summary><c>Control::Data::scale</c>.</summary>
    public bool TryGetScale(out GodotVector2 scale)
        => ControlGeometry.TryReadVector2(Source, Profile, Address.Address, GodotField.ControlScale, out scale);

    /// <summary>Scale, or <see langword="null"/> on a failed read.</summary>
    public GodotVector2? Scale => TryGetScale(out GodotVector2 v) ? v : null;

    /// <summary>
    /// <c>Control::Data::offset[4]</c> — the four anchor offsets, in upstream order
    /// (left, top, right, bottom).
    /// </summary>
    /// <param name="destination">At least four elements.</param>
    public bool TryGetOffsets(Span<double> destination)
        => ControlGeometry.TryReadOffsets(Source, Profile, Address.Address, destination);

    /// <summary><c>offset[4]</c> as an array, or <see langword="null"/> on a failed read.</summary>
    public double[]? Offsets
    {
        get
        {
            double[] buffer = new double[4];
            return TryGetOffsets(buffer) ? buffer : null;
        }
    }

    /// <summary><c>CanvasItem::visible</c> — a single byte.</summary>
    /// <remarks>
    /// This is the node's own flag, not effective visibility: a visible child of a hidden parent
    /// reads <see langword="true"/> here. Compose it up the tree if effective visibility is what you
    /// need.
    /// </remarks>
    public bool TryGetVisible(out bool visible)
        => ControlGeometry.TryReadVisible(Source, Profile, Address.Address, out visible);

    /// <summary>The <c>visible</c> flag, or <see langword="null"/> on a failed read.</summary>
    public bool? Visible => TryGetVisible(out bool v) ? v : null;

    /// <summary>
    /// The engine's <b>cached</b> global position. Exposed for cross-checking only.
    /// </summary>
    /// <remarks>
    /// §4.6/§12.3: this is two float reads of a stale cache, observed returning <c>[0,0]</c> for
    /// Controls that plainly had positions. It is stale rather than malformed, so nothing downstream
    /// can detect the problem from the value. Use <see cref="TryGetGlobalPosition"/>.
    /// </remarks>
    public bool TryGetCachedGlobalPosition(out GodotVector2 position)
        => ControlGeometry.TryReadCachedGlobalPosition(Source, Profile, Address.Address, out position);

    /// <summary>
    /// Composes global position by summing local positions up the parent chain, <b>stopping at the
    /// first ancestor that is not a <c>Control</c></b>.
    /// </summary>
    /// <param name="result">The composed position and how far the walk got.</param>
    /// <returns>
    /// <see langword="false"/> if any read failed, the parent chain looped, or it exceeded the depth
    /// bound. A partial sum is never returned as if it were an answer.
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>The gate is load-bearing.</b> <c>pos_cache</c> is a <c>Control</c> field; on a non-Control
    /// ancestor those bytes are some other struct's business and decode to garbage that is finite,
    /// plausible, and wrong (§12.4c: <c>2.6e-38</c>). Adding it to a running sum corrupts the answer
    /// silently, so the walk consults the epoch's <see cref="INodeClassifier"/> before each hop and
    /// halts rather than composing across a type boundary.
    /// </para>
    /// <para>
    /// Composition is <b>translation only</b>, matching scry's <c>computeGlobalPosition</c>: a scaled
    /// or rotated ancestor is not accounted for. That is a limit of the technique — handling it needs
    /// the full <c>Transform2D</c> chain, for which no validated offset exists. <see cref="Scale"/>
    /// is available if a caller wants to detect and refuse the scaled case.
    /// </para>
    /// </remarks>
    public bool TryGetGlobalPosition(out ComposedGlobalPosition result)
    {
        bool stoppedAtNonControl = false;
        ulong self = Address.Address;

        bool ok = ControlGeometry.TryComposeGlobalPosition(
            Source,
            Profile,
            self,
            out GodotVector2 position,
            out int ancestors,
            includeAncestor: candidate =>
            {
                if (Epoch.IsControl(new NativePtr(candidate)))
                {
                    return true;
                }

                // The gate is also asked about the starting node. Rejecting *that* is a refusal to
                // compose at all, not a truncated chain, so it must not be reported as a stop.
                if (candidate != self)
                {
                    stoppedAtNonControl = true;
                }

                return false;
            });

        result = ok
            ? new ComposedGlobalPosition(position, ancestors, stoppedAtNonControl)
            : default;

        return ok;
    }

    /// <summary>The composed global position, or <see langword="null"/> on a failed read.</summary>
    public GodotVector2? GlobalPosition
        => TryGetGlobalPosition(out ComposedGlobalPosition result) ? result.Position : null;
}
