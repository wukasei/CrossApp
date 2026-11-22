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
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Ті самі змінні середовища, що й у Cli: CROSSAPP_Storage__Kind, CROSSAPP_ConnectionStrings__AppDb...
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

// ---------- Сховище ----------
// Лаба 11: за замовчуванням SQLite через EF Core. memory/file лишились з тижнів 9–10.
var storage = builder.Configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();
var storageKind = builder.Configuration["Storage:Kind"] ?? "sqlite";
var useSqlite = storageKind.Equals("sqlite", StringComparison.OrdinalIgnoreCase);

if (useSqlite)
{
    // Рядок підключення з ConnectionStrings:AppDb; відносний шлях — від ContentRootPath
    var appDbConnectionString = SqlitePath.Resolve(
        builder.Configuration.GetConnectionString("AppDb"),
        builder.Environment.ContentRootPath);

    // AddDbContext реєструє AppDbContext як Scoped: окремий екземпляр на кожен HTTP-запит
    builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(appDbConnectionString));

    // Сховище теж Scoped — живе стільки ж, скільки його DbContext. lock не потрібен.
    builder.Services.AddScoped<IBookStore, EfBookStore>();
}
else
{
    // memory / file: один екземпляр на застосунок, обгорнутий у lock
    builder.Services.AddSingleton<IBookStore>(_ =>
    {
        IBookStore inner = storageKind.Equals("file", StringComparison.OrdinalIgnoreCase)
            ? new FileBookStore(storage.ResolveFullPath(AppContext.BaseDirectory))
            : new InMemoryBookStore(SampleData.Books());
        return new SynchronizedBookStore(inner);
    });
}

// Сервіс тепер Scoped: Singleton не може залежати від Scoped-сховища (captive dependency)
builder.Services.AddScoped<LendingService>();

var app = builder.Build();

// Під час розробки застосовуємо міграції автоматично; у Production — командою dotnet ef database update
if (useSqlite && app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

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