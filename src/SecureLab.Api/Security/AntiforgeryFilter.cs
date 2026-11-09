using Microsoft.AspNetCore.Antiforgery;

namespace SecureLab.Api.Security;

// ЛР 6: єдине місце explicit-валідації antiforgery для cookie-authenticated mutation endpoints.
// Підключається до кожного маршруту через .AddEndpointFilter<AntiforgeryFilter>().
public sealed class AntiforgeryFilter(IAntiforgery antiforgery) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            // Перевірка пари: antiforgery cookie + request token у header X-CSRF-TOKEN
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            // Ранній return: handler не виконується, стан не змінюється
            return Results.Problem(
                title: "Antiforgery token is missing or invalid.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return await next(context);
    }
}
