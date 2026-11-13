using Core.Abstractions;
using Core.Domain;

namespace Core.Storage;

// Тиждень 10: декоратор, що робить будь-яке сховище безпечним для паралельних
// HTTP-запитів. Кожен виклик виконується під одним lock, тож дві операції
// не можуть одночасно змінювати внутрішній словник чи писати у файл.
public sealed class SynchronizedBookStore(IBookStore inner) : IBookStore
{
    private readonly IBookStore _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly object _gate = new();

    public IReadOnlyList<BookCopy> List()
    {
        lock (_gate) return _inner.List();
    }

    public BookCopy? GetById(string id)
    {
        lock (_gate) return _inner.GetById(id);
    }

    public void Add(BookCopy item)
    {
        lock (_gate) _inner.Add(item);
    }

    public void Update(BookCopy item)
    {
        lock (_gate) _inner.Update(item);
    }

    public bool Remove(string id)
    {
        lock (_gate) return _inner.Remove(id);
    }
}