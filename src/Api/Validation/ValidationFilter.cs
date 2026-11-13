using FluentValidation;

namespace Api.Validation;

// Фільтр маршруту: знаходить серед аргументів обробника об'єкт типу T,
// перевіряє його валідатором і за помилок повертає 400, не викликаючи обробник
public sealed class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter where T : class
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var argument = context.Arguments.OfType<T>().FirstOrDefault();
        if (argument is null)
            return await next(context);

        var result = await validator.ValidateAsync(argument, context.HttpContext.RequestAborted);
        if (result.IsValid)
            return await next(context);

        // Той самий формат, що й раніше: поле → масив повідомлень
        var errors = result.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        return TypedResults.ValidationProblem(errors);
    }
}