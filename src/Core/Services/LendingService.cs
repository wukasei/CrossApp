using Core.Abstractions;
using Core.Domain;
using Core.Events;

namespace Core.Services;

public sealed class LendingService(IBookStore store, IEnumerable<INotificationSink>? sinks = null)
{
    public const int LoanPeriodDays = 14;

    private readonly IBookStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly INotificationSink[] _sinks = sinks?.ToArray() ?? [];

    public event EventHandler<LibraryChangedEventArgs>? Changed;

    private void OnChanged(LibraryChangeKind kind, BookCopy copy,
                           string? readerId = null, DateOnly? dueDate = null)
    {
        var args = new LibraryChangedEventArgs(kind, copy.Isbn, copy.Id, readerId, dueDate);

        Changed?.Invoke(this, args);        // спосіб 1: подія
        foreach (var sink in _sinks)        // спосіб 2: отримувачі з конструктора
            sink.Notify(args);
    }

    public BookCopy AddBook(string isbn, string title, int year, string? author)
    {
        var copy = BookCopy.Create(Guid.NewGuid().ToString("N")[..8], isbn, title, year, author);
        _store.Add(copy);
        OnChanged(LibraryChangeKind.BookAdded, copy);   // після зміни
        return copy;
    }

    public void IssueCopy(string id, string readerId)
    {
        if (string.IsNullOrWhiteSpace(readerId))
            throw new ArgumentException("Не вказано читача", nameof(readerId));

        var copy = _store.GetById(id)
            ?? throw new DomainException($"Немає примірника з id={id}.");
        copy.Issue();            // якщо вже виданий — виняток, і до події не дійдемо
        _store.Update(copy);

        var dueDate = DateOnly.FromDateTime(DateTime.Today).AddDays(LoanPeriodDays);
        OnChanged(LibraryChangeKind.CopyIssued, copy, readerId.Trim(), dueDate);
    }

    public void ReturnCopy(string id)
    {
        var copy = _store.GetById(id)
            ?? throw new DomainException($"Немає примірника з id={id}.");
        copy.Return();           // якщо не виданий — виняток, події не буде
        _store.Update(copy);
        OnChanged(LibraryChangeKind.CopyReturned, copy);
    }

    // Тиждень 10: оновлення даних примірника (для PUT). null — примірника немає.
    public BookCopy? UpdateBook(string id, string isbn, string title, int year, string? author)
    {
        var existing = _store.GetById(id);
        if (existing is null) return null;

        var replacement = BookCopy.Create(existing.Id, isbn, title, year, author);

        // Create завжди дає «на полиці», тому переносимо статус старого примірника
        if (existing.Status == CopyStatus.Issued) replacement.Issue();
        else if (existing.Status == CopyStatus.WrittenOff) replacement.WriteOff();

        _store.Update(replacement);
        return replacement;
    }

    // Тиждень 10: видалення примірника (для DELETE). false — примірника немає.
    public bool RemoveBook(string id)
    {
        var copy = _store.GetById(id);
        if (copy is null) return false;

        if (copy.IsIssued)
            throw new DomainException(
                $"Примірник {id} («{copy.Title}») зараз виданий, видалити можна лише після повернення");

        return _store.Remove(id);
    }

    // Тиждень 8: асинхронний імпорт. Імпортер приходить параметром (ручний DI).
    // Додаткове 3: progress отримує відсоток оброблених записів (0–100).
    public async Task<int> ImportAsync(IBookImporter importer, string path,
                                       CancellationToken ct = default, IProgress<int>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(importer);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ct.ThrowIfCancellationRequested();

        var copies = await importer.LoadAsync(path, ct);   // токен передаємо далі

        var added = 0;
        var lastPercent = -1;
        for (var i = 0; i < copies.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var copy = copies[i];
            if (_store.GetById(copy.Id) is null)   // уже є — пропускаємо (ідемпотентність)
            {
                _store.Add(copy);
                OnChanged(LibraryChangeKind.BookAdded, copy);   // подія для кожного доданого
                added++;
            }

            var percent = (i + 1) * 100 / copies.Count;
            if (percent != lastPercent)            // повідомляємо лише коли відсоток змінився
            {
                progress?.Report(percent);
                lastPercent = percent;
            }
        }

        return added;
    }

        // Додаткове 1: потоковий імпорт через await foreach
    public async Task<int> ImportStreamAsync(IBookStreamImporter importer, string path,
                                             CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(importer);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var added = 0;
        await foreach (var copy in importer.StreamAsync(path).WithCancellation(ct))
        {
            if (_store.GetById(copy.Id) is not null) continue;

            _store.Add(copy);
            OnChanged(LibraryChangeKind.BookAdded, copy);
            added++;
        }

        return added;
    }

        // Додаткове 2: імпорт із таймаутом через зв'язані токени
    public Task<int> ImportWithTimeoutAsync(IBookImporter importer, string path,
                                            TimeSpan timeout, CancellationToken userToken = default,
                                            IProgress<int>? progress = null) =>
        WithTimeoutAsync(ct => ImportAsync(importer, path, ct, progress), timeout, userToken);

    public Task<int> ImportStreamWithTimeoutAsync(IBookStreamImporter importer, string path,
                                                  TimeSpan timeout, CancellationToken userToken = default) =>
        WithTimeoutAsync(ct => ImportStreamAsync(importer, path, ct), timeout, userToken);

    private static async Task<T> WithTimeoutAsync<T>(Func<CancellationToken, Task<T>> operation,
                                                     TimeSpan timeout, CancellationToken userToken)
    {
        using var timeoutCts = new CancellationTokenSource(timeout);   // таймер стартує тут
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(userToken, timeoutCts.Token);

        try
        {
            return await operation(linked.Token);   // операція бачить лише один, спільний токен
        }
        catch (OperationCanceledException ex)
            when (timeoutCts.IsCancellationRequested && !userToken.IsCancellationRequested)
        {
            // скасував саме таймаут, а не користувач
            throw new TimeoutException($"Імпорт не завершився за {timeout.TotalMilliseconds} мс.", ex);
        }
    }

    public IReadOnlyList<BookCopy> Search(Func<BookCopy, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return _store.List().Where(predicate).ToList();
    }

    public IReadOnlyList<BookCopy> All() => _store.List();

    public BookCopy? Find(string id) => _store.GetById(id);
}