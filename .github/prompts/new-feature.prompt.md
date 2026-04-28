---
mode: agent
description: Add a new feature end-to-end following project conventions
---

# New Feature Implementation

You are adding a new feature to the OutThink Email Injector App. Follow these steps using the exact patterns in this codebase.

## User Input
- **Feature name**: {{ feature_name }}
- **Description**: {{ description }}

## Implementation Checklist

### 1. Model (if new data is involved)
- Create a `record` with positional parameters in `Models/`
- Namespace: `OutThink.EmailInjectorApp.Models`
- Example pattern from codebase:
```csharp
public record MyNewModel(Guid Id, string Name, string Value);
```

### 2. Constants (if new config keys or statuses)
- Add new keys to `Constants/ConfigurationKeys.cs` as `public const string`
- Add corresponding default values to `appsettings.json`
- If new enum values needed, add to the appropriate enum in `Constants/`

### 3. Interface
- Create `Interfaces/I{FeatureName}Service.cs` (or `I{FeatureName}Client.cs` for external API clients)
- Namespace: `OutThink.EmailInjectorApp.Interfaces`
- Add `<summary>` XML docs on every method
- All async methods must return `Task` or `Task<T>` and end with `Async`

### 4. Implementation
- Create `Services/{FeatureName}Service.cs` or `Clients/{FeatureName}Client.cs`
- Namespace: `OutThink.EmailInjectorApp.Services` or `.Clients`
- Inject dependencies via constructor (interfaces only)
- Use `IConfigurationService.Get(ConfigurationKeys.Xxx)` for config — never `IConfiguration` directly
- Use `ILoggingService.LogAsync()` with `LogType` enum for logging
- Use `IHttpRequestService.SendAsync()` for backend API calls
- Use `System.Text.Json` for all serialization
- Add `<summary>` XML docs on the class and all public/internal methods

### 5. DI Registration
- Register in `Program.cs` as singleton:
```csharp
builder.Services.AddSingleton<IMyService, MyService>();
```

### 6. Integration Point
- If the feature runs per-cycle, call it from `Worker.ExecuteAsync` or `MessageProcessorService`
- Wrap calls in try/catch with `_log.LogAsync(...)` — never let exceptions crash the worker

### 7. Tests
- Create `Outthink.EmailInjectorApp.Tests/{Services|Clients}/{ClassName}Tests.cs`
- Namespace: `Outthink.EmailInjectorApp.Tests.{Services|Clients}`
- Use NSubstitute for interface mocks: `Substitute.For<IInterface>()`
- Use `MockHttpMessageHandler` from RichardSzalay.MockHttp for HTTP mocking
- Use `FakeLogger<T>` from `Helpers/` for log capture
- Factory pattern: `private MyService CreateService() => new(...);`
- Test naming: `MethodName_Scenario_ExpectedBehavior`
- Use `[Fact]` for single cases, `[Theory]`/`[InlineData]` for parameterized

### 8. Verify
- Build the solution and confirm zero errors
- Run all existing tests to confirm no regressions
- Run new tests to confirm they pass
