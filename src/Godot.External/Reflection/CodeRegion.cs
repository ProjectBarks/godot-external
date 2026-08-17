namespace Godot.External.Reflection;

/// <summary>
/// A half-open address range holding executable code — normally the main module's <c>.text</c>.
/// </summary>
/// <param name="Start">First address in the range.</param>
/// <param name="End">One past the last address.</param>
/// <remarks>
/// Exists so <see cref="MethodBindProbe"/> can tell a code pointer from a heap pointer without this
/// module growing a PE parser. Supplying a range wider than the real <c>.text</c> weakens the check
/// (more false candidates, hence more refusals) rather than producing wrong answers.
/// </remarks>
internal readonly record struct CodeRegion(ulong Start, ulong End)
{
    /// <summary><see langword="true"/> when the range is empty or inverted, i.e. unusable.</summary>
    public bool IsEmpty => End <= Start;

    /// <summary><see langword="true"/> when <paramref name="address"/> falls inside the range.</summary>
    public bool Contains(ulong address) => address >= Start && address < End;
}
