using Core.Domain;

namespace Core.Abstractions;

public interface IBookStore
{
    IReadOnlyList<BookCopy> List();
    BookCopy? GetById(string id);
    void Add(BookCopy item);
    void Update(BookCopy item);
    bool Remove(string id);
}