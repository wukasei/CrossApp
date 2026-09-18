using Core.Dto; 
using Core.Import; 

string path = args.Length > 0 ? args[0] : Path.Combine("data", "sample.csv");

if (!File.Exists(path))
{
    Console.WriteLine($"Файл не знайдено: {Path.GetFullPath(path)}");
    return 1; 
}

string extension = Path.GetExtension(path).ToLowerInvariant();
ImportResult<BookDto> result = extension switch
{
    ".csv" => BookCsvImporter.Load(path),
    ".json" => BookJsonImporter.Load(path),
    _ => throw new Exception($"Формат файлу {extension} не підтримується")
};

Console.WriteLine($"Завантажено записів: {result.Items.Count}");

foreach (BookDto book in result.Items.Take(5))
{
    Console.WriteLine($"  {book.Id,-6} {book.Isbn,-15} {book.Year,5}  {book.Title}");
}

if (result.Errors.Count > 0)
{
    Console.WriteLine($"Пропущено рядків: {result.Errors.Count}");
    
    foreach (string errorMsg in result.Errors)
    {
        Console.WriteLine($"  ! {errorMsg}");
    }
}

int accepted = result.Items.Count;
int skipped = result.Errors.Count;
int total = accepted + skipped;

double errorRate = total > 0 ? ((double)skipped / total) * 100 : 0;

Console.WriteLine(new string('-', 50));
Console.WriteLine($"Статистика: усього {total} | прийнято {accepted} | пропущено {skipped} | помилок {errorRate:F1}%");

return 0; 

