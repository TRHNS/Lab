using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SecureLab.Api.Data;

namespace SecureLab.Api.Tests;


public sealed class ProductionProfileTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private const string StudyConnectionString =
        "Host=127.0.0.1;Port=54329;Database=securelab;Username=securelab;"
        + "Password=local-study-password;Include Error Detail=false";

    private static string Password =>
        Environment.GetEnvironmentVariable("SeedUsers__Password")
        ?? throw new InvalidOperationException("Set local SeedUsers__Password outside Git.");

    [Fact]
    public async Task Production_ExpectedErrors_Keep4xxSemantics()
    {
        using var production = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:SecureLab", StudyConnectionString);
        });

        
        Assert.Equal("Production",
            production.Services.GetRequiredService<IHostEnvironment>().EnvironmentName);

        // Анонім: 401 без redirect
        using var anonymous = production.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        using var anonymousResponse = await anonymous.GetAsync("/api/incidents");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);

        // Вхід через той самий Production-host; спершу дозволений GET
        using var alice = await production.LoginAsync("alice", Password);
        using var sessionCheck = await alice.GetAsync("/api/auth/session-check");
        Assert.Equal(HttpStatusCode.NoContent, sessionCheck.StatusCode);

        
        using var invalidFilter = await alice.GetAsync("/api/incidents?status=Unknown");
        Assert.Equal(HttpStatusCode.BadRequest, invalidFilter.StatusCode);
        Assert.Equal("application/problem+json", invalidFilter.Content.Headers.ContentType?.MediaType);

        
        using var missing = await alice.GetAsync(
            "/api/incidents/99999999-9999-9999-9999-999999999999");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal("application/problem+json", missing.Content.Headers.ContentType?.MediaType);

       
        using var foreign = await alice.GetAsync($"/api/incidents/{DbSeeder.BobIncidentId}");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);

        
        using var triage = await alice.PostAsync(
            $"/api/incidents/{DbSeeder.AliceIncidentId}/triage", null);
        Assert.Equal(HttpStatusCode.Forbidden, triage.StatusCode);
    }
}
