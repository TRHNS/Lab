using System.Net;
using SecureLab.Api.Data;

namespace SecureLab.Api.Tests;

// ЛР 6: REG-RL-02 — rate limiting для GET /api/incidents/search
public sealed class SearchRateLimitTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private static string Password =>
        Environment.GetEnvironmentVariable("SeedUsers__Password")
        ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

    private const string SearchUrl = "/api/incidents/search?q=навчальн";

    [Fact]
    public async Task Search_QuotaPerUser_Returns429_OtherUserNotBlocked_ThenRecovers()
    {
        // Окремий host — власний лічильник, щоб інші тести не впливали
        using var host = factory.WithWebHostBuilder(_ => { });
        using var alice = await host.LoginAsync("alice", Password);
        using var bob = await host.LoginAsync("bob", Password);

        // У межах quota пошук працює
        for (var i = 0; i < 5; i++)
        {
            using var ok = await alice.GetAsync(SearchUrl);
            Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        }

        // 2. Наступний запит alice вичерпує quota → 429
        using var limited = await alice.GetAsync(SearchUrl);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);

        // Інший partition має власний лічильник
        using var bobSearch = await bob.GetAsync(SearchUrl);
        Assert.Equal(HttpStatusCode.OK, bobSearch.StatusCode);

        // Ліміт не змінює authorization: інші маршрути alice працюють як раніше, чужий інцидент лишається недоступним
        using var own = await alice.GetAsync($"/api/incidents/{DbSeeder.AliceIncidentId}");
        Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        using var foreign = await alice.GetAsync($"/api/incidents/{DbSeeder.BobIncidentId}");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);

        // Після вікна пошук для alice відновлюється
        await Task.Delay(TimeSpan.FromSeconds(11));
        using var recovered = await alice.GetAsync(SearchUrl);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
    }
}
