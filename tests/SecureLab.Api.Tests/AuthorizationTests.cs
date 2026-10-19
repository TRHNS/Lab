using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SecureLab.Api.Data;

namespace SecureLab.Api.Tests;

public class AuthorizationTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    private static string Password =>
        Environment.GetEnvironmentVariable("SeedUsers__Password")
        ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

    // читаємо JSON з відповіді
    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(text).RootElement;
    }

    private static async Task<Guid> CreateIncident(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/incidents", new
        {
            title = "Test-" + Guid.NewGuid(),
            description = "Тестовий інцидент",
            severity = "Low",
            occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        var json = await ReadJson(response);
        return json.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task A02_AliceReadsOwnIncident()
    {
        using var alice = await factory.LoginAsync("alice", Password);

        var response = await alice.GetAsync($"/api/incidents/{DbSeeder.AliceIncidentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A03_A10_AliceReadsBobIncident_Returns404()
    {
        using var alice = await factory.LoginAsync("alice", Password);

        var bob = await alice.GetAsync($"/api/incidents/{DbSeeder.BobIncidentId}");
        var unknown = await alice.GetAsync($"/api/incidents/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, bob.StatusCode);

        // відповідь для чужого і неіснуючого id однакова
        var bobJson = await ReadJson(bob);
        var unknownJson = await ReadJson(unknown);
        Assert.Equal(unknownJson.GetProperty("title").GetString(), bobJson.GetProperty("title").GetString());
        Assert.Equal(unknown.Content.Headers.ContentType?.MediaType, bob.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task A03R_AnalystReadsAliceIncident()
    {
        using var morgan = await factory.LoginAsync("morgan", Password);

        var response = await morgan.GetAsync($"/api/incidents/{DbSeeder.AliceIncidentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A04_AliceCannotUpdateBobIncident()
    {
        using var alice = await factory.LoginAsync("alice", Password);

        var response = await alice.PutAsJsonAsync($"/api/incidents/{DbSeeder.BobIncidentId}", new
        {
            title = "Змінено Alice",
            description = "Спроба змінити чужий інцидент",
            occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        // перевіряємо, що у Bob нічого не змінилося
        using var bob = await factory.LoginAsync("bob", Password);
        var json = await ReadJson(await bob.GetAsync($"/api/incidents/{DbSeeder.BobIncidentId}"));
        Assert.NotEqual("Змінено Alice", json.GetProperty("title").GetString());
    }

    [Fact]
    public async Task A05_AliceSeesOnlyOwnIncidents()
    {
        using var alice = await factory.LoginAsync("alice", Password);

        var text = await alice.GetStringAsync("/api/incidents");

        Assert.Contains(DbSeeder.AliceIncidentId.ToString(), text);
        Assert.DoesNotContain(DbSeeder.BobIncidentId.ToString(), text);
    }

    [Fact]
    public async Task A06_ReporterCannotTriage()
    {
        using var alice = await factory.LoginAsync("alice", Password);
        var id = await CreateIncident(alice);

        var response = await alice.PostAsync($"/api/incidents/{id}/triage", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        // статус має лишитися New
        var json = await ReadJson(await alice.GetAsync($"/api/incidents/{id}"));
        Assert.Equal("New", json.GetProperty("status").GetString());
    }

    [Fact]
    public async Task A07_AnalystCanTriageNewIncident()
    {
        using var alice = await factory.LoginAsync("alice", Password);
        var id = await CreateIncident(alice);
        using var morgan = await factory.LoginAsync("morgan", Password);

        var response = await morgan.PostAsync($"/api/incidents/{id}/triage", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await ReadJson(await morgan.GetAsync($"/api/incidents/{id}"));
        Assert.Equal("Triaged", json.GetProperty("status").GetString());

        // другий раз вже не можна
        var again = await morgan.PostAsync($"/api/incidents/{id}/triage", null);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task A12_AliceCannotReadBobOrInternalComment()
    {
        using var alice = await factory.LoginAsync("alice", Password);

        var bobComment = await alice.GetAsync("/api/incident-comments/30000000-0000-0000-0000-000000000002");
        var internalComment = await alice.GetAsync("/api/incident-comments/30000000-0000-0000-0000-000000000003");

        Assert.Equal(HttpStatusCode.NotFound, bobComment.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, internalComment.StatusCode);

        // internal коментар не видно і в деталях інциденту
        var details = await alice.GetStringAsync($"/api/incidents/{DbSeeder.AliceIncidentId}");
        Assert.DoesNotContain("30000000-0000-0000-0000-000000000003", details);
    }
}