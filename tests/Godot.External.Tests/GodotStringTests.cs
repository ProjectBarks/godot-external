using System.Buffers.Binary;
using Godot.External.Abi;
using Godot.External.Values;

namespace Godot.External.Tests;

/// <summary>
/// Anti-regression for the truncation bug docs/analysis.md §4.6 identifies: scry narrows each
/// <c>char32_t</c> to a byte when building its string. That is invisible for ASCII and destructive
/// for everything else, so the non-ASCII cases here are the point of this file.
/// </summary>
public class GodotStringTests
{
    private const ulong Buffer = 0x3000;

    [Fact]
    public void AsciiText_RoundTrips()
    {
        // One of the strings actually recovered live in §12.3b.
        FakeByteSource source = new FakeByteSource().WriteGodotString(Buffer, "[v0.107.1] (2026.06.18)");

        Assert.True(GodotString.TryRead(source, Buffer, out string value));
        Assert.Equal("[v0.107.1] (2026.06.18)", value);
    }

    // Which of these actually catch the truncation bug is worth being precise about: U+0000..U+00FF
    // survives narrowing to a byte and re-widening, so "café" round-trips even through the broken
    // decoder and is only a general sanity case. The rows above U+00FF are the real anti-regression.
    [Theory]
    [InlineData("café")]                 // Latin-1 — survives truncation; sanity case, not the guard
    [InlineData("日本語のラベル")]           // CJK, well above 0xFF — truncation destroys this
    [InlineData("Ωμέγα")]                // Greek — likewise
    [InlineData("сохранить")]            // Cyrillic — likewise
    [InlineData("emoji 🎮 astral")]       // > U+FFFF: must become a surrogate PAIR
    [InlineData("mixed ascii + 汉字 + 🐉")]
    public void NonAsciiText_RoundTripsExactly(string text)
    {
        FakeByteSource source = new FakeByteSource().WriteGodotString(Buffer, text);

        Assert.True(GodotString.TryRead(source, Buffer, out string value));
        Assert.Equal(text, value);
    }

    [Fact]
    public void AstralCodePoint_BecomesASurrogatePair_NotATruncatedByte()
    {
        // U+1F3AE GAMEPAD. A byte-truncating decoder yields U+00AE ("®"); the correct decode is the
        // surrogate pair D83C DFAE.
        FakeByteSource source = new FakeByteSource().WriteGodotString(Buffer, "\U0001F3AE");

        Assert.True(GodotString.TryRead(source, Buffer, out string value));

        Assert.Equal(2, value.Length);
        Assert.True(char.IsHighSurrogate(value[0]));
        Assert.True(char.IsLowSurrogate(value[1]));
        Assert.Equal(0x1F3AE, char.ConvertToUtf32(value[0], value[1]));
        Assert.NotEqual("®", value);
    }

    [Fact]
    public void NullPointer_DecodesToEmptyString_AndSucceeds()
    {
        FakeByteSource source = new();

        Assert.True(GodotString.TryRead(source, 0, out string value));
        Assert.Equal(string.Empty, value);
    }

    [Fact]
    public void ZeroLengthBuffer_DecodesToEmptyString()
    {
        FakeByteSource source = new FakeByteSource().WriteUInt64(Buffer - 8, 0);

        Assert.True(GodotString.TryRead(source, Buffer, out string value));
        Assert.Equal(string.Empty, value);
    }

    [Fact]
    public void StoredTerminator_IsNotPartOfTheDecodedString()
    {
        // Godot's String keeps a trailing NUL inside the CowData, so size() == length + 1.
        FakeByteSource source = new FakeByteSource().WriteGodotString(Buffer, "Proxy");

        Assert.True(CowData.TryReadElementCount(source, Buffer, out int count));
        Assert.Equal(6, count);

        Assert.True(GodotString.TryRead(source, Buffer, out string value));
        Assert.Equal("Proxy", value);
        Assert.DoesNotContain('\0', value);
    }

    [Fact]
    public void CountWithoutTerminator_AlsoDecodesCorrectly()
    {
        // Guards the decoder against the other size convention.
        FakeByteSource source = new FakeByteSource().WriteGodotString(Buffer, "AudioManager", includeTerminator: false);

        Assert.True(GodotString.TryRead(source, Buffer, out string value));
        Assert.Equal("AudioManager", value);
    }

    [Fact]
    public void InvalidScalar_BecomesReplacementChar_RatherThanThrowing()
    {
        // 0x110000 is above the Unicode maximum; a lone surrogate is equally illegal in UTF-32.
        // §8.8's error model says the read path reports, it does not throw.
        byte[] block = new byte[8];
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(0, 4), 0x110000);
        BinaryPrimitives.WriteUInt32LittleEndian(block.AsSpan(4, 4), 0xD800);

        string decoded = GodotString.DecodeUtf32(block);

        Assert.Equal(new string(GodotString.ReplacementChar, 2), decoded);
    }

    [Fact]
    public void UnreadableBuffer_Fails_AndYieldsEmpty()
    {
        FakeByteSource source = new FakeByteSource().WriteUInt64(Buffer - 8, 4); // count present, data absent

        Assert.False(GodotString.TryRead(source, Buffer, out string value));
        Assert.Equal(string.Empty, value);
    }

    [Fact]
    public void HighCodePoints_DoNotSurviveByteTruncation()
    {
        // Makes the anti-regression explicit rather than implicit in an equality assertion: this is
        // precisely what scry's char32_t -> byte narrowing produces, and it must not match.
        const string Text = "日本語";
        FakeByteSource source = new FakeByteSource().WriteGodotString(Buffer, Text);

        Assert.True(GodotString.TryRead(source, Buffer, out string value));

        string truncated = new([.. Text.Select(c => (char)(byte)c)]);
        Assert.NotEqual(truncated, value);
        Assert.Equal(Text, value);
    }

    [Fact]
    public void TryReadField_UsesTheProfilesFieldAndCowDataOffsets()
    {
        // Label::text at 0x800 (§4.6), validated 5/5 live in §12.3b.
        const ulong Label = 0x11000;
        GodotAbiProfile profile = GodotAbiProfiles.Godot451Release;

        FakeByteSource source = new FakeByteSource()
            .WritePointer(Label + (ulong)profile.Offsets.LabelText, Buffer)
            .WriteGodotString(Buffer, "Controller Detected");

        Assert.True(GodotString.TryReadField(source, profile, Label, GodotField.LabelText, out string value));
        Assert.Equal("Controller Detected", value);
    }

    [Fact]
    public void TryReadFromField_FollowsTheStoredBufferPointer()
    {
        // The Label::text shape: the field holds the CowData pointer directly.
        const ulong LabelText = 0x800;
        const ulong Node = 0x10000;

        FakeByteSource source = new FakeByteSource()
            .WritePointer(Node + LabelText, Buffer)
            .WriteGodotString(Buffer, "Connection Interrupted");

        Assert.True(GodotString.TryReadFromField(source, Node + LabelText, out string value));
        Assert.Equal("Connection Interrupted", value);
    }
}
