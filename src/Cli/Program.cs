using Cli;
using Core.Abstractions;
using Core.Services;
using Core.Storage;

IBookStore store = StoreFactory.Create(args);
var service = new LendingService(store);

string storeName = store is CachingBookStore cached
    ? $"{cached.Inner.GetType().Name} (з кешем CachingBookStore)"
    : store.GetType().Name;
Console.WriteLine($"Сховище: {storeName}");

var created = service.AddBook("978-617-585-111-2", "Intermezzo", 2020, "Михайло Коцюбинський");
Console.WriteLine($"\nДодано: {created}");

service.IssueCopy(created.Id);
Console.WriteLine($"Після видачі: {service.Find(created.Id)}");

Console.WriteLine("\nУсі примірники:");
foreach (var c in service.All())
    Console.WriteLine($"  {c}");

// ===== Додаткове 2: пошук з Func =====
Console.WriteLine("\nКнижки, видані після 2017 року:");
foreach (var c in service.Search(c => c.Year > 2017))
    Console.WriteLine($"  {c}");

Console.WriteLine("\nВидані зараз:");
foreach (var c in service.Search(c => c.IsIssued))
    Console.WriteLine($"  {c}");

// ===== Додаткове 1: перевірка кешу =====
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
    service.IssueCopy("NOPE-999");
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"  Помилка: {ex.Message}");
}

try
{
    service.IssueCopy(created.Id);
}
catch (InvalidOperationException ex)
{
    Console.WriteLine($"  Помилка: {ex.Message}");
}