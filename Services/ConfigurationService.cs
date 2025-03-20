using Azure.Identity;
using Azure.Security.KeyVault.Secrets;

namespace OutThink.EmailInjectorApp.Services
{
    public class ConfigurationService
    {
        private readonly IConfiguration _config;
        private readonly SecretClient _keyVaultClient;
        private readonly Dictionary<string, string?> _settings;
        private readonly ILogger<ConfigurationService> _logger;

        public ConfigurationService(IConfiguration config, ILogger<ConfigurationService> logger)
        {
            
            _config = config;
            _logger = logger;
            // Connect to Client KV
            // Client has to already granted access to the KV in the portal for the above ClientId
            var keyVaultUrl = _config["KeyVault:Url"];
            if (string.IsNullOrWhiteSpace(keyVaultUrl))
            {
                logger.LogError("KeyVault:Url is empty");
                throw new Exception("KeyVault URL is missing in configuration.");
            }

            var credential = new DefaultAzureCredential();
            _keyVaultClient = new SecretClient(new Uri(keyVaultUrl), credential);

            _settings = new Dictionary<string, string?>
            {
                { "AppClientId", _config["AzureId:ClientId"] },
                { "ApiBaseUrl", GetSecretOrConfig("PublicApi:BaseUrl", "BaseUrl") },
                { "BatchSize", GetSecretOrConfig("PublicApi:BatchSize", "BatchSize") },
                { "SkipConfirmation", GetSecretOrConfig("PublicApi:SkipConfirmation", "SkipConfirmation") },
                { "CycleDelay", GetSecretOrConfig("CycleDelay", "CycleDelay") },
                { "ClientId", GetSecret("ClientId") },
                { "ClientSecret", GetSecret("ClientSecret") },
                { "TenantId", GetSecret("TenantId") },
                { "OtApiKey", GetSecret("OtApiKey") },
                { "OtCustomerId", GetSecret( "OTCustomerId") }
            };

            ValidateConfiguration();
        }
        
        private string GetSecret(string secretKey)
        {
            try
            {
                return _keyVaultClient.GetSecret(secretKey).Value.Value ?? throw new Exception($"Secret {secretKey} is null.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting secret");
                throw new Exception($"Missing required secret in Key Vault: {secretKey}. Error: {ex.Message}");
            }
        }

        private string GetSecretOrConfig(string configKey, string secretKey)
        {
            string? value = null;

            // Try fetching from Key Vault first, but avoid unnecessary exceptions
            try
            {
                var secret = _keyVaultClient.GetSecret(secretKey);
                if (secret?.Value?.Value != null)
                {
                    value = secret.Value.Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Warning: Could not fetch secret '{secretKey}' from Key Vault. Falling back to config. Error: {ex.Message}");
            }

            // If Key Vault secret is missing, fallback to configuration
            if (string.IsNullOrWhiteSpace(value))
            {
                value = _config[configKey];
            }

            // If still missing, throw an error
            return value ?? throw new Exception($"Missing required configuration for '{configKey}' (KeyVault: '{secretKey}')");
        }



        private void ValidateConfiguration()
        {
            var missingKeys = _settings.Where(kvp => string.IsNullOrWhiteSpace(kvp.Value))
                                       .Select(kvp => kvp.Key)
                                       .ToList();

            if (missingKeys.Any())
            {
                throw new Exception($"Missing required configuration values: {string.Join(", ", missingKeys)}");
            }
        }

        public string Get(string key)
        {
            if (!_settings.ContainsKey(key))
                throw new Exception($"Configuration key '{key}' not found.");

            return _settings[key]!;
        }
    }
}
