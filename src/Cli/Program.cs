using Core.Domain;
using Core.Dto;
using Core.Services;

Console.WriteLine("=== Сценарій 1: успіх ===");

BookCopy kobzar = BookCopy.Create("C-001", " 978-966-03-4561-6 ", "Кобзар", 2015, "Тарас Шевченко");
Console.WriteLine(kobzar);

Loan loan1 = Loan.Open("L-001", kobzar, "R-001", new DateOnly(2026, 9, 1));
Console.WriteLine(loan1);
Console.WriteLine(kobzar);

loan1.Close(kobzar, new DateOnly(2026, 9, 15));
Console.WriteLine(loan1);
Console.WriteLine(kobzar);

BookDto kobzarDto = kobzar.ToDto();
BookCopy restored = BookCopy.FromDto(kobzarDto);
Console.WriteLine($"Після ToDto → FromDto: {restored}");

Console.WriteLine();
Console.WriteLine("=== Сценарій 2: порушення інваріантів ===");

BookCopy eneida = BookCopy.Create("C-002", "978-966-01-0123-4", "Енеїда", 2010, "Іван Котляревський");
Loan loan2 = Loan.Open("L-002", eneida, "R-002", new DateOnly(2026, 9, 20));

TryDo("порожній ISBN", () => BookCopy.Create("C-003", "   ", "Лісова пісня", 2012, null));
TryDo("рік видання 3000", () => BookCopy.Create("C-004", "978-966-02-0000-1", "Тигролови", 3000, null));
TryDo("повторна видача примірника", () => Loan.Open("L-003", eneida, "R-003", new DateOnly(2026, 9, 21)));
TryDo("повторне закриття видачі", () => loan1.Close(kobzar, new DateOnly(2026, 9, 30)));
TryDo("повернення раніше видачі", () => loan2.Close(eneida, new DateOnly(2026, 9, 1)));
TryDo("бита дата у файлі", () => Loan.FromDto(new LoanDto("L-004", "C-002", "R-002", "абвгд")));

Console.WriteLine();
Console.WriteLine("Стан після всіх відмов (нічого не змінилось):");
Console.WriteLine($"  {eneida}");
Console.WriteLine($"  {loan2}");

Console.WriteLine();
Console.WriteLine("=== Колекція: книга і її примірники ===");

Book kobzarBook = Book.Create("978-966-03-4561-6", "Кобзар", "Тарас Шевченко");
kobzarBook.AddCopy(kobzar);
kobzarBook.AddCopy(BookCopy.Create("C-005", "978-966-03-4561-6", "Кобзар", 2019, "Тарас Шевченко"));

Console.WriteLine(kobzarBook);
foreach (BookCopy c in kobzarBook.Copies)
    Console.WriteLine($"  {c}");

TryDo("додати той самий примірник двічі", () => kobzarBook.AddCopy(kobzar));
TryDo("додати примірник іншої книги", () => kobzarBook.AddCopy(eneida));

Console.WriteLine();
Console.WriteLine("=== Додаткове 1: імпорт → сутності ===");

ImportResult<BookDto> imported = new(
    [
        new BookDto("C-010", "978-617-000-001-1", "Захар Беркут", 2018, "Іван Франко"),
        new BookDto("C-011", "", "Тіні забутих предків", 2016, "Михайло Коцюбинський"),
        new BookDto("C-012", "978-617-000-003-3", "Хіба ревуть воли", 3000, "Панас Мирний")
    ],
    ["рядок 7: не вдалося прочитати рік"]);

ImportResult<BookCopy> domainResult = BookCopy.FromImport(imported);

Console.WriteLine($"Прийнято книжок: {domainResult.Items.Count}");
foreach (BookCopy c in domainResult.Items)
    Console.WriteLine($"  {c}");

Console.WriteLine($"Помилок: {domainResult.Errors.Count}");
foreach (string e in domainResult.Errors)
    Console.WriteLine($"  ! {e}");

Console.WriteLine();
Console.WriteLine("=== Додаткове 2: не більше 5 видач на читача ===");

List<Loan> loans = new();
for (int i = 1; i <= 5; i++)
{
    BookCopy c = BookCopy.Create($"C-10{i}", $"978-617-100-00{i}", $"Книжка №{i}", 2020, null);
    loans.Add(LoanService.OpenLoan($"L-10{i}", c, "R-100", new DateOnly(2026, 9, i), loans));
}
Console.WriteLine($"Відкритих видач у R-100: {loans.Count(l => l.ReaderId == "R-100" && !l.IsClosed)}");

BookCopy sixth = BookCopy.Create("C-106", "978-617-100-006", "Книжка №6", 2020, null);
TryDo("шоста видача одному читачу", () => LoanService.OpenLoan("L-106", sixth, "R-100", new DateOnly(2026, 9, 10), loans));
Console.WriteLine($"  Шоста книжка після відмови: {sixth}");

Console.WriteLine();
Console.WriteLine("=== Додаткове 3: стани примірника ===");

BookCopy old = BookCopy.Create("C-200", "978-966-00-0200-0", "Старий довідник", 1985, null);
old.WriteOff();
Console.WriteLine(old);

TryDo("видати списаний примірник", () => old.Issue());
TryDo("списати виданий примірник", () => eneida.WriteOff());

static void TryDo(string title, Action action)
{
    try
    {
        action();
        Console.WriteLine($"  {title}: виняток НЕ спрацював — інваріант відсутній!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  {title}: {ex.GetType().Name} — {ex.Message}");
    }
}