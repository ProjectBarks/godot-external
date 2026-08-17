using LiveClr.Memory;

namespace Godot.External.Calibrator.Calibration;

/// <summary>
/// Finds the node-level layout — name, child-list head, link <c>next</c> and payload — starting from
/// nothing but the walk root's address and the names the harness supplied.
/// </summary>
/// <remarks>
/// <para>
/// §12.5 derived the child-list head as "the only pointer <c>p</c> where <c>*(p + 0x18)</c> is a
/// known child". That sentence assumes two things this calibrator is not given: which address the
/// known child has, and that the payload sits at <c>0x18</c>. Both become unknowns in the same
/// search here, and the search is closed by the names — a candidate is accepted only when the slot
/// it points through lands on a node whose decoded name is one the harness listed as a child of the
/// root.
/// </para>
/// <para>
/// Nothing about a value or a size is used. The names are identity, not measurement: they say
/// <em>which object</em> was reached, exactly as a pointer comparison would.
/// </para>
/// </remarks>
public static class NodeLayoutSolver
{
    /// <summary>How far into a node to look for pointer-shaped fields.</summary>
    public const int DefaultScanBytes = 0xC00;

    /// <summary>Solves the layout, or returns every partial result for the caller to report.</summary>
    public static IReadOnlyList<CandidateLayout> Solve(
        IMemoryReader reader,
        ulong root,
        string rootName,
        IReadOnlyCollection<string> knownRootChildNames,
        IReadOnlyCollection<string> allNames,
        int expectedNodeCount,
        int scanBytes = DefaultScanBytes)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(rootName);
        ArgumentNullException.ThrowIfNull(knownRootChildNames);
        ArgumentNullException.ThrowIfNull(allNames);

        MemoryWindow window = MemoryWindow.Read(reader, root, scanBytes);
        List<CandidateLayout> solutions = [];

        foreach ((int nameOffset, int dataToBuffer) in NameOffsetCandidates(reader, window, root, rootName, scanBytes))
        {
            foreach ((int head, int payload) in ChildListCandidates(reader, window, knownRootChildNames, nameOffset, dataToBuffer, scanBytes))
            {
                foreach (int next in StructuralCalibrator.LinkOffsetCandidates)
                {
                    if (next == payload)
                    {
                        continue;
                    }

                    // parentOffset is filled in by the pointer-identity pass once the walk exists;
                    // it plays no part in reaching the nodes, so it is left at 0 here.
                    CandidateLayout candidate = new(nameOffset, 0, head, next, payload, dataToBuffer);
                    WalkedScene scene = SceneWalker.Walk(reader, root, candidate, expectedNodeCount + 1);
                    if (SceneWalker.Reproduces(scene, expectedNodeCount, allNames, verifyParents: false)
                        && !solutions.Contains(candidate))
                    {
                        solutions.Add(candidate);
                    }
                }
            }
        }

        return solutions;
    }

    /// <summary>
    /// Slots on the root whose <c>StringName</c> decodes to the root's known name, paired with the
    /// <c>_Data</c> → buffer distance that made it decode.
    /// </summary>
    public static IReadOnlyList<(int NameOffset, int DataToBuffer)> NameOffsetCandidates(
        IMemoryReader reader,
        MemoryWindow window,
        ulong node,
        string expectedName,
        int scanBytes)
    {
        ArgumentNullException.ThrowIfNull(reader);
        ArgumentNullException.ThrowIfNull(window);

        List<(int, int)> candidates = [];
        int limit = Math.Min(scanBytes, window.Length) - 8;
        for (int offset = 0; offset <= limit; offset += 8)
        {
            if (!window.TryPointer(offset, out ulong data) || data == 0 || (data & 7) != 0)
            {
                continue;
            }

            foreach (int k in GodotText.DataToBufferCandidates(reader, node + (ulong)offset, expectedName))
            {
                candidates.Add((offset, k));
            }
        }

        return candidates;
    }

    private static IReadOnlyList<(int Head, int Payload)> ChildListCandidates(
        IMemoryReader reader,
        MemoryWindow window,
        IReadOnlyCollection<string> knownChildNames,
        int nameOffset,
        int dataToBuffer,
        int scanBytes)
    {
        List<(int, int)> candidates = [];
        if (knownChildNames.Count == 0)
        {
            return candidates;
        }

        int limit = Math.Min(scanBytes, window.Length) - 8;
        for (int offset = 0; offset <= limit; offset += 8)
        {
            if (offset == nameOffset || !window.TryPointer(offset, out ulong link) || link == 0 || (link & 7) != 0)
            {
                continue;
            }

            foreach (int payload in StructuralCalibrator.LinkOffsetCandidates)
            {
                if (!reader.TryReadPointer(link + (ulong)payload, out ulong child) || child == 0 || (child & 7) != 0)
                {
                    continue;
                }

                if (GodotText.TryReadStringName(reader, child + (ulong)nameOffset, dataToBuffer, out string name)
                    && knownChildNames.Contains(name))
                {
                    candidates.Add((offset, payload));
                }
            }
        }

        return candidates;
    }
}
