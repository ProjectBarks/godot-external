using Godot.External.Abi;
using Godot.External.Values;

namespace Godot.External.Tests;

/// <summary>
/// docs/analysis.md §4.6: children hang off an intrusive linked list —
/// <c>next = readPtr(cur + 0)</c>, child = <c>readPtr(cur + 0x18)</c>. §12.4e shows this walk can
/// silently tear under mutation, so the failure signals matter as much as the happy path.
/// </summary>
public class ChildListWalkTests
{
    private const ulong Node = 0x50000;
    private const ulong Link1 = 0x51000;
    private const ulong Link2 = 0x51100;
    private const ulong Link3 = 0x51200;
    private const ulong Child1 = 0x60000;
    private const ulong Child2 = 0x60100;
    private const ulong Child3 = 0x60200;

    private static GodotOffsetTable Offsets => GodotAbiProfiles.Godot451Release.Offsets;

    private static ulong HeadAddress => Node + (ulong)Offsets.NodeChildListHead;

    private static FakeByteSource BuildList()
    {
        FakeByteSource source = new();
        source.WritePointer(HeadAddress, Link1);
        Link(source, Link1, Child1, Link2);
        Link(source, Link2, Child2, Link3);
        Link(source, Link3, Child3, 0);
        return source;
    }

    private static void Link(FakeByteSource source, ulong link, ulong payload, ulong next)
    {
        source.WritePointer(link + (ulong)Offsets.ChildLinkNext, next);
        source.WritePointer(link + (ulong)Offsets.ChildLinkPayload, payload);
    }

    [Fact]
    public void LinearList_WalksInOrder()
    {
        ChildWalkResult result = ChildListWalk.Walk(BuildList(), GodotAbiProfiles.Godot451Release, Node);

        Assert.Equal(ChildWalkStatus.Complete, result.Status);
        Assert.True(result.IsComplete);
        Assert.False(result.LooksTruncatedOrLooped);
        Assert.Equal(new[] { Child1, Child2, Child3 }, result.Children);
    }

    [Fact]
    public void EmptyList_IsCompleteAndEmpty()
    {
        FakeByteSource source = new FakeByteSource().WritePointer(HeadAddress, 0);

        ChildWalkResult result = ChildListWalk.Walk(source, Offsets, Node);

        Assert.Equal(ChildWalkStatus.Complete, result.Status);
        Assert.Empty(result.Children);
    }

    [Fact]
    public void MissingHead_ReportsReadFailure()
    {
        ChildWalkResult result = ChildListWalk.Walk(new FakeByteSource(), Offsets, Node);

        Assert.Equal(ChildWalkStatus.ReadFailed, result.Status);
        Assert.True(result.LooksTruncatedOrLooped);
    }

    [Fact]
    public void CyclicList_IsDetectedRatherThanSpinningForever()
    {
        FakeByteSource source = BuildList();
        source.WritePointer(Link3 + (ulong)Offsets.ChildLinkNext, Link1); // tail points back at head

        ChildWalkResult result = ChildListWalk.Walk(source, Offsets, Node);

        Assert.Equal(ChildWalkStatus.CycleDetected, result.Status);
        Assert.Equal(3, result.Children.Count); // every link visited exactly once
    }

    [Fact]
    public void SelfReferencingLink_IsDetected()
    {
        FakeByteSource source = new();
        source.WritePointer(HeadAddress, Link1);
        Link(source, Link1, Child1, Link1);

        ChildWalkResult result = ChildListWalk.Walk(source, Offsets, Node);

        Assert.Equal(ChildWalkStatus.CycleDetected, result.Status);
        Assert.Single(result.Children);
    }

    [Fact]
    public void WalkIsBounded_WhenTheChainOutrunsTheLimit()
    {
        ChildWalkResult result = ChildListWalk.Walk(BuildList(), Offsets, Node, maxChildren: 2);

        Assert.Equal(ChildWalkStatus.LimitExceeded, result.Status);
        Assert.Equal(2, result.Children.Count);
    }

    [Fact]
    public void TruncatedChain_ReportsReadFailure_NotAShortList()
    {
        // The §12.4e failure mode's benign cousin: the link page is simply gone. The distinction
        // that matters is that the caller is told, rather than handed a plausible short list.
        FakeByteSource source = BuildList();
        source.Unmap(Link3, 0x20);

        ChildWalkResult result = ChildListWalk.Walk(source, Offsets, Node);

        Assert.Equal(ChildWalkStatus.ReadFailed, result.Status);
        Assert.Equal(2, result.Children.Count);
        Assert.True(result.LooksTruncatedOrLooped);
    }

    [Fact]
    public void MisalignedLinkPointer_IsSuspect()
    {
        FakeByteSource source = BuildList();
        source.WritePointer(Link2 + (ulong)Offsets.ChildLinkNext, Link3 + 3);

        ChildWalkResult result = ChildListWalk.Walk(source, Offsets, Node);

        Assert.Equal(ChildWalkStatus.SuspectLink, result.Status);
        Assert.Equal(2, result.Children.Count);
    }

    [Fact]
    public void NullChildPayload_IsSuspect()
    {
        FakeByteSource source = BuildList();
        source.WritePointer(Link2 + (ulong)Offsets.ChildLinkPayload, 0);

        ChildWalkResult result = ChildListWalk.Walk(source, Offsets, Node);

        Assert.Equal(ChildWalkStatus.SuspectLink, result.Status);
        Assert.Single(result.Children);
    }

    [Fact]
    public void WalkStable_AcceptsAStaticTree()
    {
        ChildWalkResult result = ChildListWalk.WalkStable(BuildList(), Offsets, Node);

        Assert.Equal(ChildWalkStatus.Complete, result.Status);
        Assert.Equal(3, result.Children.Count);
    }

    /// <summary>
    /// Splices the list <b>during</b> a traversal, at the moment <c>Link1.next</c> is sampled — the
    /// literal §12.4e t21 scenario — and restores it when the next traversal starts.
    /// </summary>
    private static FakeByteSource BuildMidSpliceTree()
    {
        FakeByteSource source = BuildList();
        bool spliced = false;
        bool restored = false;

        source.OnRead = (address, _) =>
        {
            if (address == Link1 + (ulong)Offsets.ChildLinkNext && !spliced)
            {
                // The read that is about to happen now returns 0 instead of Link2.
                spliced = true;
                source.WritePointer(Link1 + (ulong)Offsets.ChildLinkNext, 0);
            }
            else if (address == HeadAddress && spliced && !restored)
            {
                restored = true;
                source.WritePointer(Link1 + (ulong)Offsets.ChildLinkNext, Link2);
            }
        };

        return source;
    }

    [Fact]
    public void SingleWalk_DuringASplice_ReturnsCompleteButShort_Silently()
    {
        // This is the defect, stated plainly: the walk terminates early, every read succeeds, every
        // pointer is plausible, and the status is Complete. Nothing at read level can see it — which
        // is why WalkStable exists. If this test ever starts failing because Walk detects it, the
        // agree-twice machinery can be revisited.
        ChildWalkResult torn = ChildListWalk.Walk(BuildMidSpliceTree(), Offsets, Node);

        Assert.Equal(ChildWalkStatus.Complete, torn.Status);
        Assert.Equal(new[] { Child1 }, torn.Children); // two children short of the truth
    }

    [Fact]
    public void WalkStable_CatchesAMidTraversalSplice()
    {
        // Same fixture through the structural check: traversal 1 tears (1 child), traversal 2 sees
        // the settled tree (3 children), they disagree, and the caller is told.
        ChildWalkResult result = ChildListWalk.WalkStable(BuildMidSpliceTree(), GodotAbiProfiles.Godot451Release, Node);

        Assert.Equal(ChildWalkStatus.Unstable, result.Status);
        Assert.True(result.LooksTruncatedOrLooped);
    }

    [Fact]
    public void WalkStable_ReportsUnstable_WhenTheTreeIsSplicedBetweenTraversals()
    {
        // Reproduces §12.4e t21: the walk terminates early because a `next` pointer was sampled
        // mid-splice. Every read succeeds and every pointer is plausible, so only comparing two
        // traversals can see it.
        FakeByteSource source = BuildList();

        int headReads = 0;
        source.OnRead = (address, _) =>
        {
            if (address != HeadAddress)
            {
                return;
            }

            headReads++;
            if (headReads == 2)
            {
                // Second traversal now sees a two-element list — silently short.
                source.WritePointer(Link2 + (ulong)Offsets.ChildLinkNext, 0);
            }
        };

        ChildWalkResult result = ChildListWalk.WalkStable(source, Offsets, Node);

        Assert.Equal(ChildWalkStatus.Unstable, result.Status);
        Assert.True(result.LooksTruncatedOrLooped);
    }

    [Fact]
    public void WalkStable_RetriesAndAcceptsOnceTheTreeSettles()
    {
        FakeByteSource source = BuildList();

        int headReads = 0;
        source.OnRead = (address, _) =>
        {
            if (address != HeadAddress)
            {
                return;
            }

            headReads++;
            if (headReads == 2)
            {
                source.WritePointer(Link2 + (ulong)Offsets.ChildLinkNext, 0);
            }
        };

        // Walk 1: three children. Walk 2: two (mutation). Walk 3: two — agrees with walk 2.
        ChildWalkResult result = ChildListWalk.WalkStable(source, Offsets, Node, attempts: 3);

        Assert.Equal(ChildWalkStatus.Complete, result.Status);
        Assert.Equal(new[] { Child1, Child2 }, result.Children);
    }
}
