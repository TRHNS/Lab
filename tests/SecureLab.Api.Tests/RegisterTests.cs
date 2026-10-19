using System.Net;
using System.Net.Http.Json;

namespace SecureLab.Api.Tests;

public sealed class RegisterTests(SecureLabApiFactory factory)
    : IClassFixture<SecureLabApiFactory>
{
    private const string GoodPassword = "Reg-Test-1234!x";

    private static string NewName() => "reg" + Guid.NewGuid().ToString("N")[..8];

    [Fact]
    public async Task Register_DuplicateUserName_Returns409()
    {
        using var client = factory.CreateClient();
        var name = NewName();

        using var first = await client.PostAsJsonAsync("/api/auth/register", new
        {
            userName = name,
            email = name + "@example.test",
            displayName = "Студент",
            password = GoodPassword
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var second = await client.PostAsJsonAsync("/api/auth/register", new
        {
            userName = name,
            email = NewName() + "@example.test",
            displayName = "Студент",
            password = GoodPassword
        });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        using var client = factory.CreateClient();
        var name = NewName();
        var email = name + "@example.test";

        using var first = await client.PostAsJsonAsync("/api/auth/register", new
        {
            userName = name,
            email,
            displayName = "Студент",
            password = GoodPassword
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        using var second = await client.PostAsJsonAsync("/api/auth/register", new
        {
            userName = NewName(),
            email,
            displayName = "Студент",
            password = GoodPassword
        });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task register_alice_upper()
    {
        var client = factory.CreateClient();
        var password = Environment.GetEnvironmentVariable("SeedUsers__Password");
        var resonse = await client.PostAsJsonAsync("/api/auth/register", new
        {
            userName = "ALICE",
            email = NewName() + "@test.com",
            displayName = "Test",
            password
        });
        Assert.Equal(HttpStatusCode.Conflict, resonse.StatusCode);
    }
}