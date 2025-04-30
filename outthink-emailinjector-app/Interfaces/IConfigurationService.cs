namespace OutThink.EmailInjectorApp.Interfaces;

public interface IConfigurationService
{
    string Get(string key);
    Task ReloadAsync();
}