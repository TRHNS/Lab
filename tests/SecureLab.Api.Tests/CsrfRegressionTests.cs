using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SecureLab.Api.Data;

namespace SecureLab.Api.Tests;

// ЛР 6: REG-CSRF-01 і перевірка всього cookie-периметра
public sealed class CsrfRegressionTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private static string Password =>
        Environment.GetEnvironmentVariable("SeedUsers__Password")
        ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

    private static object NewIncident(string title) => new
    {
        title,
        description = "REG-CSRF-01",
        severity = "Low",
        occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
    };

    private static async Task<bool> ListContainsAsync(HttpClient client, string title) =>
        (await client.GetStringAsync("/api/incidents")).Contains(title);

    
    [Fact]
    public async Task RegCsrf01_CreateWithoutOrInvalidToken_Rejected_ThenValidPairSucceeds()
    {
        using var alice = await factory.LoginAsync("alice", Password);
        var title = $"CSRF-{Guid.NewGuid():N}";

        // Auth-cookie є, request token немає → 400, інцидент не створено
        using var noToken = await alice.PostAsJsonAsync("/api/incidents", NewIncident(title));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        Assert.Equal("application/problem+json", noToken.Content.Headers.ContentType?.MediaType);
        Assert.False(await ListContainsAsync(alice, title));

        // Невалідне значення токена → той самий 400, стан не змінено
        using var invalid = new HttpRequestMessage(HttpMethod.Post, "/api/incidents")
        {
            Content = JsonContent.Create(NewIncident(title))
        };
        invalid.Headers.Add(AntiforgeryTestExtensions.HeaderName, "not-a-valid-token");
        using var invalidResponse = await alice.SendAsync(invalid);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        Assert.False(await ListContainsAsync(alice, title));

        // Валідна пара токенів тим самим клієнтом → 201, інцидент з'явився
        await alice.UseAntiforgeryTokenAsync();
        using var accepted = await alice.PostAsJsonAsync("/api/incidents", NewIncident(title));
        Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        Assert.True(await ListContainsAsync(alice, title));
    }

    // Токен одного користувача не підходить іншому
    [Fact]
    public async Task TokenOfAlice_IsRejectedForBob()
    {
        using var alice = await factory.LoginAsync("alice", Password);
        await alice.UseAntiforgeryTokenAsync();
        var aliceToken = alice.DefaultRequestHeaders.GetValues(AntiforgeryTestExtensions.HeaderName).Single();

        using var bob = await factory.LoginAsync("bob", Password);
        var title = $"CSRF-X-{Guid.NewGuid():N}";
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/incidents")
        {
            Content = JsonContent.Create(NewIncident(title))
        };
        request.Headers.Add(AntiforgeryTestExtensions.HeaderName, aliceToken);
        using var response = await bob.SendAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await ListContainsAsync(bob, title));
    }

    // SC-02 … SC-05: без токена → той самий 400, ресурс/сеанс не змінено
    [Fact]
    public async Task Perimeter_WithoutToken_Returns400_AndStateUnchanged()
    {
        using var alice = await factory.LoginAsync("alice", Password);
        var id = DbSeeder.AliceIncidentId;
        var before = await alice.GetStringAsync($"/api/incidents/{id}");

        // SC-02 PUT
        using var put = await alice.PutAsJsonAsync($"/api/incidents/{id}", new
        {
            title = "CSRF changed", description = "CSRF changed",
            occurredAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1)
        });
        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);

        // SC-03 DELETE
        using var delete = await alice.DeleteAsync($"/api/incidents/{id}");
        Assert.Equal(HttpStatusCode.BadRequest, delete.StatusCode);

        // SC-04 triage (Analyst)
        using var morgan = await factory.LoginAsync("morgan", Password);
        using var triage = await morgan.PostAsync($"/api/incidents/{id}/triage", null);
        Assert.Equal(HttpStatusCode.BadRequest, triage.StatusCode);

        // Ресурс не змінився: ні title, ні статус, не видалено
        var after = await alice.GetStringAsync($"/api/incidents/{id}");
        Assert.Equal(before, after);

        // SC-05 logout без токена → 400, сеанс лишився активним
        using var logout = await alice.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.BadRequest, logout.StatusCode);
        using var me = await alice.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }
}
