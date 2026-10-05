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
            // Формуємо безпечний патерн із екрануванням спецсимволів
            var pattern = "%" + EscapeLike(q ?? "") + "%";

            // Безпечний параметризований пошук через LINQ та ILike
            var query = db.Incidents.AsNoTracking()
                .Where(i => EF.Functions.ILike(i.Title, pattern, "\\") ||
                            EF.Functions.ILike(i.Description, pattern, "\\"));

            // allowlist для сортування
            IQueryable<Incident>? orderedQuery = sortBy switch
            {
                null or "" or "createdAtUtc" => query.OrderByDescending(i => i.CreatedAtUtc).ThenBy(i => i.Id),
                "severity" => query.OrderBy(i =>
                    i.Severity == IncidentSeverity.Critical ? 0 :
                    i.Severity == IncidentSeverity.High ? 1 :
                    i.Severity == IncidentSeverity.Medium ? 2 : 3)
                    .ThenBy(i => i.Id),
                "status" => query.OrderBy(i =>
                    i.Status == IncidentStatus.New ? 0 :
                    i.Status == IncidentStatus.Triaged ? 1 :
                    i.Status == IncidentStatus.InProgress ? 2 :
                    i.Status == IncidentStatus.Resolved ? 3 : 4)
                    .ThenBy(i => i.Id),
                _ => null
            };

            // Відхиляємо запит, якщо передали невідоме сортування
            if (orderedQuery == null)
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["sortBy"] = ["Допустимі значення: createdAtUtc, severity, status."]
                });
            }

            //Виконуємо запит і мапимо результат у наш DTO
            var items = await orderedQuery
                .Take(50)
                .Select(row => new IncidentListItemResponse(
                    row.Id, row.Title, row.Description,
                    row.Severity.ToString(), row.Status.ToString(), row.CreatedAtUtc))
                .ToListAsync(ct);

            return Results.Ok(items);
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


    public sealed record CreateIncidentRequest(
        string? Title, string? Description, string? Severity, DateTimeOffset? OccurredAtUtc);
    public sealed record CreatedIncidentResponse(
        Guid Id, string Title, string Description, string Severity, string Status, DateTimeOffset OccurredAtUtc, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);
    // Eкранування спецсимволів
    private static string EscapeLike(string value) =>
            value.Replace("\\", "\\\\")
                 .Replace("%", "\\%")
                 .Replace("_", "\\_");
    }
    // DTO для відповіді пошуку
    public sealed record IncidentListItemResponse(
    Guid Id, string Title, string Description, string Severity, string Status, DateTimeOffset CreatedAtUtc);
