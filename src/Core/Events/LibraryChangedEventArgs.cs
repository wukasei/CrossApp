namespace Core.Events;

public sealed class LibraryChangedEventArgs(
    LibraryChangeKind kind, string isbn, string? copyId,
    string? readerId, DateOnly? dueDate) : EventArgs
{
    public LibraryChangeKind Kind { get; } = kind;
    public string Isbn { get; } = isbn;
    public string? CopyId { get; } = copyId;
    public string? ReaderId { get; } = readerId;
    public DateOnly? DueDate { get; } = dueDate;
    public DateTimeOffset At { get; } = DateTimeOffset.Now;
}