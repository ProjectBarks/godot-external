using Godot.External.Reflection;

namespace Godot.External.Tests;

/// <summary>
/// Phase 2 of the reflection route: version gating, the <c>MethodBind</c> probe, and the
/// <c>HashMapElement</c> chain walk.
/// </summary>
/// <remarks>
/// <para>
/// <b>These tests prove the walkers, not the premise.</b> Every fixture below is synthetic memory
/// laid out the way <c>hash_map.h</c> and <c>method_bind.h</c> say Godot lays it out; none of it has
/// been read from a running game. That is exactly the confidence level the code claims for itself —
/// see the UNVALIDATED banner on <see cref="ClassDbElementWalk"/> — and it is why nothing here is
/// wired into a profile.
/// </para>
/// </remarks>
public sealed class ClassDbReflectionTests
{
    private static readonly CodeRegion Text = new(0x140001000, 0x144000000);

    // ---- version gate ----------------------------------------------------------------------

    [Theory]
    [InlineData(4, 3)]
    [InlineData(4, 5)]
    public void SupportedWindowsBuildsResolveALayout(int major, int minor)
    {
        Assert.True(GodotReflectionSupport.TryResolve(major, minor, isWindows: true, out _, out string reason));
        Assert.Empty(reason);
    }

    [Fact]
    public void FourSixIsRefusedBecauseThePropertyMapBecomesAnAHashMap()
    {
        Assert.False(GodotReflectionSupport.TryResolve(4, 6, isWindows: true, out _, out string reason));
        Assert.Contains("AHashMap", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void NonWindowsIsRefusedRatherThanAttempted()
    {
        // No TYPED_METHOD_BIND means no code address in the MethodBind at all — the route does not
        // degrade on Linux, it is absent, and saying so is cheaper than mis-decoding.
        Assert.False(GodotReflectionSupport.TryResolve(4, 5, isWindows: false, out _, out string reason));
        Assert.Contains("TYPED_METHOD_BIND", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTwoVersionsGetDifferentLayouts()
    {
        Assert.True(GodotReflectionSupport.TryResolve(4, 3, isWindows: true, out ClassDbLayout old, out _));
        Assert.True(GodotReflectionSupport.TryResolve(4, 5, isWindows: true, out ClassDbLayout current, out _));

        // HashMap shrank 48 -> 40 and StringName::_Data lost cname between these releases; a single
        // shared layout would silently misread one of them.
        Assert.Equal(48, old.HashMapSize);
        Assert.Equal(40, current.HashMapSize);
        Assert.True(old.StringNameHasCompileTimeName);
        Assert.False(current.StringNameHasCompileTimeName);
    }

    // ---- MethodBind probe ------------------------------------------------------------------

    [Fact]
    public void TheProbeFindsTheMethodPointerWithoutKnowingSizeofMethodBind()
    {
        // Release and debug MethodBind differ in size because arg_names is DEBUG_ENABLED-only, so
        // the slot index is exactly what must NOT be hardcoded.
        foreach (int slot in new[] { 4, 5 })
        {
            FakeByteSource memory = new();
            const ulong Bind = 0x200000;

            for (int i = 0; i < 8; i++)
            {
                // vtable into .rdata, then StringName handles and heap pointers: nothing executable.
                memory.WritePointer(Bind + (ulong)(i * 8), 0x150000000 + (ulong)i);
            }

            memory.WritePointer(Bind + (ulong)(slot * 8), 0x14139F520);

            Assert.True(MethodBindProbe.TryFindMethodPointer(
                memory, Bind, Text, out ulong code, out int found, out string reason));
            Assert.Equal(0x14139F520UL, code);
            Assert.Equal(slot, found);
            Assert.Empty(reason);
        }
    }

    [Fact]
    public void TwoCodePointersInTheProbeWindowRefuse()
    {
        FakeByteSource memory = new();
        const ulong Bind = 0x200000;

        for (int i = 0; i < 8; i++)
        {
            memory.WritePointer(Bind + (ulong)(i * 8), 0x150000000 + (ulong)i);
        }

        memory.WritePointer(Bind + (4 * 8), 0x14139F520);
        memory.WritePointer(Bind + (6 * 8), 0x141483BB0);

        // Picking either one would be a coin flip whose loser is a decoded offset for the wrong
        // property — indistinguishable from a right answer downstream.
        Assert.False(MethodBindProbe.TryFindMethodPointer(
            memory, Bind, Text, out ulong code, out _, out string reason));
        Assert.Equal(0UL, code);
        Assert.Contains("both hold code addresses", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void NoCodePointerAtAllRefuses()
    {
        FakeByteSource memory = new();
        const ulong Bind = 0x200000;

        for (int i = 0; i < 8; i++)
        {
            memory.WritePointer(Bind + (ulong)(i * 8), 0x150000000 + (ulong)i);
        }

        Assert.False(MethodBindProbe.TryFindMethodPointer(memory, Bind, Text, out _, out _, out string reason));
        Assert.Contains("not a typed MethodBind", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnreadableBindRefuses()
    {
        // The realistic case: the ClassInfo walk produced a plausible but wrong pointer.
        Assert.False(MethodBindProbe.TryFindMethodPointer(
            new FakeByteSource(), 0x200000, Text, out _, out _, out string reason));
        Assert.Contains("read failed", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AnEmptyExecutableRangeRefuses()
    {
        FakeByteSource memory = new();
        memory.WritePointer(0x200000, 0x14139F520);

        Assert.False(MethodBindProbe.TryFindMethodPointer(
            memory, 0x200000, default, out _, out _, out string reason));
        Assert.Contains("executable range", reason, StringComparison.Ordinal);
    }

    // ---- HashMapElement chain --------------------------------------------------------------

    private static FakeByteSource BuildChain(ulong baseAddress, int count, int stride = 0x40)
    {
        FakeByteSource memory = new();

        for (int i = 0; i < count; i++)
        {
            ulong element = baseAddress + (ulong)(i * stride);
            ulong next = i == count - 1 ? 0 : baseAddress + (ulong)((i + 1) * stride);
            ulong previous = i == 0 ? 0 : baseAddress + (ulong)((i - 1) * stride);

            memory.WritePointer(element + 0, next);
            memory.WritePointer(element + 8, previous);
            memory.WritePointer(element + 16, 0x900000 + (ulong)i);   // key: StringName::_Data*
        }

        return memory;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(6)]
    public void AnySingleElementEnumeratesTheWholeChain(int seedIndex)
    {
        // The whole point of the doubly-linked layout: no map object, no bucket array, no capacity —
        // one element is a complete handle on the container.
        const ulong Base = 0x300000;
        FakeByteSource memory = BuildChain(Base, 7);

        Assert.True(ClassDbElementWalk.TryEnumerate(
            memory,
            Base + (ulong)(seedIndex * 0x40),
            ClassDbLayout.Godot45,
            out IReadOnlyList<ulong> elements,
            out string reason));

        Assert.Empty(reason);
        Assert.Equal(7, elements.Count);
        Assert.Equal(Base, elements[0]);
        Assert.Equal(Base + (6 * 0x40), elements[6]);
    }

    [Fact]
    public void KeysAreReadableFromEveryElement()
    {
        const ulong Base = 0x300000;
        FakeByteSource memory = BuildChain(Base, 3);

        Assert.True(ClassDbElementWalk.TryEnumerate(
            memory, Base, ClassDbLayout.Godot45, out IReadOnlyList<ulong> elements, out _));

        for (int i = 0; i < elements.Count; i++)
        {
            Assert.True(ClassDbElementWalk.TryReadKeyPointer(
                memory, elements[i], ClassDbLayout.Godot45, out ulong key));
            Assert.Equal(0x900000UL + (ulong)i, key);
        }
    }

    [Fact]
    public void ACycleIsReportedRatherThanSpun()
    {
        const ulong Base = 0x300000;
        FakeByteSource memory = BuildChain(Base, 4);
        memory.WritePointer(Base + (3 * 0x40), Base);   // tail.next -> head

        Assert.False(ClassDbElementWalk.TryEnumerate(
            memory, Base, ClassDbLayout.Godot45, out _, out string reason));
        Assert.Contains("cycle", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void AMisalignedLinkIsSuspectNotFollowed()
    {
        const ulong Base = 0x300000;
        FakeByteSource memory = BuildChain(Base, 4);
        memory.WritePointer(Base + 0, Base + 0x41);   // head.next lands off-alignment

        Assert.False(ClassDbElementWalk.TryEnumerate(
            memory, Base, ClassDbLayout.Godot45, out IReadOnlyList<ulong> partial, out string reason));
        Assert.Contains("misaligned", reason, StringComparison.Ordinal);

        // The partial list is still handed back, because "one element then a torn link" is a more
        // useful diagnosis than an empty result.
        Assert.Single(partial);
    }

    [Fact]
    public void ATruncatedChainFailsInsteadOfReturningAShortList()
    {
        // The §12.4e failure mode, in another container: a walk that stops early and says nothing is
        // indistinguishable from a small map.
        const ulong Base = 0x300000;
        FakeByteSource memory = BuildChain(Base, 4);
        memory.Unmap(Base + (2 * 0x40), 8);

        Assert.False(ClassDbElementWalk.TryEnumerate(
            memory, Base, ClassDbLayout.Godot45, out IReadOnlyList<ulong> partial, out string reason));
        Assert.Contains("read failed", reason, StringComparison.Ordinal);
        Assert.Equal(3, partial.Count);
    }

    [Fact]
    public void TheWalkIsBounded()
    {
        const ulong Base = 0x300000;
        FakeByteSource memory = BuildChain(Base, 20);

        Assert.False(ClassDbElementWalk.TryEnumerate(
            memory, Base, ClassDbLayout.Godot45, out IReadOnlyList<ulong> partial, out string reason, 5));
        Assert.Contains("refusing to keep following", reason, StringComparison.Ordinal);
        Assert.Equal(5, partial.Count);
    }

    [Fact]
    public void ANullSeedRefuses()
        => Assert.False(ClassDbElementWalk.TryEnumerate(
            new FakeByteSource(), 0, ClassDbLayout.Godot45, out _, out _));
}
