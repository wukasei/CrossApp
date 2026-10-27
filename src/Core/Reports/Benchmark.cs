using System.Diagnostics;
using Core.Domain;

namespace Core.Reports;

// Додаткове 4: порівняння ланцюжка з ToList у середині і без нього
public static class Benchmark
{
    public static (double PlainMs, double MiddleToListMs) TopIssued(IReadOnlyList<BookCopy> data, int runs)
    {
        Plain(data);
        MiddleToList(data);

        double plain = Measure(() => Plain(data), runs);
        double middle = Measure(() => MiddleToList(data), runs);
        return (plain, middle);
    }

    private static double Measure(Action action, int runs)
    {
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < runs; i++)
            action();
        sw.Stop();
        return sw.Elapsed.TotalMilliseconds / runs;
    }

    // Без проміжних ToList: елементи йдуть по ланцюжку, сортування знає, що потрібні лише 5
    private static IReadOnlyList<CopyLine> Plain(IEnumerable<BookCopy> source) =>
        source.Where(c => c.IsIssued)
              .OrderByDescending(c => c.Year).ThenBy(c => c.Id, StringComparer.Ordinal)
              .Take(5)
              .Select(c => new CopyLine(c.Id, c.Title, c.Author, c.Year, c.Status))
              .ToList();

    // З ToList у середині: два проміжні списки і повне сортування всіх виданих примірників
    private static IReadOnlyList<CopyLine> MiddleToList(IEnumerable<BookCopy> source) =>
        source.Where(c => c.IsIssued).ToList()
              .OrderByDescending(c => c.Year).ThenBy(c => c.Id, StringComparer.Ordinal).ToList()
              .Take(5)
              .Select(c => new CopyLine(c.Id, c.Title, c.Author, c.Year, c.Status))
              .ToList();
}