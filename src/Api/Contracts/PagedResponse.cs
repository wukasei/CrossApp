namespace Api.Contracts;

// Одна сторінка результатів. Total — скільки записів підходить під фільтр усього,
// щоб клієнт міг порахувати кількість сторінок.
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);