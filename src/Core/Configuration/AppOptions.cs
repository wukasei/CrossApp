namespace Core.Configuration;

// Назви властивостей збігаються з ключами appsettings.json,
// тому config.Get<AppOptions>() розкладає їх автоматично.
public sealed class AppOptions
{
    public string EnvironmentName { get; init; } = "Development";
    public LoggingOptions Logging { get; init; } = new();
    public StorageOptions Storage { get; init; } = new();
    public LibraryOptions Library { get; init; } = new();
}

public sealed class LoggingOptions
{
    public string MinimumLevel { get; init; } = "Information";
}

public sealed class StorageOptions
{
    public string DataPath { get; init; } = "data";
    public string FileName { get; init; } = "catalog.json";

    // Абсолютний DataPath береться як є, відносний — від baseDir
    public string ResolveFullPath(string baseDir)
    {
        var root = Path.IsPathRooted(DataPath) ? DataPath : Path.Combine(baseDir, DataPath);
        Directory.CreateDirectory(root);   // якщо data/ видалили — створиться заново
        return Path.GetFullPath(Path.Combine(root, FileName));
    }
}

// Ключ варіанту «Бібліотека»: Library:LoanDays
public sealed class LibraryOptions
{
    public int LoanDays { get; init; } = 14;
}