using Microsoft.AspNetCore.Mvc;
using SecureLab.Api.Application.Incidents;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;
using System.Security.Claims;
using SecureLab.Api.Data;

namespace SecureLab.Api.Presentation.Endpoints;

public static class IncidentEndpoints
{
    public static IEndpointRouteBuilder MapIncidentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/incidents")
            .WithTags("Incidents")
            .RequireAuthorization();   // Анонім отримує 401

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


        // дочірній ресурс — коментарі
        var comments = endpoints.MapGroup("/api/incident-comments")
            .WithTags("IncidentComments")
            .RequireAuthorization();

        comments.MapGet("/{id:guid}", GetCommentAsync)
            .WithName("GetIncidentComment")
            .Produces<IncidentCommentResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }

    private static async Task<IResult> GetListAsync(
        string? status,
        IncidentQueries queries,
        HttpContext httpContext,
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

        if (!Guid.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Results.Challenge();
        var isAnalyst = httpContext.User.IsInRole(DbSeeder.AnalystRole);

        return Results.Ok(await queries.GetListAsync(parsedStatus, userId, isAnalyst, cancellationToken));

    }

    private static async Task<IResult> GetDetailsAsync(
         Guid id,
         HttpContext httpContext,
         IncidentQueries queries,
         CancellationToken cancellationToken)
    {
        // user id і роль лише з verified principal
        if (!Guid.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Results.Challenge();
        var isAnalyst = httpContext.User.IsInRole(DbSeeder.AnalystRole);

        var incident = await queries.GetDetailsAsync(id, userId, isAnalyst, cancellationToken);
        // Політика 404: чужий і неіснуючий id дають однакову відповідь
        return incident is null ? IncidentNotFound() : Results.Ok(incident);
    }

    // helper для 404, без id у detail
    internal static IResult IncidentNotFound() => Results.Problem(
        title: "Інцидент не знайдено",
        statusCode: StatusCodes.Status404NotFound);

    private static async Task<IResult> GetCommentAsync(
       Guid id,
       HttpContext httpContext,
       IncidentQueries queries,
       CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Results.Challenge();
        var isAnalyst = httpContext.User.IsInRole(DbSeeder.AnalystRole);

        var comment = await queries.GetCommentAsync(id, userId, isAnalyst, cancellationToken);
        // чужий, internal і неіснуючий коментар — однакова 404
        return comment is null ? CommentNotFound() : Results.Ok(comment);
    }

    internal static IResult CommentNotFound() => Results.Problem(
        title: "Коментар не знайдено",
        statusCode: StatusCodes.Status404NotFound);

    // ДОДАНО: метод-обробник для ендпоінта severity-summary, який приймає параметри, валідує їх та звертається до application-шару

    private static async Task<IResult> GetSeveritySummaryAsync(
    string? status,
    IncidentQueries queries,
    CancellationToken cancellationToken)
    {
        var parsedStatuses = new List<IncidentStatus>();

        if (!string.IsNullOrWhiteSpace(status))
        {
            foreach (var part in status.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Enum.TryParse<IncidentStatus>(part, ignoreCase: true, out var parsed)
                    || !Enum.IsDefined(parsed))
                {
                    return Results.ValidationProblem(new Dictionary<string, string[]>
                    {
                        ["status"] = ["Допустимі значення: New, Triaged, InProgress, Resolved, Closed."]
                    });
                }

                parsedStatuses.Add(parsed);
            }
        }

        return Results.Ok(await queries.GetSeveritySummaryAsync(parsedStatuses, cancellationToken));
    }
}