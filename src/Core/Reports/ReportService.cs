using Core.Domain;

namespace Core.Reports;

public static class ReportService
{
    //Фільтр + сортування + проєкція: які примірники зараз на руках
    public static IReadOnlyList<CopyLine> Issued(IEnumerable<BookCopy> source) =>
        source.Where(c => c.IsIssued)
              .OrderBy(c => c.Year).ThenBy(c => c.Id, StringComparer.Ordinal)
              .Select(c => new CopyLine(c.Id, c.Title, c.Author, c.Year, c.Status))
              .ToList();

    //Топ-N зі стабільним порядком: n найновіших видань (через власний TopBy)
    public static IReadOnlyList<CopyLine> TopNewest(IEnumerable<BookCopy> source, int n) =>
        source.TopBy(c => c.Year, c => c.Id, n)
              .Select(c => new CopyLine(c.Id, c.Title, c.Author, c.Year, c.Status))
              .ToList();

    //Групування: скільки примірників кожної книжки і скільки з них видано
    public static IReadOnlyList<GroupSummary> ByTitle(IEnumerable<BookCopy> source) =>
        source.GroupBy(c => c.Title, StringComparer.OrdinalIgnoreCase)
              .Select(g => new GroupSummary(g.Key, g.Count(), g.Count(c => c.IsIssued)))
              .OrderByDescending(s => s.Count).ThenBy(s => s.Key, StringComparer.Ordinal)
              .ToList();

    //Додаткове 3: той самий звіт у синтаксисі запитів
    public static IReadOnlyList<GroupSummary> ByTitleQuery(IEnumerable<BookCopy> source) =>
        (from copy in source
         group copy by copy.Title into g
         let summary = new GroupSummary(g.Key, g.Count(), g.Count(c => c.IsIssued))
         orderby summary.Count descending, summary.Key
         select summary).ToList();

    //Агрегати з обробкою порожнього джерела
    public static (int Copies, int Issued, double AverageCopies, int NewestYear) Totals(IEnumerable<BookCopy> source)
    {
        var list = source.ToList();  

        return list.Count == 0
            ? (0, 0, 0d, 0)
            : (list.Count,
               list.Count(c => c.IsIssued),
               list.GroupBy(c => c.Title).Average(g => g.Count()),
               list.Max(c => c.Year));
    }

    //Демонстрація відкладеного виконання
    public static (int Before, int After, int InSnapshot) DeferredDemo(IEnumerable<BookCopy> items)
    {
        var source = items.ToList();                 
        var query = source.Where(c => c.IsIssued);   

        int before = query.Count();                  

        var extra = BookCopy.Create("TMP-001", "978-000-000-000-0", "Тимчасовий примірник", 2024, null);
        extra.Issue();
        source.Add(extra);                           // змінили джерело ПІСЛЯ побудови запиту

        int after = query.Count();                   

        var snapshot = query.ToList();               
        source.Clear();                              

        return (before, after, snapshot.Count);      
    }

    //Додаткове 1: Join примірників і видач у зведений DTO
    public static IReadOnlyList<LoanLine> LoanHistory(IEnumerable<BookCopy> copies, IEnumerable<Loan> loans) =>
        copies.Join(loans,
                    copy => copy.Id,              // ключ із примірника
                    loan => loan.BookCopyId,      // ключ із видачі
                    (copy, loan) => new LoanLine(loan.Id, copy.Id, copy.Title, loan.ReaderId,
                                                 loan.IssuedOn, loan.ReturnedOn))
              .OrderBy(l => l.IssuedOn).ThenBy(l => l.LoanId, StringComparer.Ordinal)
              .ToList();

    //Додаткове 2: топ читачів за кількістю видач (другий звіт з TopBy)
    public static IReadOnlyList<GroupSummary> TopReaders(IEnumerable<Loan> loans, int n) =>
        loans.GroupBy(l => l.ReaderId)
             .Select(g => new GroupSummary(g.Key, g.Count(), g.Count(l => !l.IsClosed)))
             .TopBy(s => s.Count, s => s.Key, n)
             .ToList();
}