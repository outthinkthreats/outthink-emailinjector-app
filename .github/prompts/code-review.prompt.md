---
mode: agent
description: Review code against project-specific conventions and patterns
---

# Code Review Checklist

Review the selected code against the OutThink Email Injector App conventions. Flag any violations.

## Architecture & Structure
- [ ] New services are in `Services/`, clients in `Clients/`, models in `Models/`, constants in `Constants/`
- [ ] Every service/client has a matching `I{Name}` interface in `Interfaces/`
- [ ] Namespace follows `OutThink.EmailInjectorApp.{Folder}` pattern
- [ ] No layering violations (services should not reference `Workers/` namespace)

## DI & Registration
- [ ] Registered in `Program.cs` using `AddSingleton` for regular services, or `AddHttpClient<...>()` for `HttpClient`-based services (avoid Scoped/Transient unless there is a documented reason)
- [ ] Registered via interface → implementation where applicable (`AddSingleton<IFoo, Foo>()` or typed client equivalent)
- [ ] Dependencies injected as interfaces, not concrete types

## Configuration
- [ ] Application/business services access config via `IConfigurationService.Get(ConfigurationKeys.Xxx)`; direct `IConfiguration` injection is only for configuration/infrastructure wrapper services (for example `ConfigurationService`, and similar framework-facing services such as logging infrastructure where needed)
- [ ] New config keys added to `ConfigurationKeys` as `public const string`
- [ ] Default values added to `appsettings.json`
- [ ] No hard-coded configuration values in application/business service code

## Serialization
- [ ] Uses `System.Text.Json` exclusively — no `Newtonsoft.Json`
- [ ] `JsonSerializerOptions` with `PropertyNameCaseInsensitive = true` for deserialization

## Models
- [ ] DTOs are `record` types with positional constructor syntax (not classes)
- [ ] Placed in `Models/` folder

## Logging
- [ ] Uses `ILoggingService.LogAsync()` with `LogType` enum — not `ILogger` directly in services
- [ ] No sensitive data logged (emails masked; see `MessageProcessorService.MaskEmail` for the existing pattern)
- [ ] Error paths log before continuing (catch-log-continue pattern)

## Error Handling
- [ ] Worker loop and top-level processors use catch-log-continue (never crash the BackgroundService)
- [ ] `HttpRequestException` with `StatusCode` check for 404 handled separately from generic exceptions
- [ ] `response.EnsureSuccessStatusCode()` called after HTTP calls
- [ ] No bare `throw new Exception()` without a descriptive message

## Async
- [ ] All async methods suffixed with `Async`
- [ ] No `.Result` or `.Wait()` calls (use `await`)
- [ ] `CancellationToken` propagated where applicable

## HTTP
- [ ] Backend API calls use `IHttpRequestService.SendAsync()` (handles auth headers automatically)
- [ ] Graph API calls construct `HttpRequestMessage` directly with Bearer token
- [ ] No direct `HttpClient` instantiation (use DI)

## XML Documentation
- [ ] Public classes and methods have `<summary>` XML doc comments
- [ ] `<param>`, `<returns>`, `<exception>` tags included where applicable

## Testing
- [ ] Test class named `{ClassUnderTest}Tests` in matching subfolder
- [ ] Test methods follow `MethodName_Scenario_ExpectedBehavior` naming
- [ ] NSubstitute for interface mocks, MockHttpMessageHandler for HTTP
- [ ] Factory method `CreateService()` / `CreateClient()` used for SUT creation
- [ ] No test base classes — setup is inline
