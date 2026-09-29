using System.Text.Json;
using Core.Dto;

namespace Core.Import;

public static class BookJsonImporter
{
    public static ImportResult<BookDto> Load(string path)
    {
        var errors = new List<string>();
        
        try
        {
            string json = File.ReadAllText(path);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var raw = JsonSerializer.Deserialize<List<BookDto>>(json, options) ?? [];

            var items = new List<BookDto>();
            for (int i = 0; i < raw.Count; i++)
            {
                BookDto book = raw[i];

                string? error = book switch
                {
                    { Isbn: null or "" } or { Title: null or "" }
                        => "ISBN або назва книги порожні",
                    { Year: var y } when y < 1450 || y > DateTime.Now.Year
                        => $"рік '{y}' поза допустимими межами",
                    _ => null
                };

                if (error is null)
                    items.Add(book);
                else
                    errors.Add($"запис {i + 1} ({book.Id}): {error}");
            }

            return new ImportResult<BookDto>(items, errors);
        }
        catch (Exception ex)
        {
            errors.Add($"Помилка читання JSON: {ex.Message}");
            return new ImportResult<BookDto>([], errors);
        }
    }
}