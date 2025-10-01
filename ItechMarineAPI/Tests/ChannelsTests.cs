// Tests/ChannelsTests.cs
using FluentAssertions;
using System.Net;
using Xunit;

namespace ItechMarineAPI.Tests;

public class ChannelsTests : IClassFixture<CustomWebAppFactory>
{
    private readonly HttpClient _client;
    public ChannelsTests(CustomWebAppFactory f) => _client = f.CreateClient();

    [Fact]
    public async Task CreateChannel_Without_Token_Should_Unauthorized()
    {
        var res = await _client.PostAsJsonAsync("/boats/channels", new { name = "X", type = 0, pin = 1 });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
