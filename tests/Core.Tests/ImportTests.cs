using Core.Abstractions;
using Core.Domain;
using Core.Events;
using Core.Services;
using Core.Storage;
using Xunit;

namespace Core.Tests;

public class ImportTests
{
    // Імпортер-заглушка: повертає готовий список, диск не потрібен
    private sealed class FakeImporter(params BookCopy[] items) : IBookImporter
    {
        public Task<IReadOnlyList<BookCopy>> LoadAsync(string path, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<BookCopy>>(items);
        }
    }

    // Синхронний збирач значень прогресу (Progress<T> викликав би обробник асинхронно)
    private sealed class ListProgress : IProgress<int>
    {
        public List<int> Values { get; } = [];
        public void Report(int value) => Values.Add(value);
    }

    [Fact] // 7) скасування: токен скасовано заздалегідь
    public async Task ImportAsync_WhenAlreadyCancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var service = new LendingService(new InMemoryBookStore());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ImportAsync(new JsonBookImporter(), "any.json", cts.Token));
    }

    [Fact] // 8) ідемпотентність: повторний імпорт нічого не додає і подій не піднімає
    public async Task ImportAsync_Twice_AddsOnlyOnce()
    {
        var importer = new FakeImporter(
            BookCopy.Create("I-1", "978-0-00-000011-0", "Енеїда", 2018, null),
            BookCopy.Create("I-2", "978-0-00-000012-7", "Місто", 2019, null));
        var service = new LendingService(new InMemoryBookStore());
        var events = new List<LibraryChangedEventArgs>();
        service.Changed += (_, e) => events.Add(e);

        var first = await service.ImportAsync(importer, "fake");
        var second = await service.ImportAsync(importer, "fake");

        Assert.Equal(2, first);
        Assert.Equal(0, second);
        Assert.Equal(2, events.Count);
    }

    [Fact] // 9) зіпсований запис у тимчасовому файлі → DomainException з номером запису
    public async Task ImportAsync_WithInvalidRecord_ThrowsDomainException()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lab08-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, """
                [
                  { "Id": "B-1", "Isbn": "978-1", "Title": "Добра книжка", "Year": 2010 },
                  { "Id": "B-2", "Isbn": "978-2", "Title": "", "Year": 2010 }
                ]
                """);
            var store = new InMemoryBookStore();
            var service = new LendingService(store);

            var ex = await Assert.ThrowsAsync<DomainException>(
                () => service.ImportAsync(new JsonBookImporter(), path));

            Assert.StartsWith("Запис 2", ex.Message);
            Assert.IsType<ArgumentException>(ex.InnerException);   // оригінальна причина збережена
            Assert.Empty(store.List());                            // файл відхилено цілком
        }
        finally
        {
            File.Delete(path);
        }
    }

        [Fact] // 10) додаткове 1: токен із WithCancellation доходить до StreamAsync
    public async Task ImportStreamAsync_WhenAlreadyCancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var service = new LendingService(new InMemoryBookStore());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ImportStreamAsync(new JsonBookImporter(), "any.json", cts.Token));
    }

    [Fact] // 11) додаткове 1: потоковий імпорт додає записи до помилки
    public async Task ImportStreamAsync_WithInvalidRecord_AddsRecordsBeforeError()
    {
        var path = Path.Combine(Path.GetTempPath(), $"lab08-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(path, """
                [
                  { "Id": "S-1", "Isbn": "978-1", "Title": "Перша", "Year": 2010 },
                  { "Id": "S-2", "Isbn": "978-2", "Title": "Друга", "Year": 2011 },
                  { "Id": "S-3", "Isbn": "978-3", "Title": "Третя", "Year": 1200 }
                ]
                """);
            var store = new InMemoryBookStore();
            var service = new LendingService(store);

            var ex = await Assert.ThrowsAsync<DomainException>(
                () => service.ImportStreamAsync(new JsonBookImporter(), path));

            Assert.StartsWith("Запис 3", ex.Message);
            Assert.Equal(2, store.List().Count);   // перші два вже встигли додатися
        }
        finally
        {
            File.Delete(path);
        }
    }

        [Fact] // 14) додаткове 3: прогрес росте і закінчується на 100
    public async Task ImportAsync_ReportsProgress()
    {
        var importer = new FakeImporter(
            BookCopy.Create("P-1", "978-1", "Перша", 2010, null),
            BookCopy.Create("P-2", "978-2", "Друга", 2011, null),
            BookCopy.Create("P-3", "978-3", "Третя", 2012, null),
            BookCopy.Create("P-4", "978-4", "Четверта", 2013, null));
        var service = new LendingService(new InMemoryBookStore());
        var progress = new ListProgress();

        await service.ImportAsync(importer, "fake", progress: progress);

        Assert.Equal(new[] { 25, 50, 75, 100 }, progress.Values);
    }
}