namespace Core.Domain;
using System.Globalization;
using Core.Dto;

public sealed class Loan
{
    public string Id { get; }
    public string BookCopyId { get; }
    public string ReaderId { get; }
    public DateOnly IssuedOn { get; }

    // null = книжку ще не повернули
    public DateOnly? ReturnedOn { get; private set; }

    public bool IsClosed => ReturnedOn is not null;

    private Loan(string id, string bookCopyId, string readerId, DateOnly issuedOn, DateOnly? returnedOn)
    {
        Id = id;
        BookCopyId = bookCopyId;
        ReaderId = readerId;
        IssuedOn = issuedOn;
        ReturnedOn = returnedOn;
    }

    public static Loan Open(string id, BookCopy copy, string readerId, DateOnly issuedOn)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Ідентифікатор видачі обов'язковий", nameof(id));

        if (copy is null)
            throw new ArgumentNullException(nameof(copy), "Не вказано примірник для видачі");

        if (string.IsNullOrWhiteSpace(readerId))
            throw new ArgumentException("Ідентифікатор читача обов'язковий", nameof(readerId));

        copy.Issue(); // якщо примірник уже виданий — тут вилетить помилка, і видача не створиться

        return new Loan(id.Trim(), copy.Id, readerId.Trim(), issuedOn, returnedOn: null);
    }

    public void Close(BookCopy copy, DateOnly returnedOn)
    {
        if (copy is null)
            throw new ArgumentNullException(nameof(copy), "Не вказано примірник, який повертають");

        if (IsClosed)
            throw new InvalidOperationException(
                $"Видача {Id} вже закрита {ReturnedOn:dd.MM.yyyy}, повторне закриття неможливе");

        if (copy.Id != BookCopyId)
            throw new InvalidOperationException(
                $"Видача {Id} стосується примірника {BookCopyId}, а повертають {copy.Id}");

        if (returnedOn < IssuedOn)
            throw new ArgumentOutOfRangeException(nameof(returnedOn), returnedOn.ToString("dd.MM.yyyy"),
                $"Дата повернення не може бути раніше дати видачі ({IssuedOn:dd.MM.yyyy})");

        copy.Return();
        ReturnedOn = returnedOn;
    }

    public LoanDto ToDto() => new(
        Id,
        BookCopyId,
        ReaderId,
        IssuedOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ReturnedOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
    );

    public static Loan FromDto(LoanDto dto)
    {
        if (dto is null)
            throw new ArgumentNullException(nameof(dto));

        if (string.IsNullOrWhiteSpace(dto.Id))
            throw new ArgumentException("Ідентифікатор видачі обов'язковий", nameof(dto.Id));

        if (string.IsNullOrWhiteSpace(dto.BookCopyId))
            throw new ArgumentException("Ідентифікатор примірника обов'язковий", nameof(dto.BookCopyId));

        if (string.IsNullOrWhiteSpace(dto.ReaderId))
            throw new ArgumentException("Ідентифікатор читача обов'язковий", nameof(dto.ReaderId));

        DateOnly issuedOn = ParseDate(dto.IssueDate, nameof(dto.IssueDate));
        DateOnly? returnedOn = dto.ReturnDate is null ? null : ParseDate(dto.ReturnDate, nameof(dto.ReturnDate));

        if (returnedOn < issuedOn)
            throw new ArgumentOutOfRangeException(nameof(dto.ReturnDate), dto.ReturnDate,
                $"Дата повернення не може бути раніше дати видачі ({issuedOn:dd.MM.yyyy})");

        return new Loan(dto.Id.Trim(), dto.BookCopyId.Trim(), dto.ReaderId.Trim(), issuedOn, returnedOn);
    }

    private static DateOnly ParseDate(string? text, string paramName)
    {
        if (!DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
            throw new ArgumentException($"Некоректна дата: «{text}»", paramName);

        return date;
    }

    public override string ToString() =>
    ReturnedOn is DateOnly returned
        ? $"Видача {Id}: примірник {BookCopyId}, читач {ReaderId}, {IssuedOn:dd.MM.yyyy} → {returned:dd.MM.yyyy}"
        : $"Видача {Id}: примірник {BookCopyId}, читач {ReaderId}, {IssuedOn:dd.MM.yyyy} → не повернено";
}