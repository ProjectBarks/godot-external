namespace Godot.External.Scene;

/// <summary>
/// Thrown when a handle is used after its <see cref="SceneEpoch"/> ended.
/// </summary>
/// <remarks>
/// <para>
/// This is the one place the library throws on purpose, and the exception to §8.8's "validation must
/// be inspectable" rule. The distinction is deliberate: a torn read is a <em>runtime condition</em>
/// an overlay must handle by reusing its last good snapshot, whereas using an expired handle is a
/// <em>program defect</em> — the caller cached something the lifetime rules forbid caching. Returning
/// <see langword="false"/> for that would let the defect keep running and produce a wrong overlay
/// instead of a stack trace pointing at the cache.
/// </para>
/// <para>
/// docs/analysis.md §8.8, "Lifetimes — corrected": the CLR GC does not move native Godot pointers,
/// but <b>Godot can free a node and reuse the allocation</b>, so a stale <c>Node*</c> addresses a
/// different, entirely plausible-looking node. Nothing in the bytes reveals the substitution — the
/// name decodes, the geometry is finite, the walk succeeds. Only the epoch knows.
/// </para>
/// </remarks>
internal sealed class SceneEpochExpiredException : InvalidOperationException
{
    internal SceneEpochExpiredException(int epochId)
        : base($"Scene epoch #{epochId} has ended. Native Godot pointers are epoch-scoped "
             + "(docs/analysis.md §8.8): Godot frees nodes and reuses the allocation, so a handle "
             + "from an ended epoch may now address a different node that looks entirely plausible. "
             + "Re-resolve from the current epoch's root instead of caching handles across epochs.")
        => EpochId = epochId;

    /// <summary>Identifier of the epoch that had already ended.</summary>
    public int EpochId { get; }
}
