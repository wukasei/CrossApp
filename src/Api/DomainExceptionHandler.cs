using Core;
using Microsoft.AspNetCore.Diagnostics;

namespace Api;

// Окремий обробник для порушень правил бібліотеки.
// Дані запиту коректні, але операція суперечить поточному стану ресурсу → 409 Conflict.
public sealed class DomainExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException domain)
            return false;   // не наш виняток — передаємо наступному обробнику

        // Це очікувана ситуація, а не збій сервера, тому Information, а не Error
        logger.LogInformation("Відхилено за правилом бібліотеки: {Message}", domain.Message);

        context.Response.StatusCode = StatusCodes.Status409Conflict;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails =
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Порушено правило бібліотеки",
                Detail = domain.Message
            }
        });
    }
}