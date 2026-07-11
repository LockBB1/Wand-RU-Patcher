using System.Net;
using System.Net.Http;
using System.Text;
using WandRuInstaller.Core;
using Xunit;

namespace WandRuInstaller.Tests;

public class UpdateCheckerTests
{
    [Theory]
    [InlineData("v0.12.0", "0.11.0", true)]
    [InlineData("0.12.0", "0.11.0", true)]
    [InlineData("v0.11.0", "0.11.0", false)]
    [InlineData("v0.10.9", "0.11.0", false)]
    [InlineData("v1.0.0", "0.11.0+34", true)]  // InformationalVersion с +build
    [InlineData("garbage", "0.11.0", false)]
    public void IsNewer_compares_semver_tags(string tag, string current, bool expected)
        => Assert.Equal(expected, UpdateChecker.IsNewer(tag, current));

    [Fact]
    public async Task CheckAsync_returns_tag_when_newer()
    {
        var handler = new FakeHandler("""{"tag_name":"v9.9.9","html_url":"x"}""");
        Assert.Equal("9.9.9", await UpdateChecker.CheckAsync("0.11.0", handler));
    }

    [Fact]
    public async Task CheckAsync_returns_null_when_same_or_error()
    {
        Assert.Null(await UpdateChecker.CheckAsync("9.9.9", new FakeHandler("""{"tag_name":"v9.9.9"}""")));
        Assert.Null(await UpdateChecker.CheckAsync("0.11.0", new FakeHandler("not json")));
    }

    sealed class FakeHandler(string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
    }
}
