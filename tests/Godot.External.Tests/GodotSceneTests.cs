using Godot.External.Bridge;
using Godot.External.Objects;
using Godot.External.Scene;
using Godot.External.Values;

namespace Godot.External.Tests;

/// <summary>
/// Tree traversal, and the structural-suspicion signal it must not swallow (docs/analysis.md
/// §12.3b, §12.4e).
/// </summary>
public class GodotSceneTests
{
    private static (SyntheticScene Scene, ulong Root) BuildMenuTree()
    {
        SyntheticScene scene = new();

        ulong root = scene.NewNode("Game");
        ulong menu = scene.NewNode("MainMenu", size: (1920f, 1080f));
        ulong bg = scene.NewNode("MainMenuBg", size: (1920f, 1080f));
        ulong buttons = scene.NewNode("MainMenuTextButtons", size: (269f, 450f), position: (642f, 609f));
        ulong continueButton = scene.NewNode("ContinueButton", size: (200f, 50f), position: (34f, 50f));

        scene.SetChildren(root, menu);
        scene.SetChildren(menu, bg, buttons);
        scene.SetChildren(buttons, continueButton);

        return (scene, root);
    }

    [Fact]
    public void Walk_VisitsTheWholeTree_BreadthFirst()
    {
        (SyntheticScene scene, ulong root) = BuildMenuTree();
        using SceneEpoch epoch = scene.BeginEpoch();

        TreeWalkResult result = epoch.SceneFrom(new NativePtr(root)).Walk();

        Assert.True(result.IsComplete);
        Assert.Equal(ChildWalkStatus.Complete, result.WorstStatus);
        Assert.Empty(result.SuspectNodes);
        Assert.Equal(
            new[] { "Game", "MainMenu", "MainMenuBg", "MainMenuTextButtons", "ContinueButton" },
            result.Nodes.Select(n => n.Name).ToArray());
    }

    [Fact]
    public void ParentAndChildren_AgreeInBothDirections()
    {
        (SyntheticScene scene, ulong root) = BuildMenuTree();
        using SceneEpoch epoch = scene.BeginEpoch();

        GodotNode rootNode = epoch.Node(new NativePtr(root));

        Assert.True(rootNode.TryGetParent(out GodotNode? noParent));
        Assert.Null(noParent); // a root: read succeeded, there is simply no parent

        NodeChildren children = rootNode.GetChildren();
        Assert.True(children.IsComplete);
        GodotNode menu = Assert.Single(children.Nodes);

        Assert.True(menu.TryGetParent(out GodotNode? backToRoot));
        Assert.Equal(rootNode, backToRoot);

        Assert.Equal(
            new[] { "MainMenuBg", "MainMenuTextButtons" },
            menu.GetChildren().Nodes.Select(n => n.Name).ToArray());
    }

    [Fact]
    public void Ancestors_WalkUpToTheRoot()
    {
        (SyntheticScene scene, ulong root) = BuildMenuTree();
        using SceneEpoch epoch = scene.BeginEpoch();

        GodotScene tree = epoch.SceneFrom(new NativePtr(root));
        Assert.True(tree.TryFindByName("ContinueButton", out GodotNode? button));

        Assert.Equal(
            new[] { "MainMenuTextButtons", "MainMenu", "Game" },
            button.Ancestors().Select(n => n.Name).ToArray());
    }

    [Fact]
    public void TryFindByName_MissIsNotAnError()
    {
        (SyntheticScene scene, ulong root) = BuildMenuTree();
        using SceneEpoch epoch = scene.BeginEpoch();

        Assert.False(epoch.SceneFrom(new NativePtr(root)).TryFindByName("NotHere", out GodotNode? found));
        Assert.Null(found);
    }

    [Fact]
    public void TornChildList_SurfacesAsUnstable_RatherThanAShortList()
    {
        // §12.4e, reproduced: two traversals of the same list disagree because the tree was spliced
        // between them. Every read succeeds and every pointer is plausible — the only evidence is
        // that the sequences differ.
        SyntheticScene scene = new();
        ulong root = scene.NewNode("Game");
        ulong a = scene.NewNode("A");
        ulong b = scene.NewNode("B");
        ulong c = scene.NewNode("C");
        ulong[] links = scene.SetChildren(root, a, b, c);

        ulong headField = root + (ulong)scene.Offsets.NodeChildListHead;
        int headReads = 0;
        scene.Source.OnRead = (address, _) =>
        {
            if (address != headField)
            {
                return;
            }

            headReads++;
            if (headReads == 2)
            {
                // Splice C out just as the confirming traversal starts.
                scene.Source.WritePointer(links[1] + (ulong)scene.Offsets.ChildLinkNext, 0);
            }
        };

        using SceneEpoch epoch = scene.BeginEpoch();
        NodeChildren children = epoch.Node(new NativePtr(root)).GetChildren();

        Assert.Equal(ChildWalkStatus.Unstable, children.Status);
        Assert.False(children.IsComplete);
        Assert.True(children.LooksTruncatedOrLooped);
        Assert.Equal(2, headReads); // attempts: 2 means one walk and one confirmation, no retry
    }

    [Fact]
    public void ExtraAttempt_RecoversFromASingleSplice()
    {
        // §12.4e's recommendation is agree-twice; a third attempt is the retry that turns a
        // one-off splice into a good sample instead of a discarded one.
        SyntheticScene scene = new();
        ulong root = scene.NewNode("Game");
        ulong[] links = scene.SetChildren(root, scene.NewNode("A"), scene.NewNode("B"), scene.NewNode("C"));

        ulong headField = root + (ulong)scene.Offsets.NodeChildListHead;
        int headReads = 0;
        scene.Source.OnRead = (address, _) =>
        {
            if (address == headField && ++headReads == 2)
            {
                scene.Source.WritePointer(links[1] + (ulong)scene.Offsets.ChildLinkNext, 0);
            }
        };

        using SceneEpoch epoch = scene.BeginEpoch();
        NodeChildren children = epoch.Node(new NativePtr(root)).GetChildren(attempts: 3);

        Assert.Equal(ChildWalkStatus.Complete, children.Status);
        Assert.Equal(new[] { "A", "B" }, children.Nodes.Select(n => n.Name).ToArray());
    }

    [Fact]
    public void Walk_ReportsTheSuspectNode_NotJustAWorseTotal()
    {
        SyntheticScene scene = new();
        ulong root = scene.NewNode("Game");
        ulong healthy = scene.NewNode("HpBarContainer", size: (210f, 16f));
        ulong broken = scene.NewNode("Intents", size: (1000f, 40f));
        scene.SetChildren(root, healthy, broken);

        // A misaligned link is what a mid-splice sample looks like.
        ulong[] links = scene.SetChildren(broken, scene.NewNode("Intent", size: (64f, 64f)));
        scene.Source.WritePointer(broken + (ulong)scene.Offsets.NodeChildListHead, links[0] + 3);

        using SceneEpoch epoch = scene.BeginEpoch();
        TreeWalkResult result = epoch.SceneFrom(new NativePtr(root)).Walk();

        Assert.False(result.IsComplete);
        Assert.Equal(ChildWalkStatus.SuspectLink, result.WorstStatus);
        Assert.Equal(new NativePtr(broken), Assert.Single(result.SuspectNodes));

        // The healthy part of the tree is still returned — callers may use it, knowingly.
        Assert.Contains(result.Nodes, n => n.Name == "HpBarContainer");
    }

    [Fact]
    public void CyclicTree_TerminatesAndDoesNotRevisit()
    {
        // A freed-and-reused allocation (§8.8) can produce a child pointer back into the tree.
        SyntheticScene scene = new();
        ulong root = scene.NewNode("Game");
        ulong child = scene.NewNode("Loop");
        scene.SetChildren(root, child);
        scene.SetChildren(child, root);

        using SceneEpoch epoch = scene.BeginEpoch();
        TreeWalkResult result = epoch.SceneFrom(new NativePtr(root)).Walk();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void Walk_HonoursItsNodeBound()
    {
        (SyntheticScene scene, ulong root) = BuildMenuTree();
        using SceneEpoch epoch = scene.BeginEpoch();

        TreeWalkResult result = epoch.SceneFrom(new NativePtr(root)).Walk(maxNodes: 3);

        Assert.True(result.HitNodeLimit);
        Assert.False(result.IsComplete);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void UnreadableChildHead_IsReportedAsAFailedRead()
    {
        SyntheticScene scene = new();
        ulong root = scene.NewNode("Game");
        scene.Source.Unmap(root + (ulong)scene.Offsets.NodeChildListHead, 8);

        using SceneEpoch epoch = scene.BeginEpoch();
        NodeChildren children = epoch.Node(new NativePtr(root)).GetChildren();

        Assert.Equal(ChildWalkStatus.ReadFailed, children.Status);
        Assert.Empty(children.Nodes);
    }
}
