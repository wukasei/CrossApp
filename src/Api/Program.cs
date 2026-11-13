using System.Text.Encodings.Web;
using Api;
using Api.Contracts;
using Api.Endpoints;
using Api.Validation;
using Core;
using Core.Abstractions;
using Core.Configuration;
using Core.Services;
using Core.Storage;
using FluentValidation;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Ті самі змінні середовища, що й у Cli: CROSSAPP_Storage__Kind, CROSSAPP_Storage__DataPath...
builder.Configuration.AddEnvironmentVariables(prefix: "CROSSAPP_");

// Кирилиця в JSON-відповідях без \u0406...
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping);

// Генератор OpenAPI-документа (пакет Microsoft.AspNetCore.OpenApi)
builder.Services.AddOpenApi();

// Помилки у форматі RFC 9457 і власне перетворення винятків на коди.
// Обробники викликаються по черзі в порядку реєстрації, доки якийсь не поверне true
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<DomainExceptionHandler>();   // DomainException → 409
builder.Services.AddExceptionHandler<ApiExceptionHandler>();      // некоректний запит → 400

// Валідатори без стану, тому один екземпляр на весь застосунок
builder.Services.AddSingleton<IValidator<SaveBookRequest>, SaveBookRequestValidator>();

// Сховище: вибір з конфігурації, один екземпляр на весь застосунок (Singleton),
// обгорнутий у lock, бо запити обробляються паралельно
var storage = builder.Configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();
var storageKind = builder.Configuration["Storage:Kind"] ?? "memory";

builder.Services.AddSingleton<IBookStore>(_ =>
{
    IBookStore inner = storageKind.Equals("file", StringComparison.OrdinalIgnoreCase)
        ? new FileBookStore(storage.ResolveFullPath(AppContext.BaseDirectory))
        : new InMemoryBookStore(SampleData.Books());
    return new SynchronizedBookStore(inner);
});

// Сервіс теж Singleton: він тримає лише посилання на сховище і подію Changed
builder.Services.AddSingleton<LendingService>();

var app = builder.Build();

// Першими в конвеєрі: перехоплюють винятки з усього, що нижче
app.UseExceptionHandler();      // виняток → ProblemDetails без stack trace
app.UseStatusCodePages();       // порожні 404/400 → теж ProblemDetails

// Документацію API показуємо лише під час розробки
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();               // /openapi/v1.json
    app.MapScalarApiReference();    // /scalar/v1
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapBooks();

app.Run();

// Робить клас Program доступним для WebApplicationFactory<Program> у tests/Api.Tests
public partial class Program;