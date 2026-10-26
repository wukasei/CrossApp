namespace Core.Reports;

public sealed record LoanLine(string LoanId, string CopyId, string Title, string ReaderId,
                              DateOnly IssuedOn, DateOnly? ReturnedOn);