using System.Diagnostics.CodeAnalysis;
using Godot.External.Abi;
using Godot.External.Bridge;
using Godot.External.Scene;
using Godot.External.Values;

namespace Godot.External.Objects;

/// <summary>
/// A Godot <c>Node</c> in the target process: name, parent, children, and the route to its C# object.
/// </summary>
/// <remarks>
/// <para>
/// <b>Epoch-scoped by construction.</b> There is no accessible constructor; the only source of a
/// <see cref="GodotNode"/> is a live <see cref="SceneEpoch"/>, and every read below re-enters through
/// <see cref="SceneEpoch.Source"/>, which throws once the epoch has ended. docs/analysis.md §8.8:
/// "Godot can free a node and reuse the allocation", so a handle that outlives its epoch would
/// otherwise return a different, entirely plausible-looking node's data.
/// </para>
/// <para>
/// <b>No hardcoded offsets.</b> Every field address comes from <see cref="SceneEpoch.Profile"/>, so a
/// calibrated profile (§12.5) can replace the shipped table without touching this class.
/// </para>
/// <para>
/// Reads follow §8.8's error model: <c>TryGet…</c> returns <see langword="false"/> rather than
/// throwing, so a polling loop can answer a bad read by reusing its last good snapshot. The
/// convenience properties return <see langword="null"/> on the same condition and are for interactive
/// use, not for the hot path, where the reason for the failure matters.
/// </para>
/// </remarks>
internal class GodotNode : IEquatable<GodotNode>
{
    internal GodotNode(SceneEpoch epoch, NativePtr address)
    {
        ArgumentNullException.ThrowIfNull(epoch);
        Epoch = epoch;
        Address = address;
    }

    /// <summary>The epoch this handle belongs to, and outside which it is invalid.</summary>
    public SceneEpoch Epoch { get; }

    /// <summary>The native <c>Node*</c>.</summary>
    public NativePtr Address { get; }

    /// <summary>The ABI profile in force.</summary>
    protected GodotAbiProfile Profile => Epoch.Profile;

    /// <summary>Memory access, gated on the epoch still being current.</summary>
    /// <exception cref="SceneEpochExpiredException">The epoch has ended.</exception>
    internal IByteSource Source => Epoch.Source;

    /// <summary>
    /// Reads the node's name: <c>node + NodeName</c> &#8594; <c>StringName::_Data</c> &#8594;
    /// <c>+0x08</c> &#8594; UTF-32 buffer (§4.6; 8/8 live in §12.3b).
    /// </summary>
    /// <remarks>
    /// An empty name is a success, not a failure — Godot permits a null <c>StringName</c>. Decoding
    /// is proper UTF-32 via <c>Values/GodotString</c>, not the byte truncation §4.6 flags in scry's
    /// own <c>getName</c>, and the buffer is read in one bulk read rather than per code unit.
    /// </remarks>
    public bool TryGetName(out string name)
        => GodotStringName.TryReadNodeName(Source, Profile, Address.Address, out name);

    /// <summary>The node's name, or <see langword="null"/> if it could not be read.</summary>
    public string? Name => TryGetName(out string name) ? name : null;

    /// <summary>
    /// Reads the parent pointer (<c>node + NodeParent</c>; 4/4 live in §12.3b).
    /// </summary>
    /// <param name="parent">
    /// The parent, or <see langword="null"/> when this node is a root. A null parent is a
    /// <see langword="true"/> return: "no parent" and "could not read" must not collapse into one
    /// answer, because the first terminates an ancestor walk correctly and the second invalidates it.
    /// </param>
    public bool TryGetParent(out GodotNode? parent)
    {
        parent = null;
        if (!Source.TryReadPointer(Profile.FieldAddress(Address.Address, GodotField.NodeParent), out ulong raw))
        {
            return false;
        }

        if (raw != 0)
        {
            parent = new GodotNode(Epoch, new NativePtr(raw));
        }

        return true;
    }

    /// <summary>The parent node, or <see langword="null"/> for a root or a failed read.</summary>
    public GodotNode? Parent => TryGetParent(out GodotNode? parent) ? parent : null;

    /// <summary>
    /// Walks the intrusive child list, returning raw addresses and the structural verdict.
    /// </summary>
    /// <param name="attempts">
    /// Traversals allowed for the agree-twice check. Two is the minimum that can detect §12.4e's
    /// tearing at all; three gives one retry, which is appropriate while the tree is mutating.
    /// </param>
    public ChildWalkResult ReadChildAddresses(int attempts = 2)
        => ChildListWalk.WalkStable(Source, Profile.Offsets, Address.Address, attempts);

    /// <summary>Walks the child list and materialises handles on this epoch.</summary>
    /// <param name="attempts">See <see cref="ReadChildAddresses"/>.</param>
    public NodeChildren GetChildren(int attempts = 2)
    {
        ChildWalkResult walk = ReadChildAddresses(attempts);

        List<GodotNode> nodes = new(walk.Children.Count);
        foreach (ulong child in walk.Children)
        {
            nodes.Add(new GodotNode(Epoch, new NativePtr(child)));
        }

        return new NodeChildren(nodes, walk.Status);
    }

    /// <summary>
    /// Reads Godot's <c>ScriptInstance*</c> for this node — <b>not</b> the managed object. Use
    /// <see cref="ResolveManagedObject"/> to reach the C# side.
    /// </summary>
    /// <param name="scriptInstance">The <c>ScriptInstance*</c>; null when the node has no script.</param>
    public bool TryGetScriptInstance(out NativePtr scriptInstance)
    {
        scriptInstance = NativePtr.Null;
        if (!Source.TryReadPointer(Profile.FieldAddress(Address.Address, GodotField.NodeScriptInstance), out ulong raw))
        {
            return false;
        }

        scriptInstance = new NativePtr(raw);
        return true;
    }

    /// <summary>
    /// Crosses to the C# object behind this node, verifying the <c>ScriptInstance</c> owner
    /// back-reference on the way.
    /// </summary>
    /// <remarks>
    /// See <see cref="GodotObjectBridge"/>. In short: the pointer scry's <c>getDotNetCoreObject</c>
    /// returns is the <c>ScriptInstance</c>, the managed object is two further hops away, and the
    /// back-reference is checked so a wrong pointer fails instead of resolving to something plausible
    /// (§4.6, §12.3b).
    /// </remarks>
    public ScriptInstanceChain ResolveManagedObject()
    {
        Epoch.EnsureCurrent();
        return Epoch.Bridge.ResolveManagedObject(Address);
    }

    /// <summary>Classifies this node through the epoch's <see cref="INodeClassifier"/>.</summary>
    public GodotNodeClass Classify() => Epoch.Classify(Address);

    /// <summary><see langword="true"/> only when positively identified as a <c>Control</c>.</summary>
    public bool IsControl => Classify() == GodotNodeClass.Control;

    /// <summary>
    /// Returns a <see cref="GodotControl"/> view, but only if the node is classified as a
    /// <c>Control</c>.
    /// </summary>
    /// <remarks>
    /// The gate is the point. §12.4c: Control accessors on a non-Control succeed and return denormal
    /// garbage, because the engine performs no type check — so refusing here is the only place the
    /// mistake can be stopped.
    /// </remarks>
    public bool TryAsControl([NotNullWhen(true)] out GodotControl? control)
    {
        control = IsControl ? new GodotControl(Epoch, Address) : null;
        return control is not null;
    }

    /// <summary>Returns a <see cref="GodotLabel"/> view if the node is classified as a <c>Control</c>.</summary>
    /// <remarks>
    /// The classifier cannot distinguish <c>Label</c> from any other <c>Control</c> — nothing at this
    /// layer can. A caller reading text off the wrong Control subclass gets a wrong string, not a
    /// wrong pointer, so this is the mild failure; supply a real type oracle to remove it entirely.
    /// </remarks>
    public bool TryAsLabel([NotNullWhen(true)] out GodotLabel? label)
    {
        label = IsControl ? new GodotLabel(Epoch, Address) : null;
        return label is not null;
    }

    /// <summary>Returns a <see cref="GodotRichTextLabel"/> view if the node is classified as a <c>Control</c>.</summary>
    /// <remarks>See the remarks on <see cref="TryAsLabel"/>.</remarks>
    public bool TryAsRichTextLabel([NotNullWhen(true)] out GodotRichTextLabel? label)
    {
        label = IsControl ? new GodotRichTextLabel(Epoch, Address) : null;
        return label is not null;
    }

    /// <summary>
    /// Enumerates ancestors from the immediate parent upwards, stopping at the root, at a failed
    /// read, or at <paramref name="maxDepth"/>.
    /// </summary>
    /// <remarks>
    /// Bounded and cycle-checked for the same reason <c>ControlGeometry</c>'s walk is: a
    /// freed-and-reused node (§8.8) can produce a parent chain that is not a tree, and an unbounded
    /// walk over one does not terminate.
    /// </remarks>
    public IEnumerable<GodotNode> Ancestors(int maxDepth = ControlGeometry.MaxAncestorDepth)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxDepth);

        HashSet<ulong> visited = [Address.Address];
        GodotNode current = this;

        for (int depth = 0; depth < maxDepth; depth++)
        {
            if (!current.TryGetParent(out GodotNode? parent) || parent is null)
            {
                yield break;
            }

            if (!visited.Add(parent.Address.Address))
            {
                yield break; // the parent chain loops: not a tree
            }

            yield return parent;
            current = parent;
        }
    }

    /// <summary>
    /// Two handles are equal when they name the same address <b>in the same epoch</b>. The epoch is
    /// part of identity, not incidental: §8.8's whole point is that the same address in two epochs
    /// may be two different nodes.
    /// </summary>
    public bool Equals(GodotNode? other)
        => other is not null && Address == other.Address && ReferenceEquals(Epoch, other.Epoch);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as GodotNode);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Address, Epoch.Id);

    /// <inheritdoc/>
    public override string ToString() => $"{GetType().Name}({Address}) @epoch#{Epoch.Id}";
}
