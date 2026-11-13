using System.Net;
using System.Net.Http.Json;
using Api.Contracts;
using Core.Abstractions;
using Core.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

public sealed class BooksApiTests
{
    // Кожен тест отримує власний сервер з порожнім in-memory сховищем:
    // тести не залежать від SampleData, файлу даних чи змінних середовища і не впливають один на одного
    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.AddSingleton<IBookStore>(new SynchronizedBookStore(new InMemoryBookStore()))));

    private static readonly SaveBookRequest ValidBook =
        new("978-617-585-111-2", "Intermezzo", "Михайло Коцюбинський", 2020);

    [Fact]
    public async Task Post_ValidBook_Returns201_AndLocationReturnsSameBook()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var post = await client.PostAsJsonAsync("/api/books", ValidBook);

        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        Assert.NotNull(post.Headers.Location);

        var created = await post.Content.ReadFromJsonAsync<BookResponse>();
        Assert.NotNull(created);
        Assert.Equal("Intermezzo", created.Title);
        Assert.Equal("available", created.Status);

        // Іменно за адресою з заголовка Location, а не за власноруч зібраним шляхом
        var get = await client.GetAsync(post.Headers.Location);

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var fetched = await get.Content.ReadFromJsonAsync<BookResponse>();
        Assert.Equal(created, fetched);   // record порівнюється за значеннями всіх полів
    }

    [Fact]
    public async Task Post_InvalidBook_Returns400_WithFieldErrors()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var post = await client.PostAsJsonAsync("/api/books", new SaveBookRequest("", "", null, 1200));

        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
        var problem = await post.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Contains("Isbn", problem.Errors.Keys);
        Assert.Contains("Title", problem.Errors.Keys);
        Assert.Contains("Year", problem.Errors.Keys);
    }

    [Fact]
    public async Task Delete_ThenGet_Returns404()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var post = await client.PostAsJsonAsync("/api/books", ValidBook);
        var location = post.Headers.Location;

        var delete = await client.DeleteAsync(location);
        var get = await client.GetAsync(location);

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }
}