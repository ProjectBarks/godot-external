using Godot.External.Abi;
using Godot.External.Bridge;
using Godot.External.Memory;
using Godot.External.Values;

namespace Godot.External.Scene;

/// <summary>
/// Classifies a node by asking whether its <c>Control</c> geometry <em>looks like</em> geometry.
/// The default classifier, and the one docs/analysis.md §12.4c recommends in the absence of RTTI.
/// </summary>
/// <remarks>
/// <para>
/// The tell is specific and observed. §12.4c: reading Control accessors on an
/// <c>AudioStreamPlayer</c> "returns denormal garbage such as <c>2.6e-38</c>" — and scry returned
/// <em>the same</em> garbage, confirming both were reading identical bytes. Whatever occupies
/// <c>0x470..0x4c8</c> on a non-Control is some other struct's business: pointers, enums, padding.
/// Reinterpreted as <c>real_t</c>, the high half of an x64 heap pointer is a denormal near
/// <c>1e-40</c> and the low half is astronomically large. Neither survives
/// <see cref="ControlGeometry.IsPlausibleCoordinate"/>'s magnitude window, which is shared with the
/// composition backstop so the two cannot drift apart.
/// </para>
/// <para>
/// <b>Zero is the hole in that argument</b>, and <c>scale</c> is the patch. Padding and null pointers
/// decode to <c>0.0</c>, which is a legitimate size and a legitimate position — so a magnitude test
/// alone would read a zeroed region as a Control. It cannot pass a <c>scale</c> test: upstream
/// initialises <c>Control::Data::scale</c> to <c>Vector2(1,1)</c>, and a zero-scaled Control is
/// degenerate. Requiring a non-zero scale is what stops zeroed memory classifying as a Control.
/// </para>
/// <para>
/// <b>This is a heuristic and is documented as one.</b> A caller that can read managed class names
/// (§12.4d proves that needs neither scry nor ClrMD) should supply a
/// <see cref="DelegateNodeClassifier"/> instead; this exists so the composition gate is never simply
/// switched off for want of type information.
/// </para>
/// </remarks>
internal sealed class PlausibilityNodeClassifier : INodeClassifier
{
    /// <summary>The shared instance. Stateless.</summary>
    public static PlausibilityNodeClassifier Instance { get; } = new();

    /// <inheritdoc/>
    public GodotNodeClass Classify(SceneEpoch epoch, NativePtr node)
    {
        ArgumentNullException.ThrowIfNull(epoch);

        if (node.IsNull)
        {
            return GodotNodeClass.NotControl;
        }

        IByteSource source = epoch.Source;
        GodotAbiProfile profile = epoch.Profile;

        // Three Vector2 reads at 0x4a8, 0x4b8 and 0x4c0 are about to happen on one object. A
        // span-granular snapshot serves all three from a single fetch once it knows the extent.
        source.RegisterObject(node.Address);

        if (!ControlGeometry.TryReadVector2(source, profile, node.Address, GodotField.ControlSize, out GodotVector2 size)
            || !ControlGeometry.TryReadVector2(source, profile, node.Address, GodotField.ControlPosition, out GodotVector2 position)
            || !ControlGeometry.TryReadVector2(source, profile, node.Address, GodotField.ControlScale, out GodotVector2 scale))
        {
            // Unmapped memory at Control offsets is weak evidence of a smaller object, but the
            // honest answer is that we do not know.
            return GodotNodeClass.Unknown;
        }

        // Sizes are non-negative in Godot; a negative one is a reinterpreted something-else.
        bool plausible = ControlGeometry.IsPlausibleCoordinate(size.X) && size.X >= 0
            && ControlGeometry.IsPlausibleCoordinate(size.Y) && size.Y >= 0
            && ControlGeometry.IsPlausibleCoordinate(position.X)
            && ControlGeometry.IsPlausibleCoordinate(position.Y)
            && IsPlausibleScale(scale.X)
            && IsPlausibleScale(scale.Y);

        return plausible ? GodotNodeClass.Control : GodotNodeClass.NotControl;
    }

    // Non-zero, unlike the other components: see the remarks on this class.
    private static bool IsPlausibleScale(double value)
        => value != 0 && ControlGeometry.IsPlausibleCoordinate(value);
}
