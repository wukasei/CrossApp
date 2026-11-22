using Core;
using Core.Abstractions;
using Core.Domain;
using Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

// Реалізація IBookStore з Core поверх EF Core + SQLite.
// Назовні — ті самі BookCopy, що й у InMemory/File сховищах; всередині — дві таблиці Books 1:N Copies.
// Синхронні виклики БД — свідоме навчальне спрощення для сумісності з інтерфейсом тижня 5.
public sealed class EfBookStore(AppDbContext db) : IBookStore
{
    private readonly AppDbContext _db = db ?? throw new ArgumentNullException(nameof(db));

    // ---------- мапінг ----------

    // Row → домен ТІЛЬКИ через фабрику BookCopy.Create (перевіряє інваріанти).
    // Create завжди дає «на полиці», тому статус відновлюємо доменними переходами,
    // а не присвоєнням поля в обхід перевірок.
    private static BookCopy ToDomain(CopyRow row)
    {
        var copy = BookCopy.Create(row.Id, row.Book.Isbn, row.Book.Title, row.Book.Year, row.Book.Author);

        switch (row.Status)
        {
            case CopyStatus.Issued:     copy.Issue();    break;
            case CopyStatus.WrittenOff: copy.WriteOff(); break;
        }

        return copy;
    }

    // ---------- читання: AsNoTracking ----------

    public IReadOnlyList<BookCopy> List() =>
        _db.Copies
            .AsNoTracking()
            .Include(c => c.Book)
            .OrderBy(c => c.Id)          // сортування виконує SQLite (LINQ to Entities)
            .AsEnumerable()              // далі — у пам'яті (LINQ to Objects)
            .Select(ToDomain)
            .ToList();

    public BookCopy? GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;

        var row = _db.Copies
            .AsNoTracking()
            .Include(c => c.Book)
            .SingleOrDefault(c => c.Id == id);

        return row is null ? null : ToDomain(row);
    }

    // ---------- запис: SaveChanges ----------

    public void Add(BookCopy item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (_db.Copies.Any(c => c.Id == item.Id))
            throw new InvalidOperationException($"Примірник з id={item.Id} уже існує.");

        var book = FindOrCreateBook(item);
        _db.Copies.Add(new CopyRow { Id = item.Id, Status = item.Status, BookId = book.Id, Book = book });

        Save();
    }

    public void Update(BookCopy item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var row = _db.Copies.Include(c => c.Book).SingleOrDefault(c => c.Id == item.Id);
        if (row is null)
        {
            // як у InMemory/File сховищах: Update неіснуючого = додавання
            Add(item);
            return;
        }

        var oldBook = row.Book;

        if (oldBook.Isbn == item.Isbn)
        {
            // той самий ISBN — оновлюємо дані книги (діє на всі її примірники)
            oldBook.Title = item.Title;
            oldBook.Author = item.Author;
            oldBook.Year = item.Year;
        }
        else
        {
            // ISBN змінили — примірник переходить до іншої книги
            var newBook = FindOrCreateBook(item);
            row.Book = newBook;
            row.BookId = newBook.Id;

            // стара книга без примірників не потрібна
            if (!_db.Copies.Any(c => c.BookId == oldBook.Id && c.Id != row.Id))
                _db.Books.Remove(oldBook);
        }

        row.Status = item.Status;
        Save();
    }

    public bool Remove(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;

        var row = _db.Copies.Include(c => c.Book).SingleOrDefault(c => c.Id == id);
        if (row is null) return false;

        var book = row.Book;
        _db.Copies.Remove(row);

        // останній примірник — прибираємо і книгу, щоб не лишалось «порожніх» записів
        if (!_db.Copies.Any(c => c.BookId == book.Id && c.Id != row.Id))
            _db.Books.Remove(book);

        Save();
        return true;
    }

    // ---------- допоміжне ----------

    // Книга з таким ISBN є → дані мають збігатися; немає → створюємо нову.
    private BookRow FindOrCreateBook(BookCopy item)
    {
        var book = _db.Books.SingleOrDefault(b => b.Isbn == item.Isbn);

        if (book is null)
        {
            book = new BookRow
            {
                Id = Guid.NewGuid().ToString("N"),   // 32 символи = HasMaxLength(32)
                Isbn = item.Isbn,
                Title = item.Title,
                Author = item.Author,
                Year = item.Year
            };
            _db.Books.Add(book);
            return book;
        }

        if (book.Title != item.Title || book.Author != item.Author || book.Year != item.Year)
            throw new DomainException(
                $"Книга з ISBN {item.Isbn} вже є в каталозі як «{book.Title}» " +
                $"({book.Author ?? "автор не вказаний"}, {book.Year}). " +
                "Дані нового примірника мають збігатися з наявною книгою.");

        return book;
    }

    // UNIQUE / FK від SQLite → керована доменна помилка (409), а не 500
    private void Save()
    {
        try
        {
            _db.SaveChanges();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 })
        {
            throw new DomainException(
                "Не вдалося зберегти: порушено обмеження бази даних (такий ISBN або id уже існує).");
        }
    }
}