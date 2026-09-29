using Core.Dto;

namespace Core.Import;

public static class LibraryMixedImporter
{
    public sealed record MixedImportResult(
        IReadOnlyList<BookDto> Books,
        IReadOnlyList<ReaderDto> Readers,
        IReadOnlyList<string> Errors
    );

    public static MixedImportResult Load(string path)
    {
        var books = new List<BookDto>();
        var readers = new List<ReaderDto>();
        var errors = new List<string>();

        string[] lines = File.ReadAllLines(path);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;

            switch (ParseLine(line))
            {
                case ParseBookOk b:
                    books.Add(b.Book);
                    break;
                case ParseReaderOk r:
                    readers.Add(r.Reader);
                    break;
                case ParseFailed f:
                    errors.Add($"рядок {i + 1}: {f.Reason}");
                    break;
            }
        }

        return new MixedImportResult(books, readers, errors);
    }

    private static ParseOutcome ParseLine(string line)
    {
        string[] parts = line.Split(';', StringSplitOptions.TrimEntries);

        return parts switch
        {
            ["B", var id, var isbn, var title, var year] when int.TryParse(year, out int y)
                => new ParseBookOk(new BookDto(id, isbn, title, y)),
                
            ["R", var id, var name, var phone]
                => new ParseReaderOk(new ReaderDto(id, name, phone)),
                
            _ => new ParseFailed("невідомий префікс або пошкоджений рядок")
        };
    }

    private abstract record ParseOutcome;
    private sealed record ParseBookOk(BookDto Book) : ParseOutcome;
    private sealed record ParseReaderOk(ReaderDto Reader) : ParseOutcome;
    private sealed record ParseFailed(string Reason) : ParseOutcome;
}