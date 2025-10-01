// Tests/AuthTests.cs
using System.Net.Http.Json;
using FluentAssertions;
using Xunit;

namespace ItechMarineAPI.Tests;

public class AuthTests : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _client;
    public AuthTests(CustomWebAppFactory f) => _client = f.CreateClient();

    [Fact]
    public async Task Register_And_Login_Should_Return_Tokens()
    {
        var reg = await _client.PostAsJsonAsync("/auth/register-owner", new
        {
            email = "t@t.com",
            password = "DemoPass!123",
            boatName = "TestBoat"
        });
        reg.EnsureSuccessStatusCode();
        var r1 = await reg.Content.ReadFromJsonAsync<dynamic>();
        ((string)r1!.token).Should().NotBeNullOrEmpty();

        var login = await _client.PostAsJsonAsync("/auth/login", new
        {
            email = "t@t.com",
            password = "DemoPass!123"
        });
        login.EnsureSuccessStatusCode();
        var r2 = await login.Content.ReadFromJsonAsync<dynamic>();
        ((string)r2!.token).Should().NotBeNullOrEmpty();
    }
}
