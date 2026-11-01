using Core.Domain;

namespace Core.Abstractions;

public interface IBookStreamImporter
{
    IAsyncEnumerable<BookCopy> StreamAsync(string path, CancellationToken ct = default);
}