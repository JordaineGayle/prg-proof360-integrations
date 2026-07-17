# Package justification

Central versions live in `Directory.Packages.props`. Prefer BCL / ASP.NET Core shared framework before adding libraries.

| Package | Why present |
|---|---|
| `Microsoft.Extensions.DependencyInjection.Abstractions` | Enables `IServiceCollection` extension methods in Application, FieldFlow, and Infrastructure without referencing ASP.NET Core. |
| `Microsoft.NET.Test.Sdk` | Test host for `dotnet test`. |
| `xunit` / `xunit.runner.visualstudio` | Unit and host smoke tests. |
| `coverlet.collector` | Coverage collector hook for later CI; no runtime impact on production. |
| `Microsoft.AspNetCore.Mvc.Testing` | In-memory `WebApplicationFactory` smoke tests for API and Mock (no internet). |
| `Microsoft.EntityFrameworkCore.Sqlite` | Durable local persistence for canonical + infrastructure tables. |
| `Microsoft.EntityFrameworkCore.Design` | Migration tooling (private assets on Infrastructure). |
| `SQLitePCLRaw.bundle_e_sqlite3` `2.1.12` | Overrides transitive `2.1.11` affected by NU1903 / GHSA-2m69-gcr7-jv3q. |
| `Microsoft.Extensions.Hosting.Abstractions` | Development migration hosted service. |
| `Microsoft.Extensions.Options.ConfigurationExtensions` / Configuration packages | Bind `ConnectorPersistence` options. |

Polly and Swashbuckle remain deferred until later prompts.
