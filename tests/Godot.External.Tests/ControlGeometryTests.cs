using Godot.External.Abi;
using Godot.External.Values;

namespace Godot.External.Tests;

/// <summary>
/// docs/analysis.md §4.6/§12.3: <c>globalPosition</c> is a cached field that goes stale, so global
/// position must be composed from local positions up the parent chain — and §12.4c: reading a
/// <c>Control</c> field off a non-<c>Control</c> succeeds and returns garbage.
/// </summary>
public class ControlGeometryTests
{
    private static GodotAbiProfile Profile => GodotAbiProfiles.Godot451Release;

    private const ulong Root = 0x80000;
    private const ulong Middle = 0x81000;
    private const ulong Leaf = 0x82000;

    private static readonly Func<ulong, bool> AllControls = _ => true;

    private static FakeByteSource BuildTree()
    {
        FakeByteSource source = new();
        Control(source, Root, x: 100, y: 10, parent: 0, cachedGlobalX: 100, cachedGlobalY: 10);
        Control(source, Middle, x: 20, y: 5, parent: Root, cachedGlobalX: 0, cachedGlobalY: 0);
        Control(source, Leaf, x: 3, y: 2, parent: Middle, cachedGlobalX: 0, cachedGlobalY: 0);
        return source;
    }

    private static void Control(
        FakeByteSource source,
        ulong address,
        float x,
        float y,
        ulong parent,
        float cachedGlobalX = 0,
        float cachedGlobalY = 0)
    {
        GodotOffsetTable offsets = Profile.Offsets;
        source.WriteSingle(address + (ulong)offsets.ControlPosition, x);
        source.WriteSingle(address + (ulong)offsets.ControlPosition + 4, y);
        source.WriteSingle(address + (ulong)offsets.ControlGlobalPosition, cachedGlobalX);
        source.WriteSingle(address + (ulong)offsets.ControlGlobalPosition + 4, cachedGlobalY);
        source.WritePointer(address + (ulong)offsets.NodeParent, parent);
    }

    [Fact]
    public void ComposedGlobalPosition_SumsLocalPositionsToTheRoot()
    {
        FakeByteSource source = BuildTree();

        Assert.True(ControlGeometry.TryComposeGlobalPosition(
            source, Profile, Leaf, out GodotVector2 global, out int ancestors, AllControls));

        Assert.Equal(new GodotVector2(123, 17), global);
        Assert.Equal(2, ancestors);
    }

    [Fact]
    public void CachedGlobalPosition_IsStale_WhichIsExactlyWhyCompositionExists()
    {
        // §12.3 observed [0,0] from this field for MainMenuTextButtons and ContinueButton while both
        // had real positions. The read succeeds; the value is simply a lie.
        FakeByteSource source = BuildTree();

        Assert.True(ControlGeometry.TryReadCachedGlobalPosition(source, Profile, Leaf, out GodotVector2 cached));
        Assert.Equal(GodotVector2.Zero, cached);

        Assert.True(ControlGeometry.TryComposeGlobalPosition(
            source, Profile, Leaf, out GodotVector2 composed, out _, AllControls));
        Assert.NotEqual(cached, composed);
    }

    [Fact]
    public void NonControlAncestor_IsExcludedByTheGate()
    {
        // The realistic chain: Leaf -> Middle (Controls) -> a plain Node container. Its bytes at the
        // Control::pos_cache offset are whatever happens to live there — here the denormal-ish float
        // §12.4c saw come back from exactly this mistake.
        FakeByteSource source = new();
        Control(source, Leaf, x: 3, y: 2, parent: Middle);
        Control(source, Middle, x: 20, y: 5, parent: Root);
        source.WriteSingle(Root + (ulong)Profile.Offsets.ControlPosition, 2.6e-38f);
        source.WriteSingle(Root + (ulong)Profile.Offsets.ControlPosition + 4, 1.4e-37f);
        source.WritePointer(Root + (ulong)Profile.Offsets.NodeParent, 0);

        Assert.True(ControlGeometry.TryComposeGlobalPosition(
            source,
            Profile,
            Leaf,
            out GodotVector2 global,
            out int ancestors,
            includeAncestor: address => address != Root));

        Assert.Equal(new GodotVector2(23, 7), global);
        Assert.Equal(1, ancestors);
    }

    [Fact]
    public void DenormalGarbage_IsRejected_WhenTheGateLetsANonControlThrough()
    {
        // Backstop for a caller whose type check is wrong. Without it the sum absorbs 2.6e-38 and the
        // method still returns true — the §12.4c trap, silent.
        FakeByteSource source = new();
        Control(source, Leaf, x: 3, y: 2, parent: Root);
        source.WriteSingle(Root + (ulong)Profile.Offsets.ControlPosition, 2.6e-38f);
        source.WriteSingle(Root + (ulong)Profile.Offsets.ControlPosition + 4, 0f);
        source.WritePointer(Root + (ulong)Profile.Offsets.NodeParent, 0);

        Assert.False(ControlGeometry.TryComposeGlobalPosition(
            source, Profile, Leaf, out GodotVector2 global, out _, AllControls));
        Assert.Equal(GodotVector2.Zero, global);
    }

    [Theory]
    [InlineData(0d, true)]
    [InlineData(1920d, true)]
    [InlineData(-516.5d, true)]
    [InlineData(2.6e-38d, false)]
    [InlineData(1e-9d, false)]
    [InlineData(1e12d, false)]
    [InlineData(double.NaN, false)]
    [InlineData(double.PositiveInfinity, false)]
    public void CoordinatePlausibility(double value, bool plausible)
    {
        Assert.Equal(plausible, ControlGeometry.IsPlausibleCoordinate(value));
    }

    [Fact]
    public void MissingGate_Throws_RatherThanWalkingToTheRoot()
    {
        // There is deliberately no walk-to-the-root default: that overload would be the unsafe one.
        FakeByteSource source = BuildTree();

        Assert.Throws<ArgumentNullException>(() =>
            ControlGeometry.TryComposeGlobalPosition(source, Profile, Leaf, out _, out _, null!));
    }

    [Fact]
    public void StartingNodeIsAlsoGated()
    {
        FakeByteSource source = BuildTree();

        Assert.False(ControlGeometry.TryComposeGlobalPosition(
            source, Profile, Leaf, out _, out int ancestors, _ => false));
        Assert.Equal(0, ancestors);
    }

    [Fact]
    public void LoopedParentChain_Fails_RatherThanSummingForever()
    {
        FakeByteSource source = BuildTree();
        source.WritePointer(Root + (ulong)Profile.Offsets.NodeParent, Leaf);

        Assert.False(ControlGeometry.TryComposeGlobalPosition(
            source, Profile, Leaf, out _, out _, AllControls));
    }

    [Fact]
    public void ChainDeeperThanMaxAncestorDepth_IsRefused()
    {
        // A pathological chain is a reused/corrupt pointer, not a scene (§8.8).
        FakeByteSource source = new();
        const ulong Base = 0x100000;
        int length = ControlGeometry.MaxAncestorDepth + 40;

        for (int i = 0; i < length; i++)
        {
            ulong address = Base + (ulong)(i * 0x100);
            ulong parent = i == length - 1 ? 0 : Base + (ulong)((i + 1) * 0x100);
            Control(source, address, x: 1, y: 0, parent: parent);
        }

        Assert.False(ControlGeometry.TryComposeGlobalPosition(
            source, Profile, Base, out _, out _, AllControls));

        // Sanity: a chain just inside the bound still resolves.
        Assert.True(ControlGeometry.TryComposeGlobalPosition(
            source,
            Profile,
            Base + (ulong)((length - 10) * 0x100),
            out GodotVector2 shallow,
            out _,
            AllControls));
        Assert.Equal(new GodotVector2(10, 0), shallow);
    }

    [Fact]
    public void UnreadableAncestor_Fails_RatherThanReturningAPartialSum()
    {
        FakeByteSource source = BuildTree();
        source.Unmap(Root + (ulong)Profile.Offsets.ControlPosition, 8);

        Assert.False(ControlGeometry.TryComposeGlobalPosition(
            source, Profile, Leaf, out GodotVector2 global, out _, AllControls));
        Assert.Equal(GodotVector2.Zero, global);
    }

    [Fact]
    public void Vector2AndOffsets_AreFetchedInOneReadEach()
    {
        // A per-component read can tear between x and y: two plausible halves of different samples,
        // no error to key off (§12.4e's class of failure).
        FakeByteSource source = BuildTree();

        int before = source.ReadCount;
        Assert.True(ControlGeometry.TryReadVector2(source, Profile, Leaf, GodotField.ControlPosition, out _));
        Assert.Equal(1, source.ReadCount - before);

        Span<double> offsets = stackalloc double[4];
        for (int i = 0; i < 4; i++)
        {
            source.WriteSingle(Leaf + (ulong)(Profile.Offsets.ControlOffsets + (i * 4)), i);
        }

        before = source.ReadCount;
        Assert.True(ControlGeometry.TryReadOffsets(source, Profile, Leaf, offsets));
        Assert.Equal(1, source.ReadCount - before);
    }

    [Fact]
    public void OffsetsAndVisible_ReadAtTheValidatedOffsets()
    {
        // BgContainer's live values from §12.3: offset[4] = [-960,-516,1600,684], size [2560,1200].
        FakeByteSource source = new();
        GodotOffsetTable offsets = Profile.Offsets;
        float[] authored = [-960f, -516f, 1600f, 684f];

        for (int i = 0; i < authored.Length; i++)
        {
            source.WriteSingle(Leaf + (ulong)(offsets.ControlOffsets + (i * 4)), authored[i]);
        }

        source.WriteSingle(Leaf + (ulong)offsets.ControlSize, 2560f);
        source.WriteSingle(Leaf + (ulong)offsets.ControlSize + 4, 1200f);
        source.WriteBytes(Leaf + (ulong)offsets.CanvasItemVisible, [1]);

        Span<double> read = stackalloc double[4];
        Assert.True(ControlGeometry.TryReadOffsets(source, Profile, Leaf, read));
        Assert.Equal(new double[] { -960, -516, 1600, 684 }, read.ToArray());

        Assert.True(ControlGeometry.TryReadVector2(source, Profile, Leaf, GodotField.ControlSize, out GodotVector2 size));
        Assert.Equal(new GodotVector2(2560, 1200), size);

        Assert.True(ControlGeometry.TryReadVisible(source, Profile, Leaf, out bool visible));
        Assert.True(visible);
    }

    [Fact]
    public void DoublePrecisionProfile_ReadsEightByteComponents()
    {
        GodotAbiProfile doubled = Profile with { Precision = GodotPrecision.Double };
        FakeByteSource source = new();

        source.WriteDouble(Leaf + (ulong)doubled.Offsets.ControlSize, 613);
        source.WriteDouble(Leaf + (ulong)doubled.Offsets.ControlSize + 8, 227);

        Assert.True(ControlGeometry.TryReadVector2(source, doubled, Leaf, GodotField.ControlSize, out GodotVector2 size));
        Assert.Equal(new GodotVector2(613, 227), size);
    }
}
