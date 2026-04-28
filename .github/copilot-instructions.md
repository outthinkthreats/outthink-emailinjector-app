# OutThink Email Injector App — Copilot Instructions

## Project Purpose

Background worker service that injects and sends phishing-simulation emails via Microsoft Graph API on behalf of OutThink customers. It polls a backend API for pending messages, processes them (inject into inbox or send via Graph), then confirms or marks failures.

## Solution Structure

| Project | Purpose |
|---|---|
| `outthink-emailinjector-app` (`OutThink.EmailInjectorApp`) | Main app — ASP.NET Core 8 Web host with `BackgroundService` worker, Graph API client, config from Azure Key Vault, remote logging |
| `Outthink.EmailInjectorApp.Tests` | xUnit tests — references main project, uses NSubstitute + RichardSzalay.MockHttp |

## Tech Stack

- **.NET 8** (ASP.NET Core Web SDK, `net8.0`)
- **Microsoft Graph API** — direct HTTP calls (no Graph SDK), MSAL `ConfidentialClientApplication` for token acquisition
- **Azure Key Vault** — `Azure.Security.KeyVault.Secrets` + `Azure.Identity` (`DefaultAzureCredential`)
- **Polly 8.x** — `RetryPolicyFactory` for 429/503 with Retry-After support; `Microsoft.Extensions.Http.Resilience` standard resilience handler on `MessageProcessorService` HttpClient
- **Application Insights** — telemetry via `Microsoft.ApplicationInsights.AspNetCore`
- **System.Text.Json** — all serialization (no Newtonsoft)
- **Testing**: xUnit 2.4, NSubstitute 5.x, RichardSzalay.MockHttp 7.x, Microsoft.NET.Test.Sdk 17.x

## Architecture

- **No layered/Clean Architecture** — flat service-oriented structure: `Services/`, `Clients/`, `Interfaces/`, `Models/`, `Constants/`, `Workers/`
- **Interface-first DI** — every service has a matching `I{ServiceName}` interface in `Interfaces/`
- **Singleton registrations** — all services registered as `AddSingleton` in `Program.cs`
- **BackgroundService pattern** — `Worker` extends `BackgroundService`, loops on configurable `CycleDelay`
- **Streaming deserialization** — pending messages consumed line-by-line via `IAsyncEnumerable<DmiMessage>` from NDJSON stream

## Folder Conventions

```
Clients/       — External API clients (GraphApiClient, RetryPolicyFactory)
Constants/     — Static string/enum constants (ConfigurationKeys, MessageStatus)
Interfaces/    — One interface per service/client
Models/        — C# records for DTOs (DmiMessage, FailDmiMessage, RegisterLog, etc.)
Services/      — Business logic services
Workers/       — BackgroundService implementations
```

## Naming Conventions

- **Namespace**: `OutThink.EmailInjectorApp.{Folder}` (PascalCase)
- **Interfaces**: `I{ClassName}` — always in `Interfaces/` folder
- **Services**: `{Name}Service` — always in `Services/`
- **Clients**: `{Name}Client` — always in `Clients/`
- **Models**: C# `record` types with positional parameters (not classes)
- **Constants**: `static class` with `public const string` fields
- **Async methods**: always suffix with `Async`
- **Test classes**: `{ClassUnderTest}Tests` in matching `Services/` or `Clients/` subfolder
- **Test methods**: `MethodName_Scenario_ExpectedBehavior` pattern

## Coding Patterns

### DI Registration
Always register via interface → implementation as singleton in `Program.cs`:
```csharp
builder.Services.AddSingleton<IMyService, MyService>();
```

### Configuration
- Use `IConfigurationService.Get(ConfigurationKeys.Xxx)` — never read `IConfiguration` directly in services
- All config keys defined as constants in `ConfigurationKeys`
- Key Vault values take priority over appsettings; fallback chain: Key Vault → appsettings → default
- `ReloadAsync()` called each worker cycle to pick up Key Vault changes

### Error Handling
- Catch-log-continue in the worker loop (never crash the background service)
- Specific `HttpRequestException` catch with `StatusCode` check for 404 vs general exceptions
- Logging done via `ILoggingService.LogAsync()` which sends to remote API AND logs locally
- Use `LogType` enum (`Info`, `Warning`, `Error`, `Debug`) — not `LogLevel`

### HTTP Requests
- Backend API calls: use `IHttpRequestService.SendAsync(method, relativeEndpoint, body?)` — handles auth headers (`OT-Customer-Id`, Bearer token) automatically
- Graph API calls: construct `HttpRequestMessage` directly with Bearer token in `GraphApiClient`
- Always call `response.EnsureSuccessStatusCode()`

### Models
- Always use `record` with positional constructor syntax
- Keep models in `Models/` folder, one logical group per file

### XML Documentation
- All public classes and methods have `<summary>` XML doc comments
- Include `<param>`, `<returns>`, `<exception>` tags where applicable

## What NOT to Do

- Never use `Newtonsoft.Json` — use `System.Text.Json` exclusively
- Never read `IConfiguration` directly in services — always go through `IConfigurationService`
- Never register services as `Scoped` or `Transient` — use `Singleton`
- Never throw generic `Exception` in new code without a descriptive message
- Never hard-code configuration values — add them to `ConfigurationKeys` and `appsettings.json`
- Never skip the interface — every new service/client must have a matching interface
- Never log sensitive data (emails are masked via `MaskEmail` helper)

## Configuration (appsettings.json)

```
app:name, app:service, app:version  — App metadata
KeyVaultUrl                          — Azure Key Vault URI
ApiBaseUrl                           — Backend API base URL
BatchSize                            — Messages per batch
SkipConfirmation                     — Skip confirm call (bool)
CycleDelay                           — Worker loop delay in ms
ApplicationInsights:ConnectionString — App Insights connection
Logging:LogLevel                     — Per-category log levels
```

Secrets in Key Vault: `ClientId`, `ClientSecret`, `TenantId`, `OtApiKey`, `OTCustomerId`

## Testing Patterns

- **Framework**: xUnit with `[Fact]` and `[Theory]`/`[InlineData]`
- **Mocking**: NSubstitute (`Substitute.For<IInterface>()`) for service interfaces
- **HTTP mocking**: `RichardSzalay.MockHttp` (`MockHttpMessageHandler`) for `HttpClient`
- **Helper**: `FakeLogger` / `FakeLogger<T>` for capturing log output
- **Setup**: inline in constructor or `CreateService()` / `CreateClient()` factory methods (no base class)
- **Assertions**: `Assert.Equal`, `Assert.Contains`, `Assert.ThrowsAsync<T>`
- **InternalsVisibleTo**: test project can access `internal` members
