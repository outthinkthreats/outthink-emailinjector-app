using System.Text.Json;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using OutThink.EmailInjectorApp.Constants;
using OutThink.EmailInjectorApp.Interfaces;

namespace OutThink.EmailInjectorApp.Services
{
    /// <summary>
    /// Provides application configuration values from Azure Key Vault, app settings, and environment variables.
    /// Supports fallback and dynamic reloading.
    /// </summary>
    public class ConfigurationService : IConfigurationService
    {
        private readonly IConfiguration _config;
        private readonly SecretClient _keyVaultClient;
        private readonly Dictionary<string, string?> _settings;
        private readonly ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationService"/> class.
        /// It validates KeyVault connectivity and prepares for configuration loading.
        /// </summary>
        /// <param name="config">The application configuration provider.</param>
        /// <param name="logger"></param>
        /// To get logs from AppInsights, use the following: traces | where customDimensions.CategoryName == "OutThink" | order by timestamp desc
        public ConfigurationService(IConfiguration config, ILoggerFactory logger)
        {
            
            _config = config;
            _logger = logger.CreateLogger("OutThink");
            // Connect to Client KV
            // Client has to already granted access to the KV in the portal for the above ClientId
            var keyVaultUrl = _config[ConfigurationKeys.KeyVaultUrl];
            
            if (string.IsNullOrWhiteSpace(keyVaultUrl))
            {
                _logger.LogError("KeyVaultUrl is empty");
                throw new Exception("KeyVault URL is missing in configuration.");
            }
            
            var credential = new DefaultAzureCredential();
            
            _keyVaultClient = new SecretClient(new Uri(keyVaultUrl), credential);

            _settings = new Dictionary<string, string?>();
        }
        
        /// <summary>
        /// Retrieves a secret value from Azure Key Vault.
        /// </summary>
        /// <param name="secretKey">The name of the secret.</param>
        /// <returns>The secret value.</returns>
        /// <exception cref="Exception">Thrown if the secret is missing or empty.</exception>
        internal async Task<string> GetSecretAsync(string secretKey)
        {
            try
            {
                var secretValue = (await _keyVaultClient.GetSecretAsync(secretKey)).Value.Value;
                if (string.IsNullOrWhiteSpace(secretValue))
                    throw new Exception($"Secret {secretKey} is null or empty.");
                _logger.LogDebug($"Secret '{secretKey}' loaded from KeyVault.");
                return secretValue;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error retrieving secret '{secretKey}' from KeyVault.");
                throw new Exception($"Missing required secret in Key Vault: {secretKey}. Error: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Retrieves a configuration value from Key Vault or app settings with an optional default.
        /// Prioritizes Key Vault over local config.
        /// </summary>
        /// <param name="configKey">The key name in app settings.</param>
        /// <param name="defaultValue">The fallback default value if nothing is found.</param>
        /// <returns>The resolved configuration value.</returns>
        internal async Task<string> GetSecretOrConfigAsync(string configKey, string? defaultValue = null)
        {
            
            string? value = null;

            try
            {
                var secret = await _keyVaultClient.GetSecretAsync(configKey);
                if (secret?.Value?.Value != null)
                {
                    value = secret.Value.Value;
                    _logger.LogInformation($"Value for '{configKey}' loaded from KeyVault.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Could not fetch secret '{configKey}' from Key Vault. Falling back. Error: {ex.Message}");
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                value = _config[configKey];
                if (!string.IsNullOrWhiteSpace(value))
                    _logger.LogDebug($"Value for '{configKey}' loaded from app settings or environment.");
            }

            if (string.IsNullOrWhiteSpace(value) && defaultValue != null)
            {
                value = defaultValue;
                _logger.LogDebug($"Using default value for '{configKey}': {defaultValue}");
            }

            return value ?? throw new Exception($"Missing required configuration for '{configKey}' (KeyVault: '{configKey}')");
        }

        /// <summary>
        /// Validates that all loaded settings contain non-null, non-empty values.
        /// Throws if any required value is missing.
        /// </summary>
        private void ValidateConfiguration()
        {
            var missingKeys = _settings.Where(kvp => string.IsNullOrWhiteSpace(kvp.Value))
                .Select(kvp => kvp.Key)
                .ToList();

            if (missingKeys.Any())
                throw new Exception($"Missing required configuration values: {string.Join(", ", missingKeys)}");
        }

        /// <summary>
        /// Gets a required configuration value by key.
        /// </summary>
        /// <param name="key">The name of the configuration key.</param>
        /// <returns>The resolved configuration value.</returns>
        /// <exception cref="Exception">Thrown if the key is not found.</exception>
        public string Get(string key)
        {
            if (!_settings.ContainsKey(key))
                throw new Exception($"Configuration key '{key}' not found.");

            return _settings[key]!;
        }
        
        /// <summary>
        /// Gets all configuration values in a safe, redacted dictionary format.
        /// Secrets are masked.
        /// </summary>
        /// <returns>A dictionary of key-value pairs.</returns>
        private Dictionary<string, string> GetAllSafe()
        {
            return _settings.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Key is ConfigurationKeys.ClientSecret or ConfigurationKeys.OtApiKey
                    ? MaskSecret(kvp.Value)
                    : kvp.Value ?? "[null]"
            );
        }
        
        /// <summary>
        /// Returns a pretty-printed JSON string of all safe configuration values.
        /// </summary>
        private string GetAllSafeBeautifyString()
        {
            var result = GetAllSafe();
            return JsonSerializer.Serialize(
                result,
                new JsonSerializerOptions { WriteIndented = true }
            );
        }

        /// <summary>
        /// Obscures secrets for safe logging.
        /// </summary>
        /// <param name="value">The secret value to mask.</param>
        /// <returns>A masked version of the secret.</returns>
        private static string MaskSecret(string? value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length <= 10)
                return "**********";

            return $"{value[..3]}*****{value[^3..]}";
        }
        
        /// <summary>
        /// Reloads all configuration values from Key Vault and app settings.
        /// Applies fallback logic and validates required keys.
        /// </summary>
        public async Task ReloadAsync()
        {
            _settings.Clear();
            
            _logger.LogDebug("Reloading configuration from KeyVault and app settings...");

            _settings[ConfigurationKeys.ApiBaseUrl]     = await GetSecretOrConfigAsync(ConfigurationKeys.ApiBaseUrl);
            _settings[ConfigurationKeys.BatchSize]      = await GetSecretOrConfigAsync(ConfigurationKeys.BatchSize, "10");
            _settings[ConfigurationKeys.SkipConfirmation] = await GetSecretOrConfigAsync(ConfigurationKeys.SkipConfirmation, "false");
            _settings[ConfigurationKeys.CycleDelay]     = await GetSecretOrConfigAsync(ConfigurationKeys.CycleDelay, "60000");
            _settings[ConfigurationKeys.ClientId]       = await GetSecretAsync(ConfigurationKeys.ClientId);
            _settings[ConfigurationKeys.ClientSecret]   = await GetSecretAsync(ConfigurationKeys.ClientSecret);
            _settings[ConfigurationKeys.TenantId]       = await GetSecretAsync(ConfigurationKeys.TenantId);
            _settings[ConfigurationKeys.OtApiKey]       = await GetSecretAsync(ConfigurationKeys.OtApiKey);
            _settings[ConfigurationKeys.OtCustomerId]   = await GetSecretAsync(ConfigurationKeys.OtCustomerId);

            _logger.LogInformation("Configuration reloaded:"+ GetAllSafeBeautifyString());

            ValidateConfiguration();
        }

    }
}
