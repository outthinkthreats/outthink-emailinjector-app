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
            var keyVaultUrl = _config["KeyVaultUrl"];
            if (string.IsNullOrWhiteSpace(keyVaultUrl))
            {
                logger.LogError("KeyVaultUrl is empty");
                throw new Exception("KeyVault URL is missing in configuration.");
            }
            
            var credential = new DefaultAzureCredential();
            
            _keyVaultClient = new SecretClient(new Uri(keyVaultUrl), credential);
            
            _settings = new Dictionary<string, string?>
            {
                { "ApiBaseUrl", GetSecretOrConfig("ApiBaseUrl", "ApiBaseUrl") },
                { "BatchSize", GetSecretOrConfig("BatchSize", "BatchSize", "10") },
                { "SkipConfirmation", GetSecretOrConfig("SkipConfirmation", "SkipConfirmation", "false") },
                { "CycleDelay", GetSecretOrConfig("CycleDelay", "CycleDelay", "60000") },
                { "ClientId", GetSecret("ClientId") },
                { "ClientSecret", GetSecret("ClientSecret") },
                { "TenantId", GetSecret("TenantId") },
                { "OtApiKey", GetSecret("OtApiKey") },
                { "OtCustomerId", GetSecret("OTCustomerId") }
            };

            ValidateConfiguration();
        }
        
        private string GetSecret(string secretKey)
        {
            try
            {
                var secretValue = _keyVaultClient.GetSecret(secretKey).Value.Value;
                if (string.IsNullOrWhiteSpace(secretValue))
                    throw new Exception($"Secret {secretKey} is null or empty.");
                _logger.LogInformation($"Secret '{secretKey}' loaded from KeyVault.");
                return secretValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving secret '{secretKey}' from KeyVault.");
                throw new Exception($"Missing required secret in Key Vault: {secretKey}. Error: {ex.Message}");
            }
        }
        
        private string GetSecretOrConfig(string configKey, string secretKey, string? defaultValue = null)
        {
            string? value = null;

            try
            {
                var secret = _keyVaultClient.GetSecret(secretKey);
                if (secret?.Value?.Value != null)
                {
                    value = secret.Value.Value;
                    _logger.LogInformation($"Value for '{configKey}' loaded from KeyVault.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Could not fetch secret '{secretKey}' from Key Vault. Falling back. Error: {ex.Message}");
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                value = _config[configKey];
                if (!string.IsNullOrWhiteSpace(value))
                    _logger.LogInformation($"Value for '{configKey}' loaded from app settings or environment.");
            }

            if (string.IsNullOrWhiteSpace(value) && defaultValue != null)
            {
                value = defaultValue;
                _logger.LogInformation($"Using default value for '{configKey}': {defaultValue}");
            }

            return value ?? throw new Exception($"Missing required configuration for '{configKey}' (KeyVault: '{secretKey}')");
        }

        private void ValidateConfiguration()
        {
            var missingKeys = _settings.Where(kvp => string.IsNullOrWhiteSpace(kvp.Value))
                .Select(kvp => kvp.Key)
                .ToList();

            if (missingKeys.Any())
                throw new Exception($"Missing required configuration values: {string.Join(", ", missingKeys)}");
        }

        public string Get(string key)
        {
            if (!_settings.ContainsKey(key))
                throw new Exception($"Configuration key '{key}' not found.");

            return _settings[key]!;
        }
    }
}
