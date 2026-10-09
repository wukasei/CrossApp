namespace Core.Domain;

public sealed class Book
{
    // сам список прихований, ззовні його не видно
    private readonly List<BookCopy> _copies = [];

    public string Isbn { get; }
    public string Title { get; }
    public string? Author { get; }

    // назовні — тільки для читання: дивитись можна, Add/Clear нема
    public IReadOnlyList<BookCopy> Copies => _copies.AsReadOnly();

    // скільки примірників зараз на полиці
    public int AvailableCount => _copies.Count(c => c.Status == CopyStatus.Available);

    private Book(string isbn, string title, string? author)
    {
        Isbn = isbn;
        Title = title;
        Author = author;
    }

    public static Book Create(string isbn, string title, string? author)
    {
        if (string.IsNullOrWhiteSpace(isbn))
            throw new ArgumentException("ISBN книги не може бути порожнім", nameof(isbn));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Назва книги не може бути порожньою", nameof(title));

        return new Book(
            isbn.Trim(),
            title.Trim(),
            string.IsNullOrWhiteSpace(author) ? null : author.Trim());
    }

    public void AddCopy(BookCopy copy)
    {
        if (copy is null)
            throw new ArgumentNullException(nameof(copy), "Не вказано примірник");

        if (copy.Isbn != Isbn)
            throw new ArgumentException(
                $"Примірник {copy.Id} має ISBN {copy.Isbn}, а книга «{Title}» — {Isbn}", nameof(copy));
        if (_copies.Any(c => c.Id == copy.Id))
            throw new InvalidOperationException(
                $"Примірник {copy.Id} вже доданий до книги «{Title}»");

        _copies.Add(copy);
    }

    public override string ToString() =>
        $"«{Title}» [{Isbn}] — примірників: {_copies.Count}, на полиці: {AvailableCount}";
}