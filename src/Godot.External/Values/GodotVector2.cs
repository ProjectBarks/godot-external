namespace Godot.External.Values;

/// <summary>
/// A Godot <c>Vector2</c>, widened to <see cref="double"/> so single- and double-precision builds
/// share one representation (a <see cref="float"/> converts exactly).
/// </summary>
/// <remarks>
/// Internal, like everything else that touches <see cref="Abi.IByteSource"/>: its only producer is
/// <see cref="ControlGeometry"/>, so a public version would be a type no consumer could obtain. The
/// public surface gets decided once the LiveClr swap and the Scene layer land (§8.8).
/// </remarks>
internal readonly record struct GodotVector2(double X, double Y)
{
    /// <summary>The zero vector — also what a stale <c>globalPosition</c> cache reads as (§12.3).</summary>
    public static GodotVector2 Zero => default;

    /// <summary>Component-wise sum, used to compose local positions up the tree.</summary>
    public static GodotVector2 operator +(GodotVector2 left, GodotVector2 right)
        => new(left.X + right.X, left.Y + right.Y);

    /// <inheritdoc/>
    public override string ToString() => $"[{X}, {Y}]";
}
