using Godot.External.Abi;

namespace Godot.External.Memory;

/// <summary>
/// Lets read-path code ask an arbitrary <see cref="IByteSource"/> about caching without a type test
/// of its own, and without <see cref="IByteSource"/> growing a third member.
/// </summary>
/// <remarks>
/// Every method here is a no-op on an uncached source, so the call sites read the same whether or
/// not a snapshot is open. That is the property that keeps caching genuinely optional: there is one
/// read path, not two.
/// </remarks>
internal static class ByteSourceCoherence
{
    /// <summary>
    /// <see langword="true"/> when re-reading an address is guaranteed to return the same bytes — so
    /// a check that works by re-reading cannot detect anything.
    /// </summary>
    public static bool IsCoherent(this IByteSource source)
        => source is ICoherentByteSource { IsCoherent: true };

    /// <summary>
    /// Tells a span-granular cache that <paramref name="baseAddress"/> is a Godot object base.
    /// Records an address; performs no read.
    /// </summary>
    public static void RegisterObject(this IByteSource source, ulong baseAddress)
    {
        if (source is ICoherentByteSource coherent)
        {
            coherent.RegisterObject(baseAddress);
        }
    }

    /// <summary>
    /// Records that a temporal check was skipped because <see cref="IsCoherent"/> made it vacuous.
    /// </summary>
    public static void NoteAgreeTwiceSuppressed(this IByteSource source)
    {
        if (source is ICoherentByteSource coherent)
        {
            coherent.NoteAgreeTwiceSuppressed();
        }
    }
}
