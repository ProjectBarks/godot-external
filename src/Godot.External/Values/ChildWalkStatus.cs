namespace Godot.External.Values;

/// <summary>
/// Outcome of an intrusive child-list walk. Anything other than <see cref="Complete"/> means the
/// returned list is a partial or suspect view and must not be treated as the node's children.
/// </summary>
/// <remarks>
/// This enum exists because of docs/analysis.md §12.4e. Under structural mutation a walk can
/// terminate early <b>silently</b> — no exception, no failed read, every pointer plausible — and it
/// was observed live returning 2296 nodes where the reference returned 2306. Read-level retry
/// cannot catch that class of failure, so the walk reports structural suspicion of its own.
/// Internal alongside its producer; see <see cref="ChildWalkResult"/>.
/// </remarks>
internal enum ChildWalkStatus
{
    /// <summary>Walk reached a null <c>next</c> pointer within bounds. The list is complete as sampled.</summary>
    Complete = 0,

    /// <summary>A remote read failed. Transient in the §4.8 sense; retrying is reasonable.</summary>
    ReadFailed = 1,

    /// <summary>A link node was visited twice — the list looped, so the tail is not trustworthy.</summary>
    CycleDetected = 2,

    /// <summary>The walk hit its bound without terminating. Either a huge list or a corrupt chain.</summary>
    LimitExceeded = 3,

    /// <summary>
    /// A link looked wrong — misaligned pointer or a null child payload — which is what a
    /// mid-splice sample tends to look like.
    /// </summary>
    SuspectLink = 4,

    /// <summary>
    /// Two consecutive traversals disagreed. This is the §12.4e tearing signal, and the only way to
    /// see it at all: caller should re-sample or reuse the last good scene epoch.
    /// </summary>
    Unstable = 5,
}
