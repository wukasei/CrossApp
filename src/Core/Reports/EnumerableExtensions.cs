namespace Core.Reports;

public static class EnumerableExtensions
{
    public static IEnumerable<T> TopBy<T>(this IEnumerable<T> source, Func<T, int> key, Func<T, string> thenBy,int n) =>
        source.OrderByDescending(key)
              .ThenBy(thenBy, StringComparer.Ordinal)
              .Take(n);
}