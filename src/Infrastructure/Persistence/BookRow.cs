namespace Infrastructure.Persistence;

// Модель зберігання книги (видання). Це НЕ доменний клас:
// публічні сетери і навігація потрібні лише EF Core.
public sealed class BookRow
{
    public string Id { get; set; } = "";       // string-ключ
    public string Isbn { get; set; } = "";     // унікальний (індекс у AppDbContext)
    public string Title { get; set; } = "";
    public string? Author { get; set; }
    public int Year { get; set; }

    // 1:N — одна книга має багато примірників
    public List<CopyRow> Copies { get; set; } = [];
}