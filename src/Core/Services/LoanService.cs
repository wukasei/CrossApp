using Core.Domain;

namespace Core.Services;

public static class LoanService
{
    public const int MaxOpenLoansPerReader = 5;

    public static Loan OpenLoan(string loanId, BookCopy copy, string readerId, DateOnly issuedOn, IEnumerable<Loan> existingLoans)
    {
        if (existingLoans is null)
            throw new ArgumentNullException(nameof(existingLoans), "Не передано список видач");

        int openCount = existingLoans.Count(l => l.ReaderId == readerId?.Trim() && !l.IsClosed);

        if (openCount >= MaxOpenLoansPerReader)
            throw new InvalidOperationException(
                $"Читач {readerId} вже має {openCount} відкритих видач (максимум {MaxOpenLoansPerReader}), нову видачу оформити неможливо");

        return Loan.Open(loanId, copy, readerId, issuedOn);
    }
}