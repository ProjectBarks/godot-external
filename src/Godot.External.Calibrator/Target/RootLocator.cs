using Godot.External.Calibrator.Calibration;
using LiveClr.Memory;

namespace Godot.External.Calibrator.Target;

/// <summary>A located walk root, and the anchor pair that located it.</summary>
public sealed record RootLocation(ulong Root, NodeAnchor Anchor, int StringNameDataToBuffer, string How);

/// <summary>
/// One slot that decoded to a wanted name, with the <c>_Data</c> → buffer distance that made it
/// decode.
/// </summary>
/// <remarks>
/// The distance belongs to the <em>slot</em>, not to the name. A live target holds several
/// structures that decode to the same name at different distances — the real
/// <c>StringName::_Data</c> at 8, and unrelated tables that happen to reach the same character
/// buffer at 24 or 32. Collapsing them onto one value per name made root location depend on hash
/// ordering, which is how a calibrator ends up succeeding one run in five on an unchanging target.
/// </remarks>
public sealed record NameSlot(ulong Address, int DataToBuffer);

/// <summary>
/// Finds the scene's walk root with no pointer from anyone: a name scan, then pointer identity.
/// </summary>
/// <remarks>
/// <para>
/// Four steps, each of which narrows by identity rather than by guessing:
/// </para>
/// <list type="number">
/// <item>find the UTF-32 characters of two names the harness stated — the walk root, and one node
/// its own anchor paths reveal to be a child of the walk root;</item>
/// <item>find the slots pointing at those character buffers: each is a <c>StringName::_Data</c> plus
/// some unknown distance <c>k</c>;</item>
/// <item>find the slots pointing at those <c>_Data</c> values: each is <c>node + nameOffset</c> for
/// an unknown <c>nameOffset</c>;</item>
/// <item>solve <c>nameOffset</c> and <c>parentOffset</c> simultaneously from the one relation that
/// must hold between them — the child's node contains a pointer to the parent's node
/// (<see cref="StructuralCalibrator.SolveNameAndParent"/>).</item>
/// </list>
/// <para>
/// Every candidate that survives is then required to reproduce the entire authored scene before it
/// is returned, so a coincidence in any of the four steps is discarded rather than believed.
/// </para>
/// </remarks>
public sealed class RootLocator(IMemoryReader reader, RegionScanner scanner)
{
    /// <summary>Cap on hits kept per scan; a name occurring more often than this is not a useful anchor.</summary>
    public const int MaxHits = 512;

    /// <summary>Largest node the solver will consider, in bytes.</summary>
    public const int MaxNodeSize = 0x1000;

    private readonly IMemoryReader _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    private readonly RegionScanner _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));

    /// <summary>Locates the walk root, or explains what stopped it.</summary>
    public RootLocation? Locate(
        string rootName,
        IReadOnlyList<string> childNames,
        IReadOnlyCollection<string> allNames,
        int expectedNodeCount,
        ICollection<string> notes)
    {
        ArgumentNullException.ThrowIfNull(rootName);
        ArgumentNullException.ThrowIfNull(childNames);
        ArgumentNullException.ThrowIfNull(notes);

        if (childNames.Count == 0)
        {
            notes.Add("root scan: the request revealed no child of the walk root, and one known parent/child pair "
                    + "is the minimum the pointer-identity solve needs.");
            return null;
        }

        IReadOnlyList<NameSlot> rootSlots = NameSlots(rootName);
        if (rootSlots.Count == 0)
        {
            notes.Add($"root scan: found no StringName slot for \"{rootName}\" anywhere in the target's private "
                    + "committed memory.");
            return null;
        }

        StructuralCalibrator structural = new(_reader);

        foreach (string childName in childNames)
        {
            IReadOnlyList<NameSlot> childSlots = NameSlots(childName);

            foreach (NameSlot rootSlot in rootSlots)
            {
                // Pair only slots that agree on the _Data -> buffer distance. That distance is a
                // property of the engine's StringName layout, so the real slots share it and a decoy
                // that reached the same characters by a different route does not.
                foreach (NameSlot childSlot in childSlots.Where(s => s.DataToBuffer == rootSlot.DataToBuffer))
                {
                    foreach (NodeAnchor anchor in structural.SolveNameAndParent(rootSlot.Address, childSlot.Address, MaxNodeSize))
                    {
                        ulong root = rootSlot.Address - (ulong)anchor.NameOffset;
                        IReadOnlyList<CandidateLayout> layouts = NodeLayoutSolver.Solve(
                            _reader, root, rootName, childNames, allNames, expectedNodeCount);

                        if (layouts.Count > 0)
                        {
                            return new RootLocation(
                                root,
                                anchor,
                                rootSlot.DataToBuffer,
                                $"UTF-32 scan for \"{rootName}\" and \"{childName}\", then pointer identity");
                        }
                    }
                }
            }
        }

        notes.Add("root scan: name slots were found, but no (nameOffset, parentOffset) solution reproduced the "
                + "authored scene. The scan may have been truncated, or this build's StringName is not "
                + "CowData<char32_t>.");
        return null;
    }

    private IReadOnlyList<NameSlot> NameSlots(string name)
    {
        IReadOnlyList<ulong> buffers = _scanner.FindBytes(GodotText.Utf32Needle(name), MaxHits);
        if (buffers.Count == 0)
        {
            return [];
        }

        IReadOnlyDictionary<ulong, List<ulong>> dataSlots = _scanner.FindPointersTo(buffers, MaxHits);
        if (dataSlots.Count == 0)
        {
            return [];
        }

        // A slot pointing at the character buffer sits at _Data + k for some small k, and several
        // distinct k can be live at once. Each candidate _Data address therefore carries its OWN k
        // through to the end; one _Data may even be reachable at two distances, so this is a list
        // per address rather than a single value.
        Dictionary<ulong, List<int>> dataCandidates = [];
        foreach (List<ulong> slots in dataSlots.Values)
        {
            foreach (ulong slot in slots)
            {
                for (int k = 0; k <= 0x20; k += 8)
                {
                    if (slot < (ulong)k)
                    {
                        continue;
                    }

                    ulong data = slot - (ulong)k;
                    if (!dataCandidates.TryGetValue(data, out List<int>? distances))
                    {
                        distances = [];
                        dataCandidates[data] = distances;
                    }

                    if (!distances.Contains(k))
                    {
                        distances.Add(k);
                    }
                }
            }
        }

        IReadOnlyDictionary<ulong, List<ulong>> nameSlots = _scanner.FindPointersTo(dataCandidates.Keys, MaxHits);
        List<NameSlot> located = [];
        foreach ((ulong data, List<ulong> slots) in nameSlots)
        {
            foreach (ulong slot in slots)
            {
                foreach (int k in dataCandidates[data])
                {
                    if (GodotText.TryReadStringName(_reader, slot, k, out string decoded) && decoded == name)
                    {
                        located.Add(new NameSlot(slot, k));
                    }
                }
            }
        }

        // Dictionary enumeration order is not part of the answer. Ordering here makes the search
        // deterministic on an unchanging target, which is the property this whole method lost by
        // reducing every slot to one shared distance.
        return [.. located.DistinctBy(s => (s.Address, s.DataToBuffer)).OrderBy(s => s.Address).ThenBy(s => s.DataToBuffer)];
    }
}
