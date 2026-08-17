using System.Text.Json;

namespace Godot.External.Calibrator.Protocol;

/// <summary>
/// Reads a vector out of JSON in any of the three shapes the harness tolerates.
/// </summary>
/// <remarks>
/// docs/analysis.md §12.6: scry hands vectors back as array-<em>likes</em> (<c>{0:…,1:…}</c>) for
/// which <c>Array.isArray()</c> is false, and <c>lib/util.mjs</c> normalises all three shapes on the
/// way in. A request is authored by the harness and so is always a plain array today, but parsing
/// defensively costs nothing and stops a future shape change from reading as a calibration failure.
/// </remarks>
public static class JsonVector
{
    /// <summary>Reads <paramref name="arity"/> components, or <see langword="null"/>.</summary>
    public static double[]? Read(JsonElement element, int arity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(arity);

        return element.ValueKind switch
        {
            JsonValueKind.Array => FromArray(element, arity),
            JsonValueKind.Object => FromObject(element, arity),
            _ => null,
        };
    }

    private static double[]? FromArray(JsonElement element, int arity)
    {
        if (element.GetArrayLength() < arity)
        {
            return null;
        }

        double[] values = new double[arity];
        int index = 0;
        foreach (JsonElement item in element.EnumerateArray())
        {
            if (index >= arity)
            {
                break;
            }

            if (item.ValueKind != JsonValueKind.Number || !item.TryGetDouble(out values[index]))
            {
                return null;
            }

            index++;
        }

        return values;
    }

    private static double[]? FromObject(JsonElement element, int arity)
    {
        string[] named = ["x", "y", "z", "w"];
        double[] values = new double[arity];

        for (int i = 0; i < arity; i++)
        {
            if (!TryNumber(element, i.ToString(System.Globalization.CultureInfo.InvariantCulture), out values[i])
                && !(i < named.Length && TryNumber(element, named[i], out values[i])))
            {
                return null;
            }
        }

        return values;
    }

    private static bool TryNumber(JsonElement element, string name, out double value)
    {
        value = 0;
        return element.TryGetProperty(name, out JsonElement member)
            && member.ValueKind == JsonValueKind.Number
            && member.TryGetDouble(out value);
    }
}
