using Core.Domain;

namespace Infrastructure.Persistence;

// Модель зберігання примірника. Id збігається з BookCopy.Id з домену.
public sealed class CopyRow
{
    public string Id { get; set; } = "";
    public CopyStatus Status { get; set; }

    // зовнішній ключ на Books
    public string BookId { get; set; } = "";
    public BookRow Book { get; set; } = null!;
}