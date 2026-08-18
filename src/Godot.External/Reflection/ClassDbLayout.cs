namespace Godot.External.Reflection;

/// <summary>
/// Structure offsets the <c>ClassDB</c> walk needs, per engine version.
/// </summary>
/// <remarks>
/// <para>
/// <b>All of it is now live-measured.</b> <see cref="ElementNext"/>, <see cref="ElementPrevious"/>
/// and <see cref="ElementData"/> come straight from <c>core/templates/hash_map.h</c>, where
/// <c>HashMapElement</c> declares <c>next</c>, <c>prev</c>, then <c>KeyValue data</c> in that order
/// with no virtuals. <see cref="HashMapSize"/>, <see cref="HashMapHeadElement"/> and
/// <see cref="StringNameHasCompileTimeName"/> were read out of running 4.3 and 4.5 export templates
/// (release and debug) by walking every registered <c>ClassInfo</c> — see the provenance note on
/// each.
/// </para>
/// <para>
/// Nothing here may reach a <c>GodotAbiProfile</c>. See <see cref="ClassDbElementWalk"/> for why the
/// whole Phase 2 chain is quarantined.
/// </para>
/// </remarks>
internal sealed record ClassDbLayout
{
    /// <summary>Godot 4.5: 40-byte <c>HashMap</c>, <c>StringName::_Data</c> without <c>cname</c>/<c>idx</c>.</summary>
    public static ClassDbLayout Godot45 { get; } = new()
    {
        HashMapSize = 40,
        HashMapHeadElement = 0x10,
        StringNameHasCompileTimeName = false,
        ClassInfoMethodMap = 0x20,
        CowDataSizeBackOffset = 8,
        HasAHashMaps = false,
    };

    /// <summary>Godot 4.3: 48-byte <c>HashMap</c>, <c>StringName::_Data</c> still carrying <c>cname</c>.</summary>
    public static ClassDbLayout Godot43 { get; } = new()
    {
        HashMapSize = 48,
        HashMapHeadElement = 0x18,
        StringNameHasCompileTimeName = true,
        ClassInfoMethodMap = 0x20,
        CowDataSizeBackOffset = 8,
        HasAHashMaps = false,
    };

    /// <summary>
    /// Godot 4.6: <c>HashMap</c> unchanged from 4.5, but <c>ClassInfo</c> moves and three of its maps
    /// become <c>AHashMap</c>, and <c>CowData</c> grows a capacity field.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every number here was measured on a running 4.6.3-stable export template, release and
    /// debug, and the same probe was run against 4.5 as a control.</b> "4.6 carries over from 4.5"
    /// was the prior, and it is true of <see cref="HashMapSize"/> and only of that.
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <b><c>HashMap</c> is unchanged at 40 / <c>+0x10</c>.</b> Confirmed rather than assumed: the
    /// validated map heads inside a 4.6 <c>ClassInfo</c> stride by <c>0x28</c> where two
    /// <c>HashMap</c>s are adjacent, exactly as on 4.5.
    /// </item>
    /// <item>
    /// <b><c>method_map</c> moves to <c>+0x28</c>.</b> <c>ClassInfo</c> gains
    /// <c>const GDType *gdtype</c> between <c>class_ptr</c> and <c>gdextension</c>
    /// (<c>class_db.h:126</c>), which pushes every map down 8 bytes. §16.5's "<c>method_map</c> is at
    /// <c>+0x20</c> on both 4.3 and 4.5" was true of both versions it named and is false at 4.6.
    /// </item>
    /// <item>
    /// <b><c>CowData</c>'s element count moves from <c>-0x8</c> to <c>-0x10</c>.</b> 4.6 inserts a
    /// capacity field and aligns the payload to <c>Memory::MAX_ALIGN</c>, giving a 32-byte header
    /// instead of 16. Nothing in §15 predicted this and it is what actually breaks on 4.6: a reader
    /// that keeps <c>-0x8</c> decodes no string at all, so a scan for a node name finds nothing and
    /// the failure reads as "this name is not in memory" rather than as a layout change.
    /// </item>
    /// <item>
    /// <b>Three <c>ClassInfo</c> maps become <c>AHashMap</c></b> — <c>constant_map</c>,
    /// <c>signal_map</c> and <c>property_setget</c>. §15.2 named only the third.
    /// <see cref="AHashMapWalk"/> reads them; <see cref="ClassDbElementWalk"/> must not be pointed
    /// at one.
    /// </item>
    /// </list>
    /// </remarks>
    public static ClassDbLayout Godot46 { get; } = new()
    {
        HashMapSize = 40,
        HashMapHeadElement = 0x10,
        StringNameHasCompileTimeName = false,
        ClassInfoMethodMap = 0x28,
        CowDataSizeBackOffset = 0x10,
        HasAHashMaps = true,
    };

    /// <summary><c>HashMapElement::next</c>. From <c>hash_map.h</c>: first member, no vtable.</summary>
    public int ElementNext { get; init; }

    /// <summary><c>HashMapElement::prev</c>, immediately after <c>next</c>.</summary>
    public int ElementPrevious { get; init; } = 8;

    /// <summary><c>HashMapElement::data</c> — the <c>KeyValue</c>, whose key is its first member.</summary>
    public int ElementData { get; init; } = 16;

    /// <summary>
    /// <c>sizeof(HashMap)</c> for this version — 48 on 4.3/4.4, 40 on 4.5+. Only needed by a walker
    /// that strides across maps; the element chain does not use it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Do not confuse this with <see cref="HashMapHeadElement"/>.</b> They are different numbers
    /// about the same struct, and conflating them is what produced a stand-off between
    /// docs/analysis.md §15.3 (source-derived) and §16.5 (claimed live measurement).
    /// </para>
    /// <para>
    /// <b>Measured.</b> Walking all 869 (4.3) / 908 (4.5) registered <c>ClassInfo</c>s and accepting
    /// only offsets where <c>head_element</c>, <c>tail_element</c> and <c>num_elements</c> agree
    /// with a fully traversed chain recovers the map heads at ClassInfo <c>+0x38, +0x68, +0x98,
    /// +0xc8, +0xf8, +0x130, +0x160</c> on 4.3 and <c>+0x30, +0x58, +0x80, +0xa8, +0xd0, +0x100,
    /// +0x128, +0x150</c> on 4.5. The strides are <c>0x30</c> and <c>0x28</c> respectively, with one
    /// 8-byte widening in each where <c>ClassInfo::property_list</c> (a <c>List</c>, one pointer)
    /// sits between <c>signal_map</c> and <c>property_map</c>. So <c>sizeof(HashMap)</c> is
    /// <b>48 on 4.3 and 40 on 4.5</b>, on both release and debug templates.
    /// </para>
    /// <para>
    /// That agrees with the source exactly: 4.3/4.4 declare <c>Allocator element_alloc;</c> as the
    /// first member (an empty class, padded to 8), while 4.5+ write
    /// <c>class HashMap : private Allocator</c> and empty-base-optimization erases it.
    /// </para>
    /// </remarks>
    public int HashMapSize { get; init; }

    /// <summary>
    /// Offset of <c>HashMap::head_element</c> <em>within</em> a <c>HashMap</c> — 0x18 on 4.3/4.4,
    /// 0x10 on 4.5+. This is the number a walker needs to reach a map's first element from the map
    /// itself; <see cref="HashMapSize"/> is not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this property exists at all.</b> §16.5 recorded a live run as measuring
    /// <c>sizeof(HashMap)</c> at 40 bytes on <em>both</em> versions, contradicting §15.3's
    /// source-derived 48→40 boundary at 4.4→4.5. Re-measured, §15.3 is right and §16.5 was
    /// measuring this offset, not the size: §16.5's own "<c>ClassInfo::method_map</c> at +0x30 (4.5)
    /// / +0x38 (4.3)" numbers are the offsets of <c>head_element</c> within <c>ClassInfo</c>, not of
    /// <c>method_map</c>, which sits at <c>ClassInfo+0x20</c> on both. Subtracting a 4.5-shaped
    /// <c>head_element</c> and adding the (version-invariant) 0x18 of trailing members yields 40 on
    /// any version, which is arithmetic rather than measurement. Naming the two quantities
    /// separately is what stops that recurring.
    /// </para>
    /// <para>
    /// The remaining members follow <c>head_element</c> identically on every version:
    /// <c>tail_element</c> at <c>+0x8</c>, <c>capacity_index</c> at <c>+0x10</c>,
    /// <c>num_elements</c> at <c>+0x14</c> — which is why a head-relative measurement looks
    /// version-independent and is not evidence about the size.
    /// </para>
    /// </remarks>
    public int HashMapHeadElement { get; init; }

    /// <summary>
    /// Whether <c>StringName::_Data</c> still carries the <c>cname</c> compile-time
    /// <c>const char *</c> at <c>+0x08</c> (true through 4.4, removed in 4.5, which moves
    /// <c>name</c> from <c>+0x10</c> to <c>+0x08</c>).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>On 4.3 a reader must handle <em>both</em>, and which one is populated depends on how the
    /// name was interned, not on the version.</b> <c>StringName</c>'s <c>StaticCString</c>
    /// constructor sets <c>cname</c> and leaves <c>name</c> empty; the <c>String</c> and
    /// <c>const char *</c> constructors set <c>name</c> and leave <c>cname</c> null.
    /// <c>_Data::get_name()</c> is literally <c>cname ? String(cname) : name</c>.
    /// </para>
    /// <para>
    /// <b>Measured on 4.3, and it cuts both ways within a single walk.</b> The keys of
    /// <c>ClassDB::classes</c> — the class names — have <c>cname</c> <b>null</b> and <c>name</c>
    /// populated, so a reader that only follows <c>name</c> reads them correctly. The keys of
    /// <c>ClassInfo::method_map</c> — the method names — are the other way round: <c>cname</c> holds
    /// the ASCII (<c>"get_text"</c>, <c>"is_visible"</c>) and <c>name</c> is <b>null</b>, so a
    /// reader that only follows <c>name</c> gets an empty string for every method and can never
    /// resolve a bind by name.
    /// </para>
    /// <para>
    /// So §13.7's "a 4.3 reader must check <c>cname</c> first" is right about the mechanism and its
    /// failure is real, and §16.5's "that is backwards" is right only about the class-name keys it
    /// happened to look at. Neither generalises; a 4.3 reader needs <c>cname</c>-then-<c>name</c>,
    /// exactly as <c>get_name()</c> does. On 4.5 there is no <c>cname</c> and <c>name</c> is always
    /// populated, so the question disappears.
    /// </para>
    /// <para>
    /// <b>4.6 measured, not inherited from 4.5.</b> <c>string_name.h</c> still declares
    /// <c>_Data</c> as <c>{ refcount, static_count, String name, … }</c> with no <c>cname</c>, and a
    /// live probe reads all 959 class names and all 49 <c>Label</c> / 92 <c>CanvasItem</c> method
    /// names through <c>name</c> at <c>_Data+0x08</c>, with the <c>cname</c> fallback never firing.
    /// So the 4.5 answer holds at 4.6 — but the <em>reason</em> to check was real: this is the one
    /// place where being wrong costs every method name and no class name, which resolves no bind at
    /// all while looking like a working walk.
    /// </para>
    /// </remarks>
    public bool StringNameHasCompileTimeName { get; init; }

    /// <summary>
    /// Offset of <c>ClassInfo::method_map</c> within a <c>ClassInfo</c> — <c>0x20</c> through 4.5,
    /// <c>0x28</c> from 4.6.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The preceding members are <c>APIType api</c> (padded to 8), <c>inherits_ptr</c> and
    /// <c>class_ptr</c>, then <c>gdextension</c>. 4.6 inserts <c>const GDType *gdtype</c> between
    /// <c>class_ptr</c> and <c>gdextension</c> (<c>class_db.h:126</c>), so everything below moves 8
    /// bytes.
    /// </para>
    /// <para>
    /// <b>Measured, on all three versions, as the offset of the map's <c>head_element</c> field minus
    /// <see cref="HashMapHeadElement"/>:</b> <c>+0x30 − 0x10 = 0x20</c> on 4.5, <c>+0x38 − 0x18 =
    /// 0x20</c> on 4.3, <c>+0x38 − 0x10 = 0x28</c> on 4.6. The two 4.6 quantities that moved —
    /// <c>method_map</c> by <c>+8</c> and <c>head_element</c> by <c>−8</c> relative to 4.3 — happen to
    /// cancel in the <em>head</em> offset, which is exactly the arithmetic coincidence §16.5 was
    /// caught by once. They are recorded separately here so the next reader cannot repeat it.
    /// </para>
    /// </remarks>
    public int ClassInfoMethodMap { get; init; } = 0x20;

    /// <summary>
    /// Distance back from a <c>CowData</c> buffer pointer to its element count — <c>8</c> through
    /// 4.5, <c>0x10</c> from 4.6.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the same quantity as <see cref="Abi.GodotOffsetTable.CowDataSizeBackOffset"/>, which a
    /// calibrated profile carries per target. It is repeated here because the reflection route runs
    /// <em>before</em> any profile exists — it reads class and method names out of <c>ClassDB</c> to
    /// build one — and a name read through the wrong header distance is not a wrong name, it is no
    /// name at all.
    /// </para>
    /// <para>
    /// <b>Measured on live templates:</b> the header in front of the UTF-32 buffer for
    /// <c>"RootHarness"</c> reads <c>[refcount=1][size=12]</c> on 4.5 and
    /// <c>[refcount=1][capacity=13][size=12][pad]</c> on 4.6.3 — <c>cowdata.h</c> at 4.6 declares
    /// <c>REF_COUNT_OFFSET / CAPACITY_OFFSET / SIZE_OFFSET / DATA_OFFSET</c> where 4.5 had three, and
    /// aligns the payload to <c>Memory::MAX_ALIGN</c>. <b>A caller deriving this from the target
    /// should prefer that measurement to this constant</b>; the constant is here so a version-gated
    /// reader is not silently wrong, not so that anyone stops measuring.
    /// </para>
    /// </remarks>
    public int CowDataSizeBackOffset { get; init; } = 8;

    /// <summary>
    /// Whether this version's <c>ClassInfo</c> holds any <c>AHashMap</c>, which
    /// <see cref="ClassDbElementWalk"/> cannot read and <see cref="AHashMapWalk"/> can.
    /// </summary>
    /// <remarks>
    /// <para>
    /// False through 4.5, true from 4.6, where <c>constant_map</c>, <c>signal_map</c> and
    /// <c>property_setget</c> all convert. A walker that assumes every map in a <c>ClassInfo</c> is
    /// intrusive would follow a <c>KeyValue</c>'s first two qwords as if they were <c>next</c> and
    /// <c>prev</c>; on a <c>KeyValue&lt;StringName, int64_t&gt;</c> that is the interned name pointer
    /// and a constant's value, which is not a chain and is not diagnosable as one from the bytes.
    /// </para>
    /// <para>
    /// <b>The control that makes this a measurement.</b> The same structural detector — both storage
    /// pointers non-null and aligned, capacity a power of two at or above 16, size within it, the
    /// index spanning its full capacity — fires on three offsets in every 4.6 <c>ClassInfo</c> and on
    /// <b>none</b> of 4.5's 908. A predicate that matched everywhere would have proved nothing.
    /// </para>
    /// </remarks>
    public bool HasAHashMaps { get; init; }
}
