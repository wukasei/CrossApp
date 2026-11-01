using System.Text.Json;
using Core.Abstractions;
using Core.Domain;
using Core.Dto;
using System.Runtime.CompilerServices;

namespace Core.Storage;

public sealed class JsonBookImporter : IBookImporter, IBookStreamImporter
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public async Task<IReadOnlyList<BookCopy>> LoadAsync(string path, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ct.ThrowIfCancellationRequested();   // уже скасовано — файл навіть не відкриваємо

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                                                FileShare.Read, bufferSize: 4096, useAsync: true);

        var dtos = await JsonSerializer.DeserializeAsync<List<BookDto?>>(stream, Options, ct)
                   ?? throw new DomainException($"Файл {Path.GetFileName(path)} не містить списку книжок.");

        var result = new List<BookCopy>(dtos.Count);
        for (var i = 0; i < dtos.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                result.Add(BookCopy.FromDto(dtos[i]!));
            }
            catch (ArgumentException ex)   // сюди потрапляють і ArgumentNullException, і ArgumentOutOfRangeException
            {
                throw new DomainException($"Запис {i + 1}: {ex.Message}", ex);
            }
        }

        return result;
    }

        // Додаткове 1: потокове читання — записи віддаються по одному, не чекаючи кінця файлу
    public async IAsyncEnumerable<BookCopy> StreamAsync(string path,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ct.ThrowIfCancellationRequested();

        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                                                FileShare.Read, bufferSize: 4096, useAsync: true);

        var index = 0;
        await foreach (var dto in JsonSerializer.DeserializeAsyncEnumerable<BookDto?>(stream, Options, ct))
        {
            index++;
            ct.ThrowIfCancellationRequested();

            BookCopy copy;
            try
            {
                copy = BookCopy.FromDto(dto!);
            }
            catch (ArgumentException ex)
            {
                throw new DomainException($"Запис {index}: {ex.Message}", ex);
            }

            yield return copy;   // віддаємо один запис і чекаємо, поки попросять наступний
        }
    }
}