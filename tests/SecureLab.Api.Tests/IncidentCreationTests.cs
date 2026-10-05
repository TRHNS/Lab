using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using SecureLab.Api.Scaffolding;
using Xunit;

namespace SecureLab.Api.Tests;

// Клас для автоматичних регресійних тестів створення інцидентів
public class IncidentCreationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public IncidentCreationTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task T02_ValidationFails_Returns400()
    {
        // Звертаємося до DTO через клас Lab02Endpoints
        var request = new Lab02Endpoints.CreateIncidentRequest(
            Title: "Check T-02",
            Description: "Перевірка валідації",
            Severity: "InvalidSeverity123", // Невалідне значення Enum
            OccurredAtUtc: DateTimeOffset.UtcNow.AddDays(5) // Невалідна дата
        );

        // Act
        var response = await _client.PostAsJsonAsync("/api/incidents", request);

        // Assert: Очікуємо 400 Bad Request
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task T03_DuplicateTitle_Returns409()
    {
        var title = $"Regression-{Guid.NewGuid():N}";

        // Звертаємося до DTO через клас Lab02Endpoints
        var request = new Lab02Endpoints.CreateIncidentRequest(
            Title: title,
            Description: "Цей опис є достатньо довгим (більше 40 символів), щоб пройти cross-field перевірку.",
            Severity: "Critical",
            OccurredAtUtc: DateTimeOffset.UtcNow.AddMinutes(-2)
        );

        // Act 1: Створюємо інцидент вперше
        var response1 = await _client.PostAsJsonAsync("/api/incidents", request);

        // Assert 1: Має успішно створитися (201 Created)
        Assert.Equal(HttpStatusCode.Created, response1.StatusCode);

        // Act 2: Робимо повторний запит з тими самими даними
        var response2 = await _client.PostAsJsonAsync("/api/incidents", request);

        // Assert 2: Має повернути предметний конфлікт
        Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);
    }
}