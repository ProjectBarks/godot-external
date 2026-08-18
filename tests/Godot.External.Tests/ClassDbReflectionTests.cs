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
    [InlineData(4, 4)]
    [InlineData(4, 5)]
    [InlineData(4, 6)]
    public void SupportedWindowsBuildsResolveALayout(int major, int minor)
    {
        Assert.True(GodotReflectionSupport.TryResolve(major, minor, isWindows: true, out _, out string reason));
        Assert.Empty(reason);
    }

    [Fact]
    public void FourSevenIsRefusedBecauseClassInfoNoLongerCarriesTheClassName()
    {
        // 4.6 used to be refused here, on the premise that AHashMap ended the walk. It did not: the
        // map this route reads (method_map) is still a HashMap at 4.6, and AHashMapWalk covers the
        // three that converted. The real boundary is one version further on, where ClassInfo drops
        // `StringName name` and `StringName inherits` into GDType — at which point the walker is not
        // slower or riskier, it is reading two members that no longer exist.
        Assert.False(GodotReflectionSupport.TryResolve(4, 7, isWindows: true, out _, out string reason));
        Assert.Contains("GDType", reason, StringComparison.Ordinal);
    }

    [Fact]
    public void FourFourTakesTheFourThreeLayoutAndNotTheFourFiveOne()
    {
        // docs/analysis.md §15.7 caught this gate sending 4.4 to Godot45, which is wrong in BOTH
        // members: 4.4 has the 48-byte HashMap and still carries cname. It cost nothing only because
        // no caller reaches this code yet, and "costs nothing yet" is not "is not a defect".
        Assert.True(GodotReflectionSupport.TryResolve(4, 4, isWindows: true, out ClassDbLayout layout, out _));
        Assert.Equal(48, layout.HashMapSize);
        Assert.Equal(0x18, layout.HashMapHeadElement);
        Assert.True(layout.StringNameHasCompileTimeName);
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

    [Fact]
    public void TheHashMapSizeAndTheHeadElementOffsetAreNotTheSameNumber()
    {
        // These two were conflated once already, which is how a source-derived 48 and a
        // "measured" 40 stood off against each other (docs/analysis.md §15.3 vs §16.5). Live
        // measurement on 4.3 and 4.5 templates: head_element at +0x18 / +0x10, and the map strides
        // that follow from it are 0x30 / 0x28. Asserting only one of the pair lets the other drift.
        Assert.Equal(0x18, ClassDbLayout.Godot43.HashMapHeadElement);
        Assert.Equal(0x10, ClassDbLayout.Godot45.HashMapHeadElement);

        // The trailing members (tail_element, capacity_index, num_elements) occupy a
        // version-invariant 0x18 after head_element -- which is exactly why a head-relative
        // measurement looks version-independent and says nothing about the size.
        Assert.Equal(ClassDbLayout.Godot43.HashMapSize, ClassDbLayout.Godot43.HashMapHeadElement + 0x18);
        Assert.Equal(ClassDbLayout.Godot45.HashMapSize, ClassDbLayout.Godot45.HashMapHeadElement + 0x18);
    }

    [Fact]
    public void FourSixKeepsTheFourFiveHashMapAndChangesEverythingElseAroundIt()
    {
        // "4.6 carries over from 4.5" was the prior (§15.6). Measured on a running 4.6.3 template it
        // is true of the HashMap and of nothing else, and both halves are asserted here so that
        // aliasing Godot46 onto Godot45 — the cheap thing to do — turns this red.
        Assert.Equal(ClassDbLayout.Godot45.HashMapSize, ClassDbLayout.Godot46.HashMapSize);
        Assert.Equal(ClassDbLayout.Godot45.HashMapHeadElement, ClassDbLayout.Godot46.HashMapHeadElement);

        // ClassInfo gains `const GDType *gdtype`, so every map inside it moves 8 bytes.
        Assert.Equal(0x20, ClassDbLayout.Godot45.ClassInfoMethodMap);
        Assert.Equal(0x28, ClassDbLayout.Godot46.ClassInfoMethodMap);

        // CowData gains a capacity field and a 16-byte-aligned payload, so the element count moves.
        Assert.Equal(8, ClassDbLayout.Godot45.CowDataSizeBackOffset);
        Assert.Equal(0x10, ClassDbLayout.Godot46.CowDataSizeBackOffset);

        Assert.False(ClassDbLayout.Godot45.HasAHashMaps);
        Assert.True(ClassDbLayout.Godot46.HasAHashMaps);
    }

    [Fact]
    public void TheMethodMapHeadOffsetsCancelBetweenFourThreeAndFourSix()
    {
        // The trap this pair exists to name: the measurable quantity is the head_element FIELD's
        // offset inside ClassInfo, and on 4.3 and 4.6 it is the same number — 0x38 — for two
        // completely different reasons. 4.3 has method_map at 0x20 with head_element 0x18 into the
        // map; 4.6 has method_map at 0x28 with head_element 0x10. Reading 0x38 on both and concluding
        // "unchanged" is §16.5's mistake with the operands swapped.
        Assert.Equal(
            ClassDbLayout.Godot43.ClassInfoMethodMap + ClassDbLayout.Godot43.HashMapHeadElement,
            ClassDbLayout.Godot46.ClassInfoMethodMap + ClassDbLayout.Godot46.HashMapHeadElement);
        Assert.NotEqual(ClassDbLayout.Godot43.ClassInfoMethodMap, ClassDbLayout.Godot46.ClassInfoMethodMap);
        Assert.NotEqual(ClassDbLayout.Godot43.HashMapHeadElement, ClassDbLayout.Godot46.HashMapHeadElement);
    }

    // ---- MethodBind probe ------------------------------------------------------------------

    /// <summary>
    /// The two slots <c>MethodBind</c>'s method pointer actually occupies, measured live on stock
    /// export templates: <c>sizeof(MethodBind)</c> is <c>0x48</c> on release and <c>0x58</c> on
    /// debug (<c>arg_names</c> is <c>DEBUG_ENABLED</c>-only), identically on 4.3 and 4.5.
    /// </summary>
    public static TheoryData<int, int> MeasuredMethodSlots => new()
    {
        { 9, 0x48 },    // 4.3-release and 4.5-release
        { 11, 0x58 },   // 4.3-debug and 4.5-debug
    };

    [Theory]
    [MemberData(nameof(MeasuredMethodSlots))]
    public void TheProbeFindsTheMethodPointerWithoutKnowingSizeofMethodBind(int slot, int sizeofMethodBind)
    {
        // This test used to place the pointer at slot 4 or 5 -- inside the old 8-slot window and
        // nowhere near the real layout -- so it passed while the shipped default refused 100% of
        // live probes. It is the §13.11 family: a check with no way to come out other than the way
        // it came out. The slots below are the measured ones, so reverting DefaultProbeSlots to 8
        // now turns this red.
        Assert.Equal(sizeofMethodBind, slot * 8);

        FakeByteSource memory = new();
        const ulong Bind = 0x200000;

        for (int i = 0; i < MethodBindProbe.DefaultProbeSlots + 4; i++)
        {
            // vtable into .rdata, then StringName handles and heap pointers: nothing executable.
            memory.WritePointer(Bind + (ulong)(i * 8), 0x150000000 + (ulong)i);
        }

        memory.WritePointer(Bind + (ulong)(slot * 8), 0x14139F520);

        // No explicit probeSlots: the point is that the SHIPPED DEFAULT reaches the real slot.
        Assert.True(MethodBindProbe.TryFindMethodPointer(
            memory, Bind, Text, out ulong code, out int found, out string reason));
        Assert.Equal(0x14139F520UL, code);
        Assert.Equal(slot, found);
        Assert.Empty(reason);
    }

    [Fact]
    public void TheDefaultWindowReachesTheDebugMethodBindAndNoFurtherThanItNeedsTo()
    {
        // Reaching slot 11 is the requirement; the ceiling is the other half of the bargain,
        // because every extra slot is another chance at a second .text hit, which refuses.
        Assert.True(MethodBindProbe.DefaultProbeSlots >= 12, "the debug method pointer is at slot 11");
        Assert.True(MethodBindProbe.DefaultProbeSlots <= 16, "a wide window invites a spurious second hit");
    }

    [Fact]
    public void TwoCodePointersInTheProbeWindowRefuse()
    {
        FakeByteSource memory = new();
        const ulong Bind = 0x200000;

        for (int i = 0; i < MethodBindProbe.DefaultProbeSlots; i++)
        {
            memory.WritePointer(Bind + (ulong)(i * 8), 0x150000000 + (ulong)i);
        }

        // The two slots the release and debug layouts respectively use: a build whose MethodBind
        // somehow held both would be exactly the coin flip this refusal exists for.
        memory.WritePointer(Bind + (9 * 8), 0x14139F520);
        memory.WritePointer(Bind + (11 * 8), 0x141483BB0);

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

        for (int i = 0; i < MethodBindProbe.DefaultProbeSlots; i++)
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
