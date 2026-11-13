namespace Api.Contracts;

// Те, що API віддає клієнту. Статус рядком, бо в домені три стани, а не два.
public sealed record BookResponse(string Id, string Isbn, string Title, string? Author, int Year, string Status);