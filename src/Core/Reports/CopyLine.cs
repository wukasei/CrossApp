using Core.Domain;

namespace Core.Reports;

public sealed record CopyLine(string Id, string Title, string? Author, int Year, CopyStatus Status);