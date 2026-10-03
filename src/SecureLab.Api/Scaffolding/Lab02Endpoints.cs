using Microsoft.EntityFrameworkCore;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Scaffolding;

// Навчальний старт ЛР 02. Запускати лише з локальними штучними даними.
public static class Lab02Endpoints
{
    public static void MapLab02Endpoints(this WebApplication app)
    {
        app.MapGet("/api/incidents/search", async (string? q, string? sortBy, SecureLabDbContext db, CancellationToken ct) =>
        {
            var order = sortBy switch
            {
                null or "" or "createdAtUtc" => "created_at_utc DESC",
                "severity" => "severity", "status" => "status", _ => sortBy
            };
            var sql = "SELECT * FROM incidents WHERE title ILIKE '%" + (q ?? "")
                + "%' OR description ILIKE '%" + (q ?? "") + "%' ORDER BY " + order + " LIMIT 50";
            var rows = await db.Incidents.FromSqlRaw(sql).AsNoTracking().ToListAsync(ct);
            return Results.Ok(rows.Select(row => new
            {
                row.Id, row.Title, row.Description,
                Severity = row.Severity.ToString(), Status = row.Status.ToString(), row.CreatedAtUtc
            }));
        });
        app.MapPost("/api/incidents", async (CreateIncidentRequest request, SecureLabDbContext db, CancellationToken ct) =>
        {
            var now = DateTimeOffset.UtcNow;
            var errors = new Dictionary<string, string[]>();

            // Базові перевірки полів контракту
            if (string.IsNullOrWhiteSpace(request.Title))
                errors["title"] = ["Заголовок обов'язковий."];
            else if (request.Title.Length > 160)
                errors["title"] = ["Заголовок не може бути довшим за 160 символів."];

            if (string.IsNullOrWhiteSpace(request.Description))
                errors["description"] = ["Опис обов'язковий."];
            else if (request.Description.Length > 4000)
                errors["description"] = ["Опис не може бути довшим за 4000 символів."];

            bool isSeverityOk = Enum.TryParse<IncidentSeverity>(request.Severity, true, out var severity)
                                && Enum.IsDefined(severity);
            if (!isSeverityOk)
                errors["severity"] = ["Допустимі значення: Low, Medium, High, Critical."];

            if (request.OccurredAtUtc == null)
                errors["occurredAtUtc"] = ["Час виникнення обов'язковий."];
            else if (request.OccurredAtUtc > now.AddMinutes(5))
                errors["occurredAtUtc"] = ["Час виникнення не може бути в майбутньому."];

            string title = request.Title?.Trim() ?? "";
            string desc = request.Description?.Trim() ?? "";

            // Cross-field правило
            if (isSeverityOk && (severity == IncidentSeverity.High || severity == IncidentSeverity.Critical))
            {
                if (!errors.ContainsKey("description") && desc.Length < 40)
                {
                    errors["description"] = ["Опис має містити щонайменше 40 символів."];
                }
            }

            if (errors.Count > 0)
                return Results.ValidationProblem(errors);

            // Перевірка на предметний конфлікт 
            bool isDuplicate = await db.Incidents.AnyAsync(x =>
                x.Title == title &&
                (x.Status == IncidentStatus.New || x.Status == IncidentStatus.Triaged ||
                 x.Status == IncidentStatus.InProgress || x.Status == IncidentStatus.Resolved), ct);

            if (isDuplicate)
                return Results.Problem(statusCode: 409, title: "Конфлікт", detail: "Інцидент з таким заголовком вже існує.");

            // Збереження в БД та формування response DTO
            var incident = new Incident
            {
                Id = Guid.NewGuid(),
                Title = title,
                Description = desc,
                Severity = severity,
                OccurredAtUtc = request.OccurredAtUtc!.Value.ToUniversalTime(),
                Status = IncidentStatus.New,
                OwnerUserId = DbSeeder.AliceId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };

            db.Incidents.Add(incident);
            await db.SaveChangesAsync(ct);

            var response = new CreatedIncidentResponse(
                incident.Id, incident.Title, incident.Description,
                incident.Severity.ToString(), incident.Status.ToString(),
                incident.OccurredAtUtc, incident.CreatedAtUtc, incident.UpdatedAtUtc);

            return Results.Created($"/api/incidents/{incident.Id}", response);
        });
    }
}

public sealed record CreateIncidentRequest(
    string? Title, string? Description, string? Severity, DateTimeOffset? OccurredAtUtc);

public sealed record CreatedIncidentResponse(
    Guid Id, string Title, string Description, string Severity, string Status, DateTimeOffset OccurredAtUtc, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);