using Microsoft.AspNetCore.Mvc;
using SecureLab.Api.Application.Incidents;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Presentation.Endpoints;

public static class IncidentEndpoints
{
    public static IEndpointRouteBuilder MapIncidentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/incidents")
            .WithTags("Incidents");

        group.MapGet("/", GetListAsync)
            .WithName("GetIncidents")
            .Produces<IReadOnlyList<IncidentListItemResponse>>()
            .ProducesValidationProblem();

        group.MapGet("/{id:guid}", GetDetailsAsync)
            .WithName("GetIncidentDetails")
            .Produces<IncidentDetailsResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

      
        // ЗМІНЕНО: замість дефолтної заглушки 501 Not Implemented підключено метод обробки severity-summary
       
        group.MapGet("/severity-summary", GetSeveritySummaryAsync)
            .WithName("GetIncidentSeveritySummary")
            .Produces<IncidentSeveritySummaryResponse>();

        return endpoints;
    }

    private static async Task<IResult> GetListAsync(
        string? status,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        IncidentStatus? parsedStatus = null;
        if (status is not null)
        {
            if (!Enum.TryParse<IncidentStatus>(status, ignoreCase: true, out var candidate)
                || !Enum.IsDefined(candidate))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["status"] = ["Допустимі значення: New, Triaged, InProgress, Resolved, Closed."]
                });
            }

            parsedStatus = candidate;
        }

        return Results.Ok(await queries.GetListAsync(parsedStatus, cancellationToken));
    }

    private static async Task<IResult> GetDetailsAsync(
        Guid id,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        var incident = await queries.GetDetailsAsync(id, cancellationToken);
        return incident is null
            ? Results.Problem(
                title: "Інцидент не знайдено",
                detail: $"Інцидент '{id}' не існує.",
                statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(incident);
    }

  
    // ДОДАНО: метод-обробник для ендпоінта severity-summary, який приймає параметри, валідує їх та звертається до application-шару
    
    private static async Task<IResult> GetSeveritySummaryAsync(
        [FromQuery] string[]? status,
        IncidentQueries queries,
        CancellationToken cancellationToken)
    {
        IncidentStatus[]? parsedStatuses = null;
        if (status is not null && status.Length > 0)
        {
            var list = new List<IncidentStatus>();
            foreach (var s in status)
            {
                // Перевірка та безпечне перетворення статусів з урахуванням регістру
                if (!Enum.TryParse<IncidentStatus>(s, ignoreCase: true, out var parsed) || !Enum.IsDefined(parsed))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["status"] = ["Допустимі значення: New, Triaged, InProgress, Resolved, Closed."]
                    });
                }
                list.Add(parsed);
            }
            parsedStatuses = list.ToArray();
        }

        var result = await queries.GetSeveritySummaryAsync(parsedStatuses, cancellationToken);
        return Results.Ok(result);
    }
}
