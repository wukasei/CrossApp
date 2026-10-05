using Core.Dto;

namespace Core.Domain;

public sealed class BookCopy
{
    public string Id { get; }
    public string Isbn { get; }
    public string Title { get; }
    public int Year { get; }
    public string? Author { get; }

    public CopyStatus Status { get; private set; }

    public bool IsIssued => Status == CopyStatus.Issued;

    private BookCopy(string id, string isbn, string title, int year, string? author, CopyStatus status)
    {
        Id = id;
        Isbn = isbn;
        Title = title;
        Year = year;
        Author = author;
        Status = status;
    }

    public static BookCopy Create(string id, string isbn, string title, int year, string? author)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Ідентифікатор примірника обов'язковий", nameof(id));

        if (string.IsNullOrWhiteSpace(isbn))
            throw new ArgumentException("ISBN не може бути порожнім", nameof(isbn));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Назва книжки не може бути порожньою", nameof(title));

        if (year < 1450 || year > DateTime.Today.Year)
            throw new ArgumentOutOfRangeException(nameof(year), year,
                $"Рік видання має бути від 1450 до {DateTime.Today.Year}");

        return new BookCopy(
            id.Trim(),
            isbn.Trim(),
            title.Trim(),
            year,
            string.IsNullOrWhiteSpace(author) ? null : author.Trim(),
            CopyStatus.Available);
    }

    public void Issue() => MoveTo(CopyStatus.Issued);

    public void Return() => MoveTo(CopyStatus.Available);

    public void WriteOff() => MoveTo(CopyStatus.WrittenOff);

    // Єдине місце, де змінюється стан. Тут таблиця всіх переходів.
    private void MoveTo(CopyStatus target)
    {
        string? error = (Status, target) switch
        {
            (CopyStatus.Available, CopyStatus.Issued)     => null,
            (CopyStatus.Issued,    CopyStatus.Available)  => null,
            (CopyStatus.Available, CopyStatus.WrittenOff) => null,

            (CopyStatus.Issued,    CopyStatus.Issued)     => "вже виданий, повторна видача неможлива",
            (CopyStatus.Available, CopyStatus.Available)  => "не виданий, повертати нічого",
            (CopyStatus.Issued,    CopyStatus.WrittenOff) => "зараз виданий, списати можна лише після повернення",
            (CopyStatus.WrittenOff, _)                    => "списаний, жодні операції з ним неможливі",

            _ => "недопустима зміна стану"
        };

        if (error is not null)
            throw new InvalidOperationException($"Примірник {Id} («{Title}») {error}");

        Status = target;
    }

    public BookDto ToDto() => new(Id, Isbn, Title, Year, Author, IsIssued);

    public static BookCopy FromDto(BookDto dto)
    {
        if (dto is null)
            throw new ArgumentNullException(nameof(dto), "Не передано дані книжки");

        BookCopy copy = Create(dto.Id, dto.Isbn, dto.Title, dto.Year, dto.Author);

        if (dto.IsIssued)
            copy.Issue();

        return copy;
    }

    public static ImportResult<BookCopy> FromImport(ImportResult<BookDto> imported)
    {
        if (imported is null)
            throw new ArgumentNullException(nameof(imported), "Не передано результат імпорту");

        List<BookCopy> copies = new();
        List<string> errors = new(imported.Errors);

        foreach (BookDto dto in imported.Items)
        {
            try
            {
                copies.Add(FromDto(dto));
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                errors.Add($"Книжка {dto.Id}: {ex.Message}");
            }
        }

        return new ImportResult<BookCopy>(copies, errors);
    }

    public override string ToString() =>
        $"{Id} [{Isbn}] «{Title}» ({Year}) — {Describe(Status)}";

    private static string Describe(CopyStatus status) => status switch
    {
        CopyStatus.Available  => "на полиці",
        CopyStatus.Issued     => "видано",
        CopyStatus.WrittenOff => "списано",
        _ => status.ToString()
    };
}