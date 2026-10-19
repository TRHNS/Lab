using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;
using SecureLab.Api.Presentation.Contracts;

namespace SecureLab.Api.Application.Incidents;

public sealed class IncidentQueries(SecureLabDbContext dbContext, ILogger<IncidentQueries> logger)
{
    public async Task<IReadOnlyList<IncidentListItemResponse>> GetListAsync(
        IncidentStatus? status,
        Guid userId,
        bool isAnalyst,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Loading incidents with status filter {Status}", status);

        // Analyst бачить усі, інші лише власні 
        var query = dbContext.Incidents
            .AsNoTracking()
            .Where(incident => isAnalyst || incident.OwnerUserId == userId);

        if (status is not null)
        {
            query = query.Where(incident => incident.Status == status);
        }

        return await query
            .OrderByDescending(incident => incident.CreatedAtUtc)
            .Select(incident => new IncidentListItemResponse(
                incident.Id,
                incident.Title,
                incident.Severity.ToString(),
                incident.Status.ToString(),
                incident.OccurredAtUtc,
                incident.CreatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    public async Task<IncidentSeveritySummaryResponse> GetSeveritySummaryAsync(
    IReadOnlyList<IncidentStatus>? statuses,
    CancellationToken cancellationToken)
    {
        logger.LogInformation("Loading severity summary for statuses: {Statuses}", statuses);

        var query = dbContext.Incidents.AsNoTracking();

        if (statuses is not null && statuses.Count > 0)
        {
            query = query.Where(incident => statuses.Contains(incident.Status));
        }

        
        var items = await query
            .GroupBy(incident => incident.Severity)
            .Select(group => new IncidentSeveritySummaryItem(
                group.Key.ToString(),
                group.Count()))
            .ToListAsync(cancellationToken);

       
        var sortedItems = items
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Severity, StringComparer.Ordinal)
            .ToList();

        foreach (var item in sortedItems)
        {
            logger.LogInformation(
                "Критичність {Severity}: {Count} інцидент(ів)",
                item.Severity,
                item.Count);
        }

        return new IncidentSeveritySummaryResponse(sortedItems);
    }

    public Task<IncidentDetailsResponse?> GetDetailsAsync(
        Guid id,
        Guid userId,
        bool isAnalyst,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Loading incident {IncidentId}", id);

        return dbContext.Incidents
            .AsNoTracking()
            // id має збігтися завжди; доступ — Analyst або власник
            .Where(incident => incident.Id == id
                && (isAnalyst || incident.OwnerUserId == userId))
            .Select(incident => new IncidentDetailsResponse(
                incident.Id,
                incident.Title,
                incident.Description,
                incident.Severity.ToString(),
                incident.Status.ToString(),
                incident.OccurredAtUtc,
                incident.CreatedAtUtc,
                incident.Owner.DisplayName,
                incident.Comments
                    // internal comment бачить лише Analyst (правило матриці)
                    .Where(comment => isAnalyst || !comment.IsInternal)
                    .OrderBy(comment => comment.CreatedAtUtc)
                    .Select(comment => new IncidentCommentResponse(
                        comment.Id,
                        comment.Author.DisplayName,
                        comment.Text,
                        comment.CreatedAtUtc))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken);
    }
    // child resource — доступ лише через право на parent incident
    public Task<IncidentCommentResponse?> GetCommentAsync(
        Guid id,
        Guid userId,
        bool isAnalyst,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Loading comment {CommentId}", id);

        return dbContext.IncidentComments
            .AsNoTracking()
            // Analyst — будь-який коментар; інші — лише не internal і лише до власного інциденту
            .Where(comment => comment.Id == id
                && (isAnalyst
                    || (comment.Incident.OwnerUserId == userId && !comment.IsInternal)))
            .Select(comment => new IncidentCommentResponse(
                comment.Id,
                comment.Author.DisplayName,
                comment.Text,
                comment.CreatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);
    }
}