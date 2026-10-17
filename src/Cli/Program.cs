using Cli;
using Core.Abstractions;
using Core.Events;      
using Core.Services;
using Core.Storage;
using Core.Domain;
using Core.Dto;

IBookStore store = StoreFactory.Create(args);
var service = new LendingService(store);

var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "app.log");
Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);

void OnLibraryChanged(object? sender, LibraryChangedEventArgs e)
{
    var details = e.Kind switch
    {
        LibraryChangeKind.BookAdded    => $"додано книжку, ISBN {e.Isbn}",
        LibraryChangeKind.CopyIssued   => $"видано читачу {e.ReaderId} до {e.DueDate:dd.MM.yyyy}",
        LibraryChangeKind.CopyReturned => "повернуто на полицю",
        _ => ""
    };

    var line = $"{e.At:HH:mm:ss} {e.Kind,-12} {e.CopyId} {details}";

    Console.WriteLine(line);
    File.AppendAllText(logPath, line + Environment.NewLine);
}

service.Changed += OnLibraryChanged;

int changes = 0;
service.Changed += (_, _) =>
{
    changes++;
    Console.WriteLine($"  [лічильник подій: {changes}]");
};

string storeName = store is CachingBookStore cached
    ? $"{cached.Inner.GetType().Name} (з кешем CachingBookStore)"
    : store.GetType().Name;
Console.WriteLine($"Сховище: {storeName}");

var created = service.AddBook("978-617-585-111-2", "Intermezzo", 2020, "Михайло Коцюбинський");
Console.WriteLine($"\nДодано: {created}");

service.IssueCopy(created.Id, "R-001");   
Console.WriteLine($"Після видачі: {service.Find(created.Id)}");

Console.WriteLine("\nУсі примірники:");
foreach (var c in service.All())
    Console.WriteLine($"  {c}");

Console.WriteLine("\nКнижки, видані після 2017 року:");
foreach (var c in service.Search(c => c.Year > 2017))
    Console.WriteLine($"  {c}");

Console.WriteLine("\nВидані зараз:");
foreach (var c in service.Search(c => c.IsIssued))
    Console.WriteLine($"  {c}");

if (store is CachingBookStore caching)
{
    service.All();
    service.All();
    Console.WriteLine($"\nЗвернень до внутрішнього сховища за списком: {caching.InnerListCalls}");
}

// ===== Сценарій відмови =====
Console.WriteLine("\nПеревірка помилок:");
try
{
    service.IssueCopy("NOPE-999", "R-001");   
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"  Помилка: {ex.Message}");
}

try
{
    service.IssueCopy(created.Id, "R-002");   
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"  Помилка: {ex.Message}");
}

// ===== крок 5: відписка =====
Console.WriteLine("\nПовернення книжки (обидва підписники активні):");
service.ReturnCopy(created.Id);
Console.WriteLine($"Подій отримано: {changes}");

service.Changed -= OnLibraryChanged;   // відписали логер

Console.WriteLine("\nПовторна видача після відписки логера:");
service.IssueCopy(created.Id, "R-003");
Console.WriteLine($"Подій отримано: {changes}");
Console.WriteLine($"Стан примірника: {service.Find(created.Id)}");
 
// ===== Додаткове 1: INotificationSink =====
Console.WriteLine("\nСервіс з INotificationSink (без підписки на подію):");
var sinkService = new LendingService(store, [new ConsoleSink(), new FileSink(logPath)]);
sinkService.ReturnCopy(created.Id);

// ===== Додаткове 2: Action<string> в імпорті =====
Console.WriteLine("\nІмпорт з повідомленням про пропущені рядки:");
var raw = new ImportResult<BookDto>(
    [
        new BookDto("C-100", "978-617-000-100-1", "Кайдашева сім'я", 2019, "Іван Нечуй-Левицький", false),
        new BookDto("C-101", "978-617-000-101-8", "", 2019, null, false),       // порожня назва
        new BookDto("C-102", "978-617-000-102-5", "Стара книга", 1200, null, false) // поганий рік
    ],
    ["рядок 5: не вистачає полів"]);

var imported = BookCopy.FromImport(raw, msg => Console.WriteLine($"  пропущено: {msg}"));
Console.WriteLine($"Прийнято примірників: {imported.Items.Count}");