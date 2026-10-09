using Core.Abstractions;
using Core.Domain;

namespace Core.Storage;

public sealed class CachingBookStore(IBookStore inner) : IBookStore
{
    private readonly IBookStore _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public IBookStore Inner => _inner;

    private IReadOnlyList<BookCopy>? _cachedList;

    public int InnerListCalls { get; private set; }

    public IReadOnlyList<BookCopy> List()
    {
        if (_cachedList is null)
        {
            _cachedList = _inner.List();
            InnerListCalls++;
        }
        return _cachedList;
    }

    public BookCopy? GetById(string id) => _inner.GetById(id);

    public void Add(BookCopy item)
    {
        _inner.Add(item);
        _cachedList = null;
    }

    public void Update(BookCopy item)
    {
        _inner.Update(item);
        _cachedList = null;
    }

    public bool Remove(string id)
    {
        bool removed = _inner.Remove(id);
        if (removed) _cachedList = null;
        return removed;
    }
}