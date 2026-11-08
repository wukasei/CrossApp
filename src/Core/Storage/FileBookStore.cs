using System.Text.Encodings.Web;
using System.Text.Json;
using Core.Abstractions;
using Core.Domain;
using Core.Dto;

namespace Core.Storage;

// Шлях приходить ззовні (з конфігурації через StoreFactory), у класі шляхів немає.
public sealed class FileBookStore(string path) : IBookStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,     // у файлі: id, isbn, title...
        PropertyNameCaseInsensitive = true,                    // старий файл з Id, Isbn теж читається
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping  // кирилиця без \u0426
    };

    private readonly Dictionary<string, BookCopy> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _path = Path.GetFullPath(path);
    private bool _loaded;

    // читає файл один раз, при першому зверненні
    private void EnsureLoaded()
    {
        if (_loaded) return;
        if (File.Exists(_path))
        {
            List<BookDto> dtos;
            try
            {
                dtos = JsonSerializer.Deserialize<List<BookDto>>(File.ReadAllText(_path), Options) ?? [];
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"Файл даних {_path} пошкоджено: {ex.Message}", ex);
            }

            _cache.Clear();
            foreach (var dto in dtos)
            {
                try
                {
                    var copy = BookCopy.FromDto(dto);   // повторно перевіряє інваріанти
                    _cache[copy.Id] = copy;
                }
                catch (ArgumentException ex)
                {
                    throw new InvalidDataException(
                        $"Файл даних {_path} містить некоректний запис {dto?.Id}: {ex.Message}", ex);
                }
            }
        }
        _loaded = true;
    }

    // атомарний запис: спершу .tmp, потім підміна одним File.Move
    private void Flush() => Save(_path, _cache.Values);

    // атомарний запис колекції у файл: використовують і сховище, і команда export
    public static void Save(string path, IEnumerable<BookCopy> copies)
    {
        var full = Path.GetFullPath(path);
        var dtos = copies.Select(c => c.ToDto()).ToList();
        var tmp = full + ".tmp";

        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(tmp, JsonSerializer.Serialize(dtos, Options));
        File.Move(tmp, full, overwrite: true);
    }

    public IReadOnlyList<BookCopy> List()
    {
        EnsureLoaded();
        return _cache.Values.ToList();
    }

    public BookCopy? GetById(string id)
    {
        EnsureLoaded();
        return _cache.GetValueOrDefault(id);
    }

    public void Add(BookCopy item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureLoaded();
        if (_cache.ContainsKey(item.Id))
            throw new InvalidOperationException($"Примірник з id={item.Id} уже існує.");
        _cache.Add(item.Id, item);
        Flush();
    }

    public void Update(BookCopy item)
    {
        ArgumentNullException.ThrowIfNull(item);
        EnsureLoaded();
        _cache[item.Id] = item;
        Flush();
    }

    public bool Remove(string id)
    {
        EnsureLoaded();
        if (!_cache.Remove(id)) return false;
        Flush();
        return true;
    }
}