namespace Godot.External.Values;

/// <summary>
/// The children found by one child-list walk, together with how much that list can be trusted.
/// </summary>
/// <remarks>
/// <para>
/// The status is returned rather than thrown, per docs/analysis.md §8.8: an overlay's correct
/// response to a suspect read is "reuse the last good snapshot", which is impossible if the reader
/// throws. Callers must branch on <see cref="IsComplete"/>; a bare children list would hide exactly
/// the §12.4e failure this type exists to expose.
/// </para>
/// <para>
/// Internal for now — its only producer is <see cref="ChildListWalk"/>, which is internal because it
/// consumes <see cref="Abi.IByteSource"/>. The public surface is settled when the Scene layer lands.
/// </para>
/// </remarks>
internal sealed class ChildWalkResult
{
    internal ChildWalkResult(IReadOnlyList<ulong> children, ChildWalkStatus status)
    {
        Children = children;
        Status = status;
    }

    /// <summary>Child <c>Node*</c> addresses in list order. Possibly partial — check <see cref="Status"/>.</summary>
    public IReadOnlyList<ulong> Children { get; }

    /// <summary>Why the walk stopped.</summary>
    public ChildWalkStatus Status { get; }

    /// <summary><see langword="true"/> only when the walk terminated normally and nothing looked wrong.</summary>
    public bool IsComplete => Status == ChildWalkStatus.Complete;

    /// <summary>
    /// <see langword="true"/> when the list is known to be short, looped or unstable — i.e. the
    /// caller is holding a quietly wrong answer if it ignores this.
    /// </summary>
    /// <remarks>
    /// Defined as the negation of <see cref="IsComplete"/> on purpose: enumerating the bad statuses
    /// would leave any status added later in a limbo where neither flag is true, which is the same
    /// silent-wrongness this type exists to prevent.
    /// </remarks>
    public bool LooksTruncatedOrLooped => !IsComplete;

    /// <inheritdoc/>
    public override string ToString() => $"{Status} ({Children.Count} children)";
}
