using Core.Domain;

namespace Api.Contracts;

public static class BookMapping
{
    public static BookResponse ToResponse(this BookCopy copy) =>
        new(copy.Id, copy.Isbn, copy.Title, copy.Author, copy.Year, ToStatus(copy.Status));

    private static string ToStatus(CopyStatus status) => status switch
    {
        CopyStatus.Available  => "available",
        CopyStatus.Issued     => "issued",
        CopyStatus.WrittenOff => "writtenOff",
        _ => status.ToString()
    };
}