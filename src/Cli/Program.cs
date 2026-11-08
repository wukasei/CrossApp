using Cli;
using Core;
using Core.Abstractions;
using Core.Events;      
using Core.Services;
using Core.Storage;
using Core.Domain;
using Core.Dto;
using Core.Reports;
using System.Text.Json;
using Core.Configuration;
using Microsoft.Extensions.Configuration;
using System.Reflection;

// ===== Тиждень 9: конфігурація =====
Console.OutputEncoding = System.Text.Encoding.UTF8;
var envName = Environment.GetEnvironmentVariable("CROSSAPP_ENVIRONMENT") ?? "Development";

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
    .AddJsonFile($"appsettings.{envName}.json", optional: true)
    .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
    .AddEnvironmentVariables(prefix: "CROSSAPP_")
    .Build();

var options = config.Get<AppOptions>() ?? new AppOptions();

// проста змінна CROSSAPP_DATA_PATH перекриває розділ Storage
var legacy = Environment.GetEnvironmentVariable("CROSSAPP_DATA_PATH");
var storage = new StorageOptions
{
    DataPath = string.IsNullOrWhiteSpace(legacy) ? options.Storage.DataPath : legacy,
    FileName = options.Storage.FileName
};

Console.WriteLine($"Профіль    : {options.EnvironmentName}, лог: {options.Logging.MinimumLevel}");
var dataFile = storage.ResolveFullPath(AppContext.BaseDirectory);
Console.WriteLine($"Файл даних : {dataFile}");
Console.WriteLine($"LoanDays   : {options.Library.LoanDays}");

var apiKey = config["Integration:ApiKey"];
Console.WriteLine(apiKey is null
    ? "ApiKey     : не заданий"
   : $"ApiKey     : присутній, довжина {apiKey.Length}");

IBookStore store;
try
{
    store = StoreFactory.Create(args, dataFile);
}
catch (InvalidDataException ex)
{
    Console.WriteLine($"Помилка: {ex.Message}");
    return;
}
var service = new LendingService(store);

// ===== Тиждень 9: експорт колекції в JSON =====
// dotnet run --project src/Cli -- export [шлях]
if (args.Contains("export"))
{
    var arg = After("export");
    var target = arg is not null && !arg.StartsWith("--")
        ? arg
        : Path.Combine(Path.GetDirectoryName(dataFile)!, "export.json");

    var items = service.All();
    try
    {
        FileBookStore.Save(target, items);
        Console.WriteLine($"Експортовано {items.Count} записів у {Path.GetFullPath(target)}");
    }
    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
    {
        Console.WriteLine($"Не вдалося записати файл: {ex.Message}");
    }
    return;
}

// ===== Тиждень 8: генерація великого файлу (для демонстрації скасування) =====
// dotnet run --project src/Cli -- gen-import data/big.json 300000
if (args.Contains("gen-import"))
{
    var genPath = After("gen-import") ?? "data/big.json";
    var count = int.TryParse(After("gen-import", 2), out var n) ? n : 300_000;

    var genDtos = SampleData.Generate(count).Select(c => c.ToDto()).ToList();
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(genPath))!);

    await using var fs = new FileStream(genPath, FileMode.Create, FileAccess.Write,
                                        FileShare.None, bufferSize: 4096, useAsync: true);
    await JsonSerializer.SerializeAsync(fs, genDtos);

    Console.WriteLine($"Згенеровано {count} записів у {genPath}");
    return;
}

// ===== Тиждень 8: асинхронний імпорт зі скасуванням =====
// dotnet run --project src/Cli -- import data/import.json [--timeout 100]
if (args.Contains("import"))
{
    var dataPath = After("import") ?? "data/import.json";
    var stream = args.Contains("--stream");
    TimeSpan? timeout = int.TryParse(After("--timeout"), out var timeoutMs)
        ? TimeSpan.FromMilliseconds(timeoutMs)
        : null;
    var progress = args.Contains("--progress") ? new ConsoleProgress() : null;   // додаткове 3

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };   // Ctrl+C

    string message;
    try
    {
        var importer = new JsonBookImporter();
        int added;

        if (timeout is TimeSpan limit)   // додаткове 2: зв'язані токени (Ctrl+C + таймаут)
            added = stream
                ? await service.ImportStreamWithTimeoutAsync(importer, dataPath, limit, cts.Token)
                : await service.ImportWithTimeoutAsync(importer, dataPath, limit, cts.Token, progress);
        else
            added = stream
                ? await service.ImportStreamAsync(importer, dataPath, cts.Token)
                : await service.ImportAsync(importer, dataPath, cts.Token, progress);

        message = $"Імпортовано записів: {added}";
    }
    catch (TimeoutException ex)         { message = $"Імпорт перервано за таймаутом: {ex.Message}"; }
    catch (OperationCanceledException) { message = "Імпорт скасовано."; }
    catch (DomainException ex)          { message = $"Дані відхилено: {ex.Message}"; }
    catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
                                        { message = $"Файл не знайдено: {dataPath}"; }
    catch (JsonException ex)            { message = $"Файл не є коректним JSON: {ex.Message}"; }

    progress?.EndLine();
    Console.WriteLine(message);
    return;
}

// значення аргументу, що йде після name (offset = 1 — наступний, 2 — через один)
string? After(string name, int offset = 1)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + offset < args.Length ? args[i + offset] : null;
}

var command = args.FirstOrDefault(a => a.StartsWith("report-"));

if (command is not null)
{
    var items = service.All();

    switch (command)
    {
        case "report-issued":
            PrintCopies(ReportService.Issued(items));
            break;

        case "report-top":
            PrintCopies(ReportService.TopNewest(items, 5));
            break;

        case "report-groups":
            PrintGroups(ReportService.ByTitle(items));
            break;

        case "report-groups-q":   // додаткове 3: синтаксис запитів
            PrintGroups(ReportService.ByTitleQuery(items));
            break;

        case "report-sum":
            var t = ReportService.Totals(items);
            Console.WriteLine($" примірників {t.Copies}, видано {t.Issued}, " +
                              $"в середньому {t.AverageCopies:F2} прим. на книжку, найновіший рік {t.NewestYear}");
            break;

        case "report-demo":
            var d = ReportService.DeferredDemo(items);
            Console.WriteLine($" Виданих до змін:     {d.Before}");
            Console.WriteLine($" Виданих після змін:  {d.After}");
            Console.WriteLine($" У знімку лишилось:   {d.InSnapshot}");
            break;

        case "report-loans":      // додаткове 1: Join
            foreach (var l in ReportService.LoanHistory(items, SampleData.Loans()))
                Console.WriteLine($" {l.LoanId,-6} {l.CopyId,-6} {l.Title,-24} {l.ReaderId,-6} " +
                                  $"{l.IssuedOn:dd.MM.yyyy} → {l.ReturnedOn?.ToString("dd.MM.yyyy") ?? "на руках"}");
            break;

        case "report-readers":    // додаткове 2: TopBy
            foreach (var r in ReportService.TopReaders(SampleData.Loans(), 5))
                Console.WriteLine($" {r.Key,-6} видач: {r.Count,2}   на руках: {r.IssuedCount,2}");
            break;

        case "report-bench":      // додаткове 4: Stopwatch
            var data = SampleData.Generate(100_000);
            var b = Benchmark.TopIssued(data, 20);
            Console.WriteLine($" {data.Count} записів, середнє з 20 запусків:");
            Console.WriteLine($" без ToList у середині: {b.PlainMs,8:F2} мс");
            Console.WriteLine($" з ToList у середині:   {b.MiddleToListMs,8:F2} мс");
            break;

        default:
            Console.WriteLine("report-issued | report-top | report-groups | report-sum | report-demo");
            Console.WriteLine("report-loans | report-readers | report-groups-q | report-bench");
            break;
    }

    return;   // звіт показано, демо попередніх тижнів не запускаємо
}

// ===== Тижні 5-6: демо (запускається, якщо команди звіту немає) =====

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
catch (DomainException ex)
{
    Console.WriteLine($"  Помилка: {ex.Message}");
}

try
{
    service.IssueCopy(created.Id, "R-002");   
}
catch (DomainException ex)
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

// ===== Тиждень 7: друк рядків звіту =====
static void PrintCopies(IReadOnlyList<CopyLine> lines)
{
    foreach (var l in lines)
        Console.WriteLine($" {l.Id,-7} {l.Title,-24} {l.Author ?? "—",-24} {l.Year}  {l.Status}");
}

static void PrintGroups(IReadOnlyList<GroupSummary> groups)
{
    foreach (var g in groups)
        Console.WriteLine($" {g.Key,-24} примірників: {g.Count,2}   видано: {g.IssuedCount,2}");
}