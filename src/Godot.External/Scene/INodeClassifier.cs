using Godot.External.Bridge;

namespace Godot.External.Scene;

/// <summary>
/// Decides whether a native node is a <c>Control</c>. Pluggable because the good answer and the
/// available answer are different things.
/// </summary>
/// <remarks>
/// <para>
/// The <em>good</em> answer is the managed class name, reachable from the node through the
/// <c>ScriptInstance</c>/GCHandle chain and ECMA-335 metadata (docs/analysis.md §12.4d) — but only
/// for nodes that carry a script, and only with a CLR-side reader this repo does not yet reference.
/// The <em>available</em> answer is plausibility, which §12.4c explicitly recommends. Both plug in
/// here.
/// </para>
/// <para>
/// A classifier must be conservative: answering <see cref="GodotNodeClass.Control"/> wrongly turns
/// denormal bytes into overlay coordinates, whereas answering
/// <see cref="GodotNodeClass.NotControl"/> wrongly merely truncates a global-position composition
/// at a node that would have contributed. One is silent corruption; the other is a visible offset.
/// </para>
/// </remarks>
internal interface INodeClassifier
{
    /// <summary>Classifies the node at <paramref name="node"/> within <paramref name="epoch"/>.</summary>
    GodotNodeClass Classify(SceneEpoch epoch, NativePtr node);
}

/// <summary>Adapts a lambda to <see cref="INodeClassifier"/>.</summary>
/// <param name="classify">
/// Returns the class of a node. A caller with real type identity — e.g. managed class names via
/// §12.4d — should supply it here in preference to <see cref="PlausibilityNodeClassifier"/>.
/// </param>
internal sealed class DelegateNodeClassifier(Func<SceneEpoch, NativePtr, GodotNodeClass> classify) : INodeClassifier
{
    private readonly Func<SceneEpoch, NativePtr, GodotNodeClass> _classify =
        classify ?? throw new ArgumentNullException(nameof(classify));

    /// <inheritdoc/>
    public GodotNodeClass Classify(SceneEpoch epoch, NativePtr node) => _classify(epoch, node);
}
