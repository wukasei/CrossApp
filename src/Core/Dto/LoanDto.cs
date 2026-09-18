namespace Core.Dto;

public record LoanDto(
    string Id,
    string BookCopyId,
    string ReaderId,
    string IssueDate,
    string? ReturnDate = null
);