using System.Net;
using System.Net.Http.Json;
using SecureLab.Api.Scaffolding;

namespace SecureLab.Api.Tests;


public class IncidentCreationTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    // Пароль береться лише зі змінної середовища, не з коду
    private static string Password =>
        Environment.GetEnvironmentVariable("SeedUsers__Password")
        ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

    [Fact]
    public async Task T02_ValidationFails_Returns400()
    {
        using var client = await factory.LoginAsync("alice", Password);

        var request = new Lab02Endpoints.CreateIncidentRequest(
            Title: "Check T-02",
            Description: "Перевірка валідації",
            Severity: "InvalidSeverity123",              // невалідний severity
            OccurredAtUtc: DateTimeOffset.UtcNow.AddDays(5) // дата в майбутньому
        );

        var response = await client.PostAsJsonAsync("/api/incidents", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task T03_DuplicateTitle_Returns409()
    {
        using var client = await factory.LoginAsync("alice", Password);
        var title = $"Regression-{Guid.NewGuid():N}";

        var request = new Lab02Endpoints.CreateIncidentRequest(
            Title: title,
            Description: "Цей опис є достатньо довгим (більше 40 символів), щоб пройти cross-field перевірку.",
            Severity: "Critical",
            OccurredAtUtc: DateTimeOffset.UtcNow.AddMinutes(-2)
        );

        var first = await client.PostAsJsonAsync("/api/incidents", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/incidents", request);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }
}