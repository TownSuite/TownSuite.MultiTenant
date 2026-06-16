# Changelog

All notable changes to **TownSuite.MultiTenant** are documented here.
The format is based on [Keep a Changelog](https://keepachangelog.com/).

## 26.2.0

A correctness, hardening, and decoupling release. **Contains breaking changes** —
review the migration notes before upgrading.

### Breaking changes

- **The library is now database-agnostic.** `Dapper` and
  `Microsoft.Data.SqlClient` are no longer referenced.
  - You must supply the unique-id lookup. `AddTownSuiteMultiTenant` now takes a
    `UniqueIdLookup` delegate **or** an `IUniqueIdRetriever`.
  - `SqlUniqueIdRetriever` was removed from the library and now ships in the
    Console project as a reference implementation named `UniqueIdRetriever`
    (raw ADO.NET, SQL Server). Copy it into your host or write your own.
  - `IUniqueIdRetriever.GetUniqueId` now returns `Task<string?>`.
  - The library's `Tenant.CreateConnection` (returned a `SqlConnection`) was
    removed. Use `Tenant.GetConnectionString(appName)` and build the connection
    in your host (see the Console's `CreateConnection`).
  - Connection strings are parsed with `System.Data.Common.DbConnectionStringBuilder`
    instead of `SqlConnectionStringBuilder`, so decrypted output is **no longer
    keyword-normalized** (`Server=` stays `Server=`, keywords are lower-cased).
- **`Tenant` is now immutable from the outside.** `Connections`, `Aliases`, and
  the new `AppSettings` are `IReadOnlyDictionary`/`IReadOnlyList`. Populate via
  `TryAddConnection` / `TryAddAlias` / `TryAddAppSetting`.
- **`TenantResolver.Tenants`** is now `IReadOnlyDictionary`; `Resolve` /
  `ResolveAsync` return `Tenant?`.
- **`IConfigReader`** gained `EnsureLoadedAsync`, `ResolveUniqueId`,
  `GetAppSettings`, and `LastLoadErrorCount`; `Refresh` takes a
  `CancellationToken`. Custom `ConfigReader` subclasses must implement the new
  `LoadConnectionsAsync(target, appSettings, cancellationToken)` signature.

### Added

- **Resolve tenants by alias.** A tenant can be resolved by any alias (e.g. a DNS
  hostname) as well as its canonical unique id. The reader exposes an
  alias→unique-id index via `IConfigReader.ResolveUniqueId`.
- **Per-tenant app settings.** The HTTP response's `appSettings` are captured and
  exposed on `Tenant.AppSettings` (and `IConfigReader.GetAppSettings`).
- **HTTP responses use `tenantId`.** When a config response includes a `tenantId`,
  it is treated as the authoritative canonical id — its connections are grouped
  under it with no per-connection lookup. Responses without a `tenantId` fall back
  to the injected retriever.
- `AddTownSuiteMultiTenant` DI extension (HTTP or AppSettings source) and a
  `UniqueIdLookup` delegate / `DelegateUniqueIdRetriever`.
- `AppSettingsConfigPairs.UniqueIdLookup` (generic) — `SqlUniqueIdLookup` is kept
  as a backward-compatible alias (resolved via `ResolvedUniqueIdLookup`).
- `LastLoadErrorCount` to distinguish a failed/partial load from "no tenants."
- Package README and release notes.

### Changed

- Tenant data loads are published via a single atomic swap, so concurrent reads
  never observe a half-built cache; concurrent `Refresh` calls are coalesced and
  per-tenant id lookups are throttled.
- `TsWebClient` resolves a fresh `HttpClient` per request from `IHttpClientFactory`.
- Compiled regex cache and an alias index replace per-call regex compilation and
  the quadratic grouping loop.
- All projects target the aligned `Microsoft.Extensions.*` 10.0.9 packages; Dapper
  2.1.79 and Microsoft.Data.SqlClient 7.0.1 now live in the Console only.
- Builds clean with nullable reference annotations throughout (zero warnings).

### Fixed

- `Tenant.Clone` now deep-copies its collections (previously a shared reference
  let alias mutations leak back into the original).
- `Tenant` implements `IEquatable<Tenant>` with correct `Equals`/`GetHashCode`.
- Unknown-tenant lookups return empty results instead of throwing.
- Structured logging templates and correct `LogError(exception, ...)` overloads.

### Security

- An explicit `enc:`-prefixed connection string that fails to decrypt now throws
  instead of silently passing the ciphertext through.
- HTTP response bodies are no longer embedded in `ApiException` messages/`ToString`
  (retained on the `Response` property), avoiding secret leakage into logs.
- The `Authorization` header is omitted when no bearer token is configured.

> Note: the legacy connection-string encryption (TripleDES/ECB/MD5) is retained
> as-is for backwards compatibility.
