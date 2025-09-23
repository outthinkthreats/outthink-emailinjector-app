namespace OutThink.EmailInjectorApp.Interfaces;

public interface IConfigurationService
{
    /// <summary>
    /// Gets a required configuration value by key.
    /// </summary>
    /// <param name="key">The name of the configuration key.</param>
    /// <returns>The resolved configuration value.</returns>
    /// <exception cref="Exception">Thrown if the key is not found.</exception>
    string Get(string key);
    /// <summary>
    /// Reloads all configuration values from Key Vault and app settings.
    /// Applies fallback logic and validates required keys.
    /// </summary>
    Task ReloadAsync();
}