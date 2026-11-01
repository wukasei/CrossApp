using Core.Domain;

namespace Core.Tests;

// Маленький фіксований набір лише для тестів (не SampleData).
// T-02 і T-03 мають однаковий рік — щоб перевірити стабільний порядок топ-N.
internal static class TestData
{
    public static List<BookCopy> Items()
    {
        var a = BookCopy.Create("T-01", "978-0-00-000001-1", "Тіні забутих предків", 2015, "М. Коцюбинський");
        var b = BookCopy.Create("T-02", "978-0-00-000002-8", "Кобзар", 2020, "Т. Шевченко");
        var c = BookCopy.Create("T-03", "978-0-00-000002-8", "Кобзар", 2020, "Т. Шевченко");
        var d = BookCopy.Create("T-04", "978-0-00-000004-2", "Лісова пісня", 2001, "Леся Українка");
        var e = BookCopy.Create("T-05", "978-0-00-000005-9", "Захар Беркут", 2023, "І. Франко");

        b.Issue();   // T-02 видано
        d.Issue();   // T-04 видано

        return [a, b, c, d, e];
    }
}