using Azure;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using OutThink.EmailInjectorApp.Constants;
using OutThink.EmailInjectorApp.Services;
using Xunit;

namespace Outthink.EmailInjectorApp.Tests.Services;

public class ConfigurationServiceTests
{
    private readonly IConfiguration _config;
    private readonly ILogger _logger = Substitute.For<ILogger>();
    private readonly ILoggerFactory _loggerFactory = Substitute.For<ILoggerFactory>();
    private readonly SecretClient _secretClient = Substitute.For<SecretClient>();

    public ConfigurationServiceTests()
    {
        var inMemory = new Dictionary<string, string?>
        {
            { ConfigurationKeys.KeyVaultUrl, "https://mock.vault.azure.net/" }
        };
        _config = new ConfigurationBuilder().AddInMemoryCollection(inMemory!).Build();
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(_logger);
    }

    private ConfigurationService CreateService()
    {
        var service = Substitute.ForPartsOf<ConfigurationService>(_config, _loggerFactory);
        typeof(ConfigurationService)
            .GetField("_keyVaultClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(service, _secretClient);
        return service;
    }

    [Fact]
    public async Task GetSecretAsync_ReturnsValue_WhenSecretExists()
    {
        var service = CreateService();
        _secretClient.GetSecretAsync("MySecret")
            .Returns(Task.FromResult(Response.FromValue(new KeyVaultSecret("MySecret", "secretValue"), null!)));

        var result = await service.GetSecretAsync("MySecret");

        Assert.Equal("secretValue", result);
    }

    [Fact]
    public async Task GetSecretAsync_Throws_WhenSecretIsEmpty()
    {
        var service = CreateService();
        _secretClient.GetSecretAsync("EmptySecret")
            .Returns(Task.FromResult(Response.FromValue(new KeyVaultSecret("EmptySecret", ""), null!)));

        var ex = await Assert.ThrowsAsync<Exception>(() => service.GetSecretAsync("EmptySecret"));
        Assert.Contains("null or empty", ex.Message);
    }

    [Fact]
    public async Task GetSecretOrConfigAsync_ReturnsFromKeyVault_IfAvailable()
    {
        var service = CreateService();
        _secretClient.GetSecretAsync("ConfigKey")
            .Returns(Task.FromResult(Response.FromValue(new KeyVaultSecret("ConfigKey", "kvValue"), null!)));

        var result = await service.GetSecretOrConfigAsync("ConfigKey");

        Assert.Equal("kvValue", result);
    }

    [Fact]
    public async Task GetSecretOrConfigAsync_FallbacksToConfig_IfKeyVaultFails()
    {
        var service = CreateService();
        _secretClient.GetSecretAsync("ConfigKey")
            .Returns(Task.FromException<Response<KeyVaultSecret>>(new Exception("Not found")));
        _config["ConfigKey"] = "configValue";

        var result = await service.GetSecretOrConfigAsync("ConfigKey");

        Assert.Equal("configValue", result);
    }

    [Fact]
    public async Task GetSecretOrConfigAsync_UsesDefault_IfAllMissing()
    {
        var service = CreateService();
        _secretClient.GetSecretAsync("MissingKey")
            .Returns(Task.FromException<Response<KeyVaultSecret>>(new Exception("Not found")));
        _config["MissingKey"] = null;

        var result = await service.GetSecretOrConfigAsync("MissingKey", "default123");

        Assert.Equal("default123", result);
    }
}
