using System.Net.Http.Json;
using System.Collections.Generic;
using Xunit;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.EntityFrameworkCore;
using SDM.Infrastructure.Data;

namespace SDM.IntegrationTests;

public class EnrollmentFlowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public EnrollmentFlowTests(WebApplicationFactory<Program> factory)
    {
        // Customize factory to use in-memory DB and disable Hangfire
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((ctx, conf) =>
            {
                conf.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Hangfire:Enabled"] = "false"
                });
            });

            builder.ConfigureServices(services =>
            {
                // Replace EF DbContext with InMemory DB for tests
                services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
                services.AddDbContext<ApplicationDbContext>(options =>
                {
                    options.UseInMemoryDatabase("SDM_TestDb");
                });
            });
        });
    }

    [Fact]
    public async Task Enrollment_EndToEnd_ReturnsDeviceJwt()
    {
        var client = _factory.CreateClient();

        // 1) Create an enrollment token via the anonymous QR test endpoint
        var createTokenReq = new { ExpiresInMinutes = 30, MaxDevices = 1 };
        var tokenResp = await client.PostAsJsonAsync("/api/enrollment/tokens/generate-qr/test", createTokenReq);
        tokenResp.EnsureSuccessStatusCode();
        var html = await tokenResp.Content.ReadAsStringAsync();

        // Extract token from HTML: <p>Token: <strong>{token}</strong></p>
        var m = Regex.Match(html, @"Token:\s*<strong>([A-Za-z0-9\-]+)</strong>", RegexOptions.IgnoreCase);
        Assert.True(m.Success, "Could not find token in create-qr/test response HTML");
        var token = m.Groups[1].Value;

        // 2) Register device with token
        var registerReq = new
        {
            Token = token,
            DeviceIdentifier = Guid.NewGuid().ToString(),
            SerialNumber = "TEST-SN",
            Manufacturer = "TestCorp",
            Model = "TC-1",
            AndroidVersion = "14"
        };

        var regResp = await client.PostAsJsonAsync("/api/devices/register-with-token", registerReq);
        regResp.EnsureSuccessStatusCode();

        var body = await regResp.Content.ReadFromJsonAsync<DeviceRegisterResponse?>();
        Assert.NotNull(body);
        Assert.NotEqual(Guid.Empty, body!.DeviceId);
        Assert.False(string.IsNullOrWhiteSpace(body.DeviceJwt));
    }

    private class DeviceRegisterResponse
    {
        public Guid DeviceId { get; set; }
        public string DeviceJwt { get; set; } = string.Empty;
        public int ExpiresInSeconds { get; set; }
    }
}
