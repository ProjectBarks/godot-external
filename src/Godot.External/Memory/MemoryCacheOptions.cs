namespace Godot.External.Memory;

/// <summary>Which caching shape a <see cref="MemorySnapshot"/> installs.</summary>
public enum MemoryCacheMode
{
    /// <summary>
    /// No cache. Reads go straight to the target, one syscall each — the library's default and the
    /// behaviour of every API that does not open a snapshot.
    /// </summary>
    None = 0,

    /// <summary>
    /// Page-granular, the shape LiveClr's <c>PageCache</c> uses: aligned blocks of
    /// <see cref="MemoryCacheOptions.PageSize"/> bytes, fetched once each. <b>The measured winner</b>
    /// — see <see cref="Span"/> for what it beat and by how much.
    /// </summary>
    Page,

    /// <summary>
    /// Object-granular: on first touch of a registered Godot object, fetch the whole struct in one
    /// read. Reads that fall outside any registered object go to the target directly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Measured, and it loses.</b> On Slay the Spire 2 (1,562 nodes, <c>bench/README.md</c>) the
    /// object span was never the fastest variant and never had the lowest amplification. Against
    /// <see cref="Page"/> with a 256-byte block, on a full tree walk: 0.285x the syscalls of the
    /// uncached path against 0.301x, but 16.5x amplification against 8.0x, and 11.3 ms of wall time
    /// against 7.6 ms.
    /// </para>
    /// <para>
    /// The reason is that the fields this library reads on a node are not spread across its struct —
    /// they cluster. A tree walk touches <c>0x148</c> and <c>0x1c0</c>; geometry touches
    /// <c>0x370</c> and <c>0x470..0x4c8</c>. A small aligned block adapts to whichever clusters a
    /// workload actually uses; a 1,224-byte span always fetches all of them, including the ones
    /// nobody asked for. Kept because it is the evidence for that conclusion, and because a caller
    /// who really does read most of a node might still want it.
    /// </para>
    /// </remarks>
    Span,

    /// <summary>
    /// <see cref="Span"/> for node structs, <see cref="Page"/> for everything else — link nodes,
    /// <c>StringName::_Data</c>, and character buffers, none of which are objects this library knows
    /// the extent of.
    /// </summary>
    /// <remarks>
    /// Dominated by both of its halves on every measured workload: the span policy stops the page
    /// policy from coalescing neighbouring nodes into one fetch, and the page policy stops the span
    /// policy from keeping amplification down. It is here as the measurement that ruled it out.
    /// </remarks>
    Hybrid,
}

/// <summary>How a <see cref="MemorySnapshot"/> is configured. All fields have defensible defaults.</summary>
public sealed record MemoryCacheOptions
{
    /// <summary>The uncached configuration, for A/B measurement against a cached one.</summary>
    public static MemoryCacheOptions None { get; } = new() { Mode = MemoryCacheMode.None };

    /// <summary>Cache shape. Page-granular, because that is what measured best; see <see cref="MemoryCacheMode"/>.</summary>
    public MemoryCacheMode Mode { get; init; } = MemoryCacheMode.Page;

    /// <summary>
    /// Block size for <see cref="MemoryCacheMode.Page"/> and for the hybrid's fallback. Power of two,
    /// 8 bytes to 1 MiB.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>512, not the 4,096 LiveClr's <c>PageCache</c> defaults to.</b> That default is right for
    /// LiveClr, where a block is a page of the CLR's own tightly packed structures; it is wrong here,
    /// and the measurement says why. Slay the Spire 2's scene tree puts <b>1.76 nodes in a 4 KiB
    /// page</b> — a Godot <c>Control</c> is about 1.3 KB and Godot's allocator does not lay siblings
    /// out in walk order — so a 4 KiB fetch buys not quite two nodes and reads 43.8 bytes for every
    /// byte the tree walk actually used. At 512 bytes that falls to 13.2, and at 256 to 8.0, for a
    /// wall-time cost of about 2 ms on a 1,562-node walk.
    /// </para>
    /// <para>
    /// 512 is the knee: below it the syscall count climbs faster than the amplification falls; above
    /// it the reverse. <c>bench/README.md</c> has the full sweep, and moving along it is a one-line
    /// change here.
    /// </para>
    /// </remarks>
    public int PageSize { get; init; } = 512;

    /// <summary>
    /// Object span in bytes for <see cref="MemoryCacheMode.Span"/> and <see cref="MemoryCacheMode.Hybrid"/>.
    /// Zero means "derive from the profile" — the highest offset the offset table can address plus
    /// that field's width, which is the largest read this library will ever issue against a node.
    /// </summary>
    public int SpanBytes { get; init; }

    /// <summary>
    /// Whether the derived span covers <c>Label::text</c> and <c>RichTextLabel::text</c>. Those sit
    /// far past the geometry fields (<c>0x800</c> and <c>0xa78</c> on the validated release profile,
    /// against <c>0x4c0</c> for <c>size_cache</c>), so including them roughly doubles the span for
    /// every node in the tree in order to serve the minority that are labels. Off by default; a text-
    /// heavy overlay should measure it both ways.
    /// </summary>
    public bool SpanIncludesText { get; init; }

    /// <summary>
    /// How long a snapshot may live before its reads are counted as stale. Exceeding it does
    /// <b>not</b> invalidate anything — silently refreshing mid-traversal would swap the image out
    /// from under a walk, which is the failure the snapshot exists to prevent. It sets
    /// <see cref="MemorySnapshot.IsStale"/> and increments
    /// <see cref="CacheStatistics.StaleReads"/> so a snapshot being held across polls is visible
    /// rather than merely wrong.
    /// </summary>
    /// <remarks>
    /// The default is a quarter of a second: the overlay polls at 4 Hz (docs/analysis.md), so a
    /// snapshot older than one poll interval is being reused across frames.
    /// </remarks>
    public TimeSpan MaxAge { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Records every address read and counts re-reads of one. This is the exact detector for the
    /// mistake in <see cref="CacheStatistics.RepeatedReads"/>: a caller that reads an address twice
    /// to see whether it changed, inside a snapshot, will never see it change.
    /// </summary>
    /// <remarks>
    /// Off by default because it costs a hash-set insert per read and a snapshot-lifetime set of
    /// every address touched — real cost on a 2,300-node walk. Turn it on in tests and in
    /// development; the always-on <see cref="CacheStatistics.AgreeTwiceSuppressed"/> counter catches
    /// the one case inside this library for free.
    /// </remarks>
    public bool DetectRepeatedReads { get; init; }
}
