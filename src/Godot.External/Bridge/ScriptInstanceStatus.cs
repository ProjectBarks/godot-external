namespace Godot.External.Bridge;

/// <summary>
/// Outcome of walking <c>Node* -&gt; ScriptInstance* -&gt; GCHandle -&gt; managed object</c>.
/// </summary>
/// <remarks>
/// <para>
/// The statuses are enumerated rather than collapsed to a bool because the failures mean very
/// different things: <see cref="NoScriptInstance"/> is normal (most nodes in a Godot tree carry no
/// script at all), while <see cref="OwnerMismatch"/> means we are reading something that is not the
/// <c>ScriptInstance</c> of the node we asked about — a wrong offset, a freed-and-reused allocation
/// (§8.8), or a torn read.
/// </para>
/// <para>
/// docs/analysis.md §12.3b is the reason this type is careful: the one failed check out of 112 was
/// treating <c>node + 0x68</c> as the managed object directly. That produced an address, not an
/// error, which is the failure mode this enum exists to prevent recurring.
/// </para>
/// </remarks>
internal enum ScriptInstanceStatus
{
    /// <summary>The full chain resolved and the owner back-reference matched the node.</summary>
    Ok = 0,

    /// <summary>
    /// <c>node + NodeScriptInstance</c> was null. The node has no script attached — the common case
    /// for engine-created nodes, and <b>not</b> an error.
    /// </summary>
    NoScriptInstance = 1,

    /// <summary>A remote read failed. Transient in the §4.8 sense; retrying is reasonable.</summary>
    ReadFailed = 2,

    /// <summary>
    /// <b>The self-check failed.</b> <c>ScriptInstance + ScriptInstanceOwner</c> did not equal the
    /// <c>Node*</c> we started from, so the pointer at <c>NodeScriptInstance</c> is not this node's
    /// <c>ScriptInstance</c>. §4.6 documents this back-reference precisely so that a wrong pointer is
    /// caught here instead of being dereferenced into a plausible-looking managed object.
    /// </summary>
    OwnerMismatch = 3,

    /// <summary>
    /// The <c>ScriptInstance</c>'s GCHandle slot was null. Nothing to dereference; distinct from
    /// <see cref="NoScriptInstance"/> because a <c>ScriptInstance</c> did exist.
    /// </summary>
    NoGcHandle = 4,

    /// <summary>
    /// The GCHandle slot or the object it points at was misaligned or null. Godot's allocator and the
    /// CLR both align object references, so this is a torn or fabricated read rather than an object.
    /// </summary>
    SuspectHandle = 5,

    /// <summary>
    /// The <c>ScriptInstance</c> pointer itself was misaligned — the chain was abandoned before any
    /// dereference.
    /// </summary>
    SuspectScriptInstance = 6,
}
