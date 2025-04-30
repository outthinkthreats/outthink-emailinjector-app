namespace OutThink.EmailInjectorApp.Models;


public record RegisterLog(
    string Application,
    string Message,
    string[] Args,
    LogType LogType
);

public enum LogType
{
    Info = 1,
    Warning = 2,
    Error = 3
}