using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SecureLab.Api.Tests;

public sealed class SearchMechanicsTests(SecureLabApiFactory factory) : IClassFixture<SecureLabApiFactory>
{
    [Fact]
    public async Task Search_ReturnsJson()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/incidents/search?q=навчальн");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task S02_SqlPayload_IsTreatedAsText_AndSortByIsAllowlisted()
    {
        using var host = factory.WithWebHostBuilder(_ => { });   // окремий лічильник rate limit
        using var client = host.CreateClient();

        // Позитив: порожній пошук повертає seed-інциденти
        var all = await client.GetFromJsonAsync<List<JsonElement>>("/api/incidents/search?q=");
        Assert.NotNull(all);
        Assert.NotEmpty(all);

        // Негатив: класичний payload не повертає «всі рядки» — він шукається як буквальний текст
        var payload = Uri.EscapeDataString("' OR '1'='1");
        var injected = await client.GetFromJsonAsync<List<JsonElement>>($"/api/incidents/search?q={payload}");
        Assert.NotNull(injected);
        Assert.Empty(injected);

        // Негатив: довільний sortBy не потрапляє в ORDER BY — 400 від allowlist
        var sort = Uri.EscapeDataString("title; DROP TABLE incidents");
        using var badSort = await client.GetAsync($"/api/incidents/search?sortBy={sort}");
        Assert.Equal(HttpStatusCode.BadRequest, badSort.StatusCode);
    }
}
