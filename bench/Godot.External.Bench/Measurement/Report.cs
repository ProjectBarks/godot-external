using System.Globalization;
using System.Text;

namespace Godot.External.Bench.Measurement;

/// <summary>Renders benchmark rows as Markdown tables and as CSV.</summary>
internal static class Report
{
    /// <summary>One table per (target, workload), rows in variant order.</summary>
    public static void WriteMarkdown(IReadOnlyList<BenchRow> rows, TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(writer);

        foreach (IGrouping<(string Target, string Workload), BenchRow> group in
                 rows.GroupBy(r => (r.Target, r.Workload)))
        {
            BenchRow baseline = group.First();

            writer.WriteLine();
            writer.WriteLine($"### {group.Key.Target} / {group.Key.Workload}");
            writer.WriteLine();
            writer.WriteLine("| variant | syscalls | syscalls vs base | bytes read | bytes vs base | useful bytes | amplification | hit rate | wall ms | retained |");
            writer.WriteLine("|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|");

            foreach (BenchRow row in group)
            {
                double calls = baseline.Syscalls == 0 ? 0 : (double)row.Syscalls / baseline.Syscalls;
                double bytes = baseline.BytesRead == 0 ? 0 : (double)row.BytesRead / baseline.BytesRead;
                writer.WriteLine(string.Create(
                    CultureInfo.InvariantCulture,
                    $"| {row.Variant} | {row.Syscalls:N0} | {calls:F3}x | {row.BytesRead:N0} | {bytes:F2}x | {row.UsefulBytes:N0} | {row.Amplification:F2}x | {row.HitRate:P1} | {row.WallMs:F2} | {Bytes(row.RetainedBytes)} |"));
            }

            string extra = Extras(group);
            if (extra.Length > 0)
            {
                writer.WriteLine();
                writer.WriteLine(extra);
            }
        }
    }

    /// <summary>Every column, machine-readable, for keeping runs side by side over time.</summary>
    public static void WriteCsv(IReadOnlyList<BenchRow> rows, string path)
    {
        ArgumentNullException.ThrowIfNull(rows);

        StringBuilder csv = new();
        csv.AppendLine("target,workload,variant,syscalls,bytes_read,useful_bytes,logical_reads,amplification,"
                     + "hit_rate,wall_ms,span_fetches,span_overreads,agree_twice_suppressed,retained_bytes,items,checksum,note");

        foreach (BenchRow r in rows)
        {
            csv.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{r.Target},{r.Workload},{r.Variant},{r.Syscalls},{r.BytesRead},{r.UsefulBytes},{r.LogicalReads},"
              + $"{r.Amplification:F4},{r.HitRate:F4},{r.WallMs:F4},{r.SpanFetches},{r.SpanOverreads},"
              + $"{r.AgreeTwiceSuppressed},{r.RetainedBytes},{r.Items},{r.Checksum:X16},\"{r.Note}\""));
        }

        File.WriteAllText(path, csv.ToString());
    }

    private static string Extras(IEnumerable<BenchRow> rows)
    {
        List<string> lines = [];
        foreach (BenchRow row in rows)
        {
            List<string> parts = [];
            if (row.SpanFetches > 0)
            {
                parts.Add($"{row.SpanFetches:N0} span fetches");
            }

            if (row.SpanOverreads > 0)
            {
                parts.Add($"{row.SpanOverreads:N0} span over-reads fell back");
            }

            if (row.AgreeTwiceSuppressed > 0)
            {
                parts.Add($"{row.AgreeTwiceSuppressed:N0} agree-twice checks suppressed as vacuous");
            }

            if (row.Note.Contains("mismatch", StringComparison.Ordinal))
            {
                parts.Add(row.Note);
            }

            if (parts.Count > 0)
            {
                lines.Add($"- `{row.Variant}`: {string.Join("; ", parts)}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string Bytes(long value) => value switch
    {
        >= 1 << 20 => $"{value / (double)(1 << 20):F1} MiB",
        >= 1 << 10 => $"{value / (double)(1 << 10):F0} KiB",
        _ => $"{value} B",
    };
}
