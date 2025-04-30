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
    private readonly IConfiguration _config = Substitute.For<IConfiguration>();
    private readonly ILogger<ConfigurationService> _logger = Substitute.For<ILogger<ConfigurationService>>();
    private readonly SecretClient _secretClient = Substitute.For<SecretClient>();

    private ConfigurationService CreateService()
    {
        _config[ConfigurationKeys.KeyVaultUrl].Returns("https://mock.vault.azure.net/");
        var service = Substitute.ForPartsOf<ConfigurationService>(_config, _logger);
        typeof(ConfigurationService)
            .GetField("_keyVaultClient", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(service, _secretClient);
        return service;
    }

    [Fact]
    public async Task GetSecretAsync_ReturnsValue_WhenSecretExists()
    {
        var service = CreateService();
        _secretClient.GetSecretAsync("MySecret").Returns(Task.FromResult(Response.FromValue(SecretModel("MySecret", "secretValue"), null!)));

        var result = await service.GetSecretAsync("MySecret");

        Assert.Equal("secretValue", result);
    }

    [Fact]
    public async Task GetSecretAsync_Throws_WhenSecretIsEmpty()
    {
        var service = CreateService();
        _secretClient.GetSecretAsync("EmptySecret").Returns(Task.FromResult(Response.FromValue(SecretModel("EmptySecret", ""), null!)));

        var ex = await Assert.ThrowsAsync<Exception>(() => service.GetSecretAsync("EmptySecret"));
        Assert.Contains("null or empty", ex.Message);
    }

    [Fact]
    public async Task GetSecretOrConfigAsync_ReturnsFromKeyVault_IfAvailable()
    {
        var service = CreateService();
        _secretClient.GetSecretAsync("ConfigKey").Returns(Task.FromResult(Response.FromValue(SecretModel("ConfigKey", "kvValue"), null!)));

        var result = await service.GetSecretOrConfigAsync("ConfigKey");

        Assert.Equal("kvValue", result);
    }

    [Fact]
    public async Task GetSecretOrConfigAsync_FallbacksToConfig_IfKeyVaultFails()
    {
        var service = CreateService();
        _secretClient.GetSecretAsync("ConfigKey")
            .Returns(Task.FromException<Response<KeyVaultSecret>>(new Exception("Not found")));
        _config["ConfigKey"].Returns("configValue");

        var result = await service.GetSecretOrConfigAsync("ConfigKey");

        Assert.Equal("configValue", result);
    }

    [Fact]
    public async Task GetSecretOrConfigAsync_UsesDefault_IfAllMissing()
    {
        var service = CreateService();
        _secretClient.GetSecretAsync("MissingKey")
            .Returns(Task.FromException<Response<KeyVaultSecret>>(new Exception("Not found")));
        _config["MissingKey"].Returns((string?)null);

        var result = await service.GetSecretOrConfigAsync("MissingKey", "default123");

        Assert.Equal("default123", result);
    }

    private KeyVaultSecret SecretModel(string name, string value)
    {
        return new KeyVaultSecret(name, value);
    }
}