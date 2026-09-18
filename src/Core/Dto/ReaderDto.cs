namespace Core.Dto;

public record ReaderDto(
    string Id,
    string FullName,
    string Phone,
    string? Email = null
);