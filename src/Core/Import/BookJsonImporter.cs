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
            
            var items = JsonSerializer.Deserialize<List<BookDto>>(json, options) ?? [];
            
            return new ImportResult<BookDto>(items, errors);
        }
        catch (Exception ex)
        {
            errors.Add($"Помилка читання JSON: {ex.Message}");
            return new ImportResult<BookDto>([], errors);
        }
    }
}