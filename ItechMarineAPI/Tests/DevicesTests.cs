// Tests/DevicesTests.cs
using FluentAssertions;
using System.Net;
using Xunit;

namespace ItechMarineAPI.Tests;

public class DevicesTests : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _client;
    public DevicesTests(CustomWebAppFactory f) => _client = f.CreateClient();

    [Fact]
    public async Task RotateKey_Without_Token_Unauthorized()
    {
        var res = await _client.PostAsync($"/devices/{Guid.NewGuid()}/rotate-key", null);
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
