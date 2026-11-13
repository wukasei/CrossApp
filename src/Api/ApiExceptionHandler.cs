using Microsoft.AspNetCore.Diagnostics;

namespace Api;

// Помилки самого запиту: тіло не розібралося або дані порушують інваріант домену.
// Доменні правила (409) обробляє DomainExceptionHandler, усе інше стане загальним 500.
public sealed class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            // Тіло запиту не розібралося: кривий JSON або "year":"abc"
            BadHttpRequestException bad =>
                (bad.StatusCode, "Некоректний запит", "Тіло запиту не вдалося прочитати як JSON потрібного формату."),

            // Інваріант домену, який валідація DTO не відловила
            ArgumentException =>
                (StatusCodes.Status400BadRequest, "Некоректні дані", exception.Message),

            _ => (0, null, null)
        };

        if (status == 0)
            return false;   // невідомий виняток — нехай буде загальний 500

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = { Status = status, Title = title, Detail = detail }
        });
    }
}