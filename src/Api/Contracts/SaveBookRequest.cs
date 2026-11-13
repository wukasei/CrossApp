namespace Api.Contracts;

// Те, що клієнт надсилає в POST і PUT. Id і статусу тут немає: їх задає сервер.
// Правила перевірки — у Validation/SaveBookRequestValidator.
public sealed record SaveBookRequest(string Isbn, string Title, string? Author, int Year);