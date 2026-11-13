using Api.Contracts;
using Core.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Api.Validation;

namespace Api.Endpoints;

public static class BookEndpoints
{   
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public static void MapBooks(this WebApplication app)
    {
        var group = app.MapGroup("/api/books").WithTags("Books");

                // GET /api/books?page=1&pageSize=20&search=...&author=...
        group.MapGet("/", Results<Ok<PagedResponse<BookResponse>>, ValidationProblem>
            (string? search, string? author, int? page, int? pageSize, LendingService service) =>
        {
            var currentPage = page ?? 1;
            var size = pageSize ?? DefaultPageSize;

            var errors = new Dictionary<string, string[]>();
            if (currentPage < 1)
                errors["page"] = ["Номер сторінки має бути від 1."];
            if (size < 1 || size > MaxPageSize)
                errors["pageSize"] = [$"Розмір сторінки має бути від 1 до {MaxPageSize}."];
            if (errors.Count > 0)
                return TypedResults.ValidationProblem(errors);

            var text = search?.Trim();
            var authorPart = author?.Trim();

            // search — у назві, авторі або ISBN; author — лише в авторі
            var matched = service.Search(c =>
                (string.IsNullOrEmpty(text)
                    || c.Title.Contains(text, StringComparison.OrdinalIgnoreCase)
                    || c.Isbn.Contains(text, StringComparison.OrdinalIgnoreCase)
                    || (c.Author?.Contains(text, StringComparison.OrdinalIgnoreCase) ?? false))
                && (string.IsNullOrEmpty(authorPart)
                    || (c.Author?.Contains(authorPart, StringComparison.OrdinalIgnoreCase) ?? false)));

            // long — щоб page=999999999 не переповнив int, а просто дав порожню сторінку
            var skip = (int)Math.Min((long)(currentPage - 1) * size, int.MaxValue);

            var items = matched
                .OrderBy(c => c.Id, StringComparer.Ordinal)   // стабільний порядок між сторінками
                .Skip(skip)
                .Take(size)
                .Select(c => c.ToResponse())
                .ToList();

            return TypedResults.Ok(new PagedResponse<BookResponse>(items, matched.Count, currentPage, size));
        });

        // GET /api/books/{id}
        group.MapGet("/{id}", Results<Ok<BookResponse>, NotFound>
            (string id, LendingService service) =>
                service.Find(id) is { } copy
                    ? TypedResults.Ok(copy.ToResponse())
                    : TypedResults.NotFound());

                // POST /api/books — валідацію виконує фільтр до виклику обробника
        group.MapPost("/", (SaveBookRequest request, LendingService service) =>
        {
            var created = service.AddBook(request.Isbn, request.Title, request.Year, request.Author);
            return TypedResults.Created($"/api/books/{created.Id}", created.ToResponse());
        })
        .AddEndpointFilter<ValidationFilter<SaveBookRequest>>()
        .ProducesValidationProblem();

        // PUT /api/books/{id}
        group.MapPut("/{id}", Results<Ok<BookResponse>, NotFound>
            (string id, SaveBookRequest request, LendingService service) =>
                service.UpdateBook(id, request.Isbn, request.Title, request.Year, request.Author) is { } updated
                    ? TypedResults.Ok(updated.ToResponse())
                    : TypedResults.NotFound())
        .AddEndpointFilter<ValidationFilter<SaveBookRequest>>()
        .ProducesValidationProblem();

        // DELETE /api/books/{id}
        group.MapDelete("/{id}", Results<NoContent, NotFound>
            (string id, LendingService service) =>
                service.RemoveBook(id)
                    ? TypedResults.NoContent()
                    : TypedResults.NotFound());
    }
}