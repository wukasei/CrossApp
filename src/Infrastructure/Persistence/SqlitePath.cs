using Microsoft.Data.Sqlite;

namespace Infrastructure.Persistence;

// Перетворює рядок підключення з конфігурації на робочий:
// відносний шлях до файлу БД → абсолютний від ContentRootPath (а не від поточного каталогу),
// каталог для файлу створюється, якщо його ще немає.
public static class SqlitePath
{
    public const string DefaultConnectionString = "Data Source=data/app.db";

    public static string Resolve(string? rawConnectionString, string contentRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        var csb = new SqliteConnectionStringBuilder(
            string.IsNullOrWhiteSpace(rawConnectionString) ? DefaultConnectionString : rawConnectionString);

        if (string.IsNullOrWhiteSpace(csb.DataSource))
            throw new InvalidOperationException("У ConnectionStrings:AppDb не вказано Data Source.");

        // БД у пам'яті (для тестів) — файлу немає, шлях не потрібен
        if (csb.DataSource == ":memory:" || csb.Mode == SqliteOpenMode.Memory)
            return csb.ConnectionString;

        // Path.Combine + GetFullPath: однаково працює у Windows, Linux і macOS
        var fullPath = Path.IsPathRooted(csb.DataSource)
            ? Path.GetFullPath(csb.DataSource)
            : Path.GetFullPath(Path.Combine(contentRootPath, csb.DataSource));

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);   // data/ видалили — створиться

        csb.DataSource = fullPath;
        return csb.ConnectionString;
    }
}