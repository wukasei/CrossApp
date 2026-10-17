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
            ?? throw new InvalidOperationException($"Немає примірника з id={id}.");
        copy.Issue();            // якщо вже виданий — виняток, і до події не дійдемо
        _store.Update(copy);

        var dueDate = DateOnly.FromDateTime(DateTime.Today).AddDays(LoanPeriodDays);
        OnChanged(LibraryChangeKind.CopyIssued, copy, readerId.Trim(), dueDate);
    }

    public void ReturnCopy(string id)
    {
        var copy = _store.GetById(id)
            ?? throw new InvalidOperationException($"Немає примірника з id={id}.");
        copy.Return();           // якщо не виданий — виняток, події не буде
        _store.Update(copy);
        OnChanged(LibraryChangeKind.CopyReturned, copy);
    }

    public IReadOnlyList<BookCopy> Search(Func<BookCopy, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return _store.List().Where(predicate).ToList();
    }

    public IReadOnlyList<BookCopy> All() => _store.List();

    public BookCopy? Find(string id) => _store.GetById(id);
}