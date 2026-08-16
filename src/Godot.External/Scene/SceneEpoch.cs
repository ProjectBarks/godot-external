using Godot.External.Abi;
using Godot.External.Bridge;
using Godot.External.Objects;

namespace Godot.External.Scene;

/// <summary>
/// The lifetime owner for native Godot pointers. Every handle in this library belongs to exactly one
/// epoch and stops working when that epoch ends.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this type exists.</b> docs/analysis.md §8.8 corrects an earlier draft that called native
/// pointers session-scoped: "The CLR GC does not move them, but <b>Godot can free a node and reuse
/// the allocation</b> — so a stale native pointer can address a different, entirely plausible-looking
/// node. Silent, like §12.4e." Nothing in the bytes gives the substitution away: the name decodes to
/// a real string, the geometry is finite, the child walk succeeds. The doc's instruction is explicit:
/// "Encode it: … <c>GodotNode</c> cannot outlive a <c>SceneEpoch</c>. This turns §7b.1 and §12.4e
/// from documentation into type errors."
/// </para>
/// <para>
/// <b>How it is encoded.</b> Three properties together, none of which is a comment:
/// </para>
/// <list type="number">
/// <item>
/// <see cref="NativePtr"/> is <b>inert</b> — it carries an address and has no method that reads
/// memory. An address that escapes an epoch cannot be dereferenced by anything in this library.
/// </item>
/// <item>
/// <see cref="GodotNode"/> and its subclasses are <b>reference types with no accessible
/// constructor</b>. The only way to obtain one is from a live epoch, and because they are classes
/// rather than structs there is no <c>default</c> value with a null epoch to smuggle one in through.
/// </item>
/// <item>
/// Every read re-enters through <see cref="Source"/>, which calls <see cref="EnsureCurrent"/> and
/// throws <see cref="SceneEpochExpiredException"/> on an ended epoch. A handle that outlives its
/// epoch fails loudly at the moment of use rather than returning a different node's data.
/// </item>
/// </list>
/// <para>
/// A <c>readonly ref struct</c> handle was considered — it would make "cannot outlive" a compiler
/// error outright — and rejected because a scene walk must materialise thousands of nodes into a
/// list, which ref structs cannot enter. The three properties above are what remains achievable
/// without crippling the API.
/// </para>
/// <para>
/// <b>When to end an epoch.</b> Whenever the scene tree could have been rebuilt underneath you: a
/// scene change, a run transition, a re-attach, or a §12.4e structural-suspicion signal you decided
/// not to trust. Ending is cheap; keeping a stale epoch is not. Epochs are the middle tier of §8.8's
/// three — process-tier state (the ABI profile, module info) outlives them, and snapshot-tier state
/// (managed addresses, values) is shorter-lived still.
/// </para>
/// </remarks>
internal sealed class SceneEpoch : IDisposable
{
    private static int _nextId;

    private readonly IByteSource _source;
    private volatile bool _ended;

    internal SceneEpoch(IByteSource source, GodotAbiProfile profile, INodeClassifier? classifier = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(profile);

        // Refused rather than half-supported: every shipped profile was recovered from an x64
        // binary, so a 32-bit target would need its own calibrated profile anyway and reading it
        // through these offsets would produce plausible garbage.
        if (!source.IsSupportedTarget())
        {
            throw new NotSupportedException(
                "Godot.External reads 64-bit targets only. A 32-bit process needs its own calibrated "
              + "profile (docs/analysis.md §12.5); no shipped offset table describes one.");
        }

        _source = source;
        Profile = profile;
        Classifier = classifier ?? PlausibilityNodeClassifier.Instance;
        Bridge = new GodotObjectBridge(source, profile);
        Id = Interlocked.Increment(ref _nextId);
    }

    /// <summary>Opens a new epoch over a caller-supplied reader.</summary>
    /// <param name="read">Remote read primitive; see <see cref="RemoteRead"/>.</param>
    /// <param name="is64Bit">Pointer width of the target process.</param>
    /// <param name="profile">
    /// The ABI profile. Every offset used by this epoch's handles comes from here — no layer above
    /// <c>Abi/</c> hardcodes a constant, which is what lets a calibrated profile (§12.5) be
    /// substituted for the shipped one.
    /// </param>
    /// <param name="classifier">
    /// How to decide "is this a Control?". Defaults to <see cref="PlausibilityNodeClassifier"/>;
    /// supply a real type oracle when one is available.
    /// </param>
    public static SceneEpoch Begin(
        RemoteRead read,
        bool is64Bit,
        GodotAbiProfile profile,
        INodeClassifier? classifier = null)
        => new(new RemoteReadByteSource(read, is64Bit), profile, classifier);

    /// <summary>
    /// Monotonic identifier, unique within the process. Useful in diagnostics for the case this type
    /// guards against: two handles that agree on an address but not on an epoch are not the same node.
    /// </summary>
    public int Id { get; }

    /// <summary>The ABI profile all offsets are read from.</summary>
    public GodotAbiProfile Profile { get; }

    /// <summary>The managed&#8596;native bridge scoped to this epoch.</summary>
    public GodotObjectBridge Bridge { get; }

    /// <summary>How this epoch answers "is this a Control?".</summary>
    public INodeClassifier Classifier { get; }

    /// <summary><see langword="true"/> once <see cref="End"/> or <see cref="Dispose"/> has run.</summary>
    public bool HasEnded => _ended;

    /// <summary>Pointer width of the target process, in bytes. Fixed at 8; see <see cref="Begin"/>.</summary>
    public int PointerSize => ByteSourceExtensions.PointerWidth;

    /// <summary>Memory access, gated on the epoch still being current.</summary>
    /// <exception cref="SceneEpochExpiredException">The epoch has ended.</exception>
    internal IByteSource Source
    {
        get
        {
            EnsureCurrent();
            return _source;
        }
    }

    /// <summary>Throws if this epoch has ended.</summary>
    /// <exception cref="SceneEpochExpiredException">The epoch has ended.</exception>
    public void EnsureCurrent()
    {
        if (_ended)
        {
            throw new SceneEpochExpiredException(Id);
        }
    }

    /// <summary>
    /// Ends the epoch, invalidating every handle taken from it. Idempotent.
    /// </summary>
    public void End() => _ended = true;

    /// <inheritdoc/>
    public void Dispose() => End();

    /// <summary>Takes a node handle on this epoch.</summary>
    /// <exception cref="SceneEpochExpiredException">The epoch has ended.</exception>
    public GodotNode Node(NativePtr address)
    {
        EnsureCurrent();
        return new GodotNode(this, address);
    }

    /// <summary>
    /// Takes a <c>Control</c> handle <b>without checking that the node is one</b>.
    /// </summary>
    /// <remarks>
    /// Matches the engine's own behaviour, which §12.4c measured: <c>engine.getControl()</c> on a
    /// non-Control succeeds and returns denormal garbage, because there is no type check anywhere in
    /// the accessor. Prefer <see cref="GodotNode.TryAsControl"/>, which consults
    /// <see cref="Classifier"/> first.
    /// </remarks>
    public GodotControl ControlUnchecked(NativePtr address)
    {
        EnsureCurrent();
        return new GodotControl(this, address);
    }

    /// <summary>Takes a <c>Label</c> handle without a type check; see <see cref="ControlUnchecked"/>.</summary>
    public GodotLabel LabelUnchecked(NativePtr address)
    {
        EnsureCurrent();
        return new GodotLabel(this, address);
    }

    /// <summary>Takes a <c>RichTextLabel</c> handle without a type check; see <see cref="ControlUnchecked"/>.</summary>
    public GodotRichTextLabel RichTextLabelUnchecked(NativePtr address)
    {
        EnsureCurrent();
        return new GodotRichTextLabel(this, address);
    }

    /// <summary>Opens a scene view rooted at <paramref name="root"/>.</summary>
    public GodotScene SceneFrom(NativePtr root) => new(Node(root));

    /// <summary>Classifies a node through <see cref="Classifier"/>.</summary>
    public GodotNodeClass Classify(NativePtr address)
    {
        EnsureCurrent();
        return Classifier.Classify(this, address);
    }

    /// <summary>
    /// <see langword="true"/> only when the node is positively identified as a <c>Control</c>.
    /// <see cref="GodotNodeClass.Unknown"/> counts as no — see that enum for why guessing yes is the
    /// expensive mistake.
    /// </summary>
    public bool IsControl(NativePtr address) => Classify(address) == GodotNodeClass.Control;

    /// <inheritdoc/>
    public override string ToString() => $"SceneEpoch #{Id} ({(HasEnded ? "ended" : "current")}, {Profile})";
}
