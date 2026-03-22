# RabbitMQ Integration + Remaining Structured Logs — Implementation Prompt

> **How to use this file:** Paste the entire contents of the section(s) you need into a fresh Claude / AI chat session. It contains every architectural detail needed to do the work without guessing. Read the RULES section first.

---

## RULES FOR THE AI IMPLEMENTING THIS

1. **Read before you write.** Before touching any file, read its current contents so you do not overwrite existing logic.
2. **Preserve every existing logger call.** Only add new ones — never remove or replace existing structured log statements.
3. **Follow existing patterns exactly.** Constructor-injected `ILogger<T>` is already used everywhere. Do not introduce `Log.` static calls or new abstractions.
4. **Serilog is already wired up** on the Server. Do not touch `Program.cs` Serilog bootstrap unless specifically instructed.
5. **RabbitMQ is a side-effect bus, not a replacement.** Every action must still complete synchronously (return its response to the caller). RabbitMQ messages are fire-and-forget audit/notification events published *after* the main operation succeeds or fails.
6. **Do not break existing DI registrations.** All new services must be registered in `Server/Program.cs` and/or `ServerLibrary` DI extension methods that already exist.
7. **Be safe with null-checks.** The codebase targets `net8.0` with nullable reference types. Any new code must handle nulls as the existing code does.
8. **No placeholder comments like `// TODO: implement`.** Every method you write must be fully implemented.

---

## SOLUTION ARCHITECTURE QUICK-REFERENCE

```
Solution: EmployeeManagmentSystemSolution
├── BaseLibrary          (net8.0 class library) — DTOs, Entities, Responses
├── ServerLibrary        (net8.0 class library) — AppDbContext, Repositories, Helpers
├── Server               (ASP.NET Core 8.0 Web API) — Controllers, Middleware, Program.cs
├── ClientLibrary        (net8.0 class library) — HTTP services, Auth helpers
└── Client               (Blazor WebAssembly 8.0) — UI pages, components
```

### Tech stack already in use
| Concern | Library / Version |
|---|---|
| Structured logging | Serilog 8 — Console + File (rolling) + Seq (localhost:5341) |
| ORM | EF Core 8 / SQL Server |
| Auth | JWT Bearer (HS256, 1-day lifetime) + refresh tokens stored in DB |
| Password hashing | BCrypt.Net-Next work-factor 11 |
| UI grid & exports | Syncfusion Blazor 32.x (SfGrid with Excel/PDF/Print toolbar) |
| State provider | CustomAuthenticationStateProvider (Blazored.LocalStorage) |
| HTTP pipeline | CustomHttpHandler (auto-attach token + auto-refresh on 401) |
| Middleware | GlobalExceptionHandlerMiddleware, CorrelationIdMiddleware, AuditEnrichmentMiddleware |

### Key file paths (Server-side)
```
Server/
  Program.cs                                          ← DI, middleware, Serilog config
  Controllers/AuthenticationController.cs            ← /api/authentication/*
  Controllers/GenericController.cs                   ← base CRUD controller

ServerLibrary/
  Repositories/Implementations/UserAccountRepository.cs   ← login, register, refresh-token
  Repositories/Implementations/EmployeeRepositoy.cs       ← employee CRUD
  Repositories/Contracts/IUserAccount.cs
  Repositories/Contracts/IGenericRepositoryInterface.cs
  Data/AppDbContext.cs
  Helpers/JwtSection.cs

Client/
  Pages/ContentPages/EmployeePages/EmployeePage.razor          ← Excel/PDF/Print toolbar
  Pages/ContentPages/EmployeePages/AddOrUpdateEmployeePage.razor ← Image upload
  Pages/ContentPages/DoctorPages/DoctorPage.razor              ← Excel/PDF/Print toolbar
  Pages/ContentPages/OvertimePage/OvertimePage.razor           ← Excel/PDF/Print toolbar
  Pages/ContentPages/SanctionPages/SanctionPage.razor          ← Excel/PDF/Print toolbar
  Pages/ContentPages/VacationPages/VacationPage.razor          ← Excel/PDF/Print toolbar

ClientLibrary/
  Helpers/CustomHttpHandler.cs        ← 401 interception + token refresh
  Helpers/CustomAuthenticationStateProvider.cs
  Helpers/LocalStorageService.cs
  Services/Implementations/UserAccountService.cs
```

---

## PART A — RABBITMQ INTEGRATION

### A.1 Goal

Introduce a **lightweight RabbitMQ event bus** on the Server so that every significant business event publishes a structured message to a queue. A background consumer on the same server reads those messages and processes them (e.g., audit persistence, notifications, future downstream services). The client is **not** changed.

**Events to publish (minimum scope):**
| Event | Trigger |
|---|---|
| `EmployeeExported` | Excel or PDF export requested via Syncfusion grid |
| `EmployeePrinted` | Print toolbar button clicked |
| `ImageUploaded` | Employee photo saved successfully |
| `ImageUploadFailed` | Employee photo failed validation / save |
| `TokenRefreshed` | Successful JWT refresh |
| `TokenExpired` | 401 due to expired token detected server-side |
| `InvalidToken` | 401 due to invalid/malformed token |
| `UserLoggedIn` | Successful sign-in |
| `UserRegistered` | Successful registration |

### A.2 NuGet packages to add (Server and/or ServerLibrary)

```xml
<!-- Add to Server/Server.csproj -->
<PackageReference Include="RabbitMQ.Client" Version="6.8.1" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.0" />
```

> **Why 6.8.1?** It is the last stable RabbitMQ.Client release with the familiar `IModel`/`BasicPublish` API. Version 7.x changed the API significantly. Keep 6.8.1 for stability.

### A.3 Configuration (appsettings.json)

Add the following section to `Server/appsettings.json` (and `appsettings.Development.json` if it exists):

```json
"RabbitMQ": {
  "HostName": "localhost",
  "Port": 5672,
  "UserName": "guest",
  "Password": "guest",
  "VirtualHost": "/",
  "ExchangeName": "ems.events",
  "ExchangeType": "topic",
  "QueueName": "ems.audit",
  "RoutingKeyPrefix": "ems"
}
```

Create a strongly-typed settings class in `ServerLibrary/Helpers/RabbitMqSettings.cs`:

```csharp
namespace ServerLibrary.Helpers;

public class RabbitMqSettings
{
    public string HostName    { get; set; } = "localhost";
    public int    Port        { get; set; } = 5672;
    public string UserName    { get; set; } = "guest";
    public string Password    { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string ExchangeName   { get; set; } = "ems.events";
    public string ExchangeType   { get; set; } = "topic";
    public string QueueName      { get; set; } = "ems.audit";
    public string RoutingKeyPrefix { get; set; } = "ems";
}
```

Bind in `Server/Program.cs`:
```csharp
builder.Services.Configure<RabbitMqSettings>(
    builder.Configuration.GetSection("RabbitMQ"));
```

### A.4 IEventBus interface + RabbitMqEventBus implementation

Create `ServerLibrary/Services/Contracts/IEventBus.cs`:

```csharp
namespace ServerLibrary.Services.Contracts;

/// <summary>
/// Fire-and-forget event publisher. Never throws — failures are logged only.
/// </summary>
public interface IEventBus
{
    void Publish(string routingKey, object payload);
}
```

Create `ServerLibrary/Services/Implementations/RabbitMqEventBus.cs`:

```csharp
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using ServerLibrary.Helpers;
using ServerLibrary.Services.Contracts;

namespace ServerLibrary.Services.Implementations;

/// <summary>
/// Publishes domain events to RabbitMQ as persistent JSON messages.
/// Uses a single long-lived connection with auto-reconnect logic.
/// Failures are swallowed and logged — the main business flow is never blocked.
/// </summary>
public sealed class RabbitMqEventBus : IEventBus, IDisposable
{
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<RabbitMqEventBus> _logger;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _lock = new();

    public RabbitMqEventBus(
        IOptions<RabbitMqSettings> settings,
        ILogger<RabbitMqEventBus> logger)
    {
        _settings = settings.Value;
        _logger   = logger;
        TryConnect();
    }

    public void Publish(string routingKey, object payload)
    {
        try
        {
            EnsureChannel();
            var body = Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(payload,
                    new JsonSerializerOptions { WriteIndented = false }));

            var props = _channel!.CreateBasicProperties();
            props.Persistent    = true;
            props.ContentType   = "application/json";
            props.Timestamp     = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            props.Headers       = new Dictionary<string, object>
            {
                ["routing-key"] = routingKey,
                ["source"]      = "EMS-Server"
            };

            _channel.BasicPublish(
                exchange:   _settings.ExchangeName,
                routingKey: routingKey,
                basicProperties: props,
                body: body);

            _logger.LogDebug(
                "RabbitMQ event published — RoutingKey: {RoutingKey}, PayloadSize: {Bytes}B",
                routingKey, body.Length);
        }
        catch (Exception ex)
        {
            // IMPORTANT: never let a messaging failure bubble up to the caller
            _logger.LogWarning(ex,
                "RabbitMQ publish failed — RoutingKey: {RoutingKey}. " +
                "Event dropped, business operation continues.",
                routingKey);
        }
    }

    // ── private helpers ───────────────────────────────────────────────────────

    private void TryConnect()
    {
        try
        {
            lock (_lock)
            {
                var factory = new ConnectionFactory
                {
                    HostName    = _settings.HostName,
                    Port        = _settings.Port,
                    UserName    = _settings.UserName,
                    Password    = _settings.Password,
                    VirtualHost = _settings.VirtualHost,
                    AutomaticRecoveryEnabled = true,
                    NetworkRecoveryInterval  = TimeSpan.FromSeconds(10)
                };
                _connection = factory.CreateConnection("EMS-Server");
                _channel    = _connection.CreateModel();

                _channel.ExchangeDeclare(
                    exchange: _settings.ExchangeName,
                    type:     _settings.ExchangeType,
                    durable:  true,
                    autoDelete: false);

                _channel.QueueDeclare(
                    queue:      _settings.QueueName,
                    durable:    true,
                    exclusive:  false,
                    autoDelete: false);

                _channel.QueueBind(
                    queue:      _settings.QueueName,
                    exchange:   _settings.ExchangeName,
                    routingKey: $"{_settings.RoutingKeyPrefix}.#");

                _logger.LogInformation(
                    "RabbitMQ connected — Host: {Host}:{Port}, Exchange: {Exchange}, Queue: {Queue}",
                    _settings.HostName, _settings.Port,
                    _settings.ExchangeName, _settings.QueueName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "RabbitMQ connection failed on startup — event publishing disabled. " +
                "Application will continue without RabbitMQ.");
        }
    }

    private void EnsureChannel()
    {
        if (_channel is { IsOpen: true }) return;
        lock (_lock)
        {
            if (_channel is { IsOpen: true }) return;
            _logger.LogWarning("RabbitMQ channel closed — attempting reconnect.");
            TryConnect();
        }
    }

    public void Dispose()
    {
        try { _channel?.Close(); _channel?.Dispose(); } catch { /* ignore */ }
        try { _connection?.Close(); _connection?.Dispose(); } catch { /* ignore */ }
    }
}
```

### A.5 Background consumer (IHostedService)

Create `Server/BackgroundServices/EmsAuditConsumer.cs`:

```csharp
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ServerLibrary.Helpers;

namespace Server.BackgroundServices;

/// <summary>
/// Long-running background service that consumes messages from the EMS audit queue.
/// Extend ProcessMessage() to persist audit records, send emails, trigger webhooks, etc.
/// </summary>
public sealed class EmsAuditConsumer : BackgroundService
{
    private readonly RabbitMqSettings _settings;
    private readonly ILogger<EmsAuditConsumer> _logger;
    private IConnection? _connection;
    private IModel? _channel;

    public EmsAuditConsumer(
        IOptions<RabbitMqSettings> settings,
        ILogger<EmsAuditConsumer> logger)
    {
        _settings = settings.Value;
        _logger   = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        stoppingToken.ThrowIfCancellationRequested();

        try
        {
            var factory = new ConnectionFactory
            {
                HostName    = _settings.HostName,
                Port        = _settings.Port,
                UserName    = _settings.UserName,
                Password    = _settings.Password,
                VirtualHost = _settings.VirtualHost,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval  = TimeSpan.FromSeconds(10)
            };

            _connection = factory.CreateConnection("EMS-AuditConsumer");
            _channel    = _connection.CreateModel();
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 10, global: false);

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (_, ea) =>
            {
                try
                {
                    var body    = Encoding.UTF8.GetString(ea.Body.Span);
                    var key     = ea.RoutingKey;
                    ProcessMessage(key, body);
                    _channel.BasicAck(ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to process RabbitMQ message — RoutingKey: {RoutingKey}. Nacking.",
                        ea.RoutingKey);
                    _channel.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            _channel.BasicConsume(
                queue:      _settings.QueueName,
                autoAck:    false,
                consumer:   consumer);

            _logger.LogInformation(
                "EmsAuditConsumer started — listening on queue: {Queue}", _settings.QueueName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "EmsAuditConsumer could not start — RabbitMQ unavailable. " +
                "Consumer will not run.");
        }

        return Task.CompletedTask;
    }

    private void ProcessMessage(string routingKey, string body)
    {
        // Structured log every received event — extend this to write to DB, send alerts, etc.
        _logger.LogInformation(
            "EMS audit event received — RoutingKey: {RoutingKey}, Body: {Body}",
            routingKey, body);

        // Future: deserialise body, switch on routingKey, persist to AuditLog table, etc.
        // Example:
        //   case "ems.employee.exported":
        //       var evt = JsonSerializer.Deserialize<EmployeeExportedEvent>(body);
        //       await _auditRepo.SaveAsync(evt);
        //       break;
    }

    public override void Dispose()
    {
        try { _channel?.Close(); _channel?.Dispose(); } catch { /* ignore */ }
        try { _connection?.Close(); _connection?.Dispose(); } catch { /* ignore */ }
        base.Dispose();
    }
}
```

### A.6 Register in Program.cs

In `Server/Program.cs`, **after** the existing DI registrations, add:

```csharp
// ── RabbitMQ ──────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IEventBus, RabbitMqEventBus>();
builder.Services.AddHostedService<EmsAuditConsumer>();
```

### A.7 Inject IEventBus into repositories

Anywhere you want to publish an event, add `IEventBus eventBus` to the constructor and store it as `_eventBus`. Then call `_eventBus.Publish(routingKey, payload)` **after** the main operation completes.

**Example — inside `UserAccountRepository`:**

```csharp
// After successful sign-in (inside SignInAsync, after tokens are persisted):
_eventBus.Publish("ems.auth.user-logged-in", new
{
    UserId    = user.Id,
    Email     = user.Email,
    Role      = userRole.Role?.Name,
    Timestamp = DateTime.UtcNow
});

// After successful registration:
_eventBus.Publish("ems.auth.user-registered", new
{
    UserId    = newUser.Id,
    Email     = newUser.Email,
    Timestamp = DateTime.UtcNow
});
```

**Example — inside `EmployeeRepository`:**

```csharp
// After successful Insert:
_eventBus.Publish("ems.employee.created", new
{
    EmployeeId = item.Id,
    Name       = item.Name,
    BranchId   = item.BranchId,
    Timestamp  = DateTime.UtcNow
});
```

---

## PART B — REMAINING STRUCTURED LOGS

These are the logging gaps to fill. Each subsection names the **exact file**, the **method**, and provides the **exact log statements** to add.

---

### B.1 Export Logs — Excel & PDF (Server-side endpoint)

**Context:** Syncfusion grid calls `ExcelExportAsync()` / `ExportToPdfAsync()` entirely client-side — no HTTP request is made to the server for the export itself. Therefore the **recommended approach** is to add a lightweight audit endpoint that the client calls *after* the export completes, OR log inside the `ToolbarClickHandler` on the client side via the existing logger infrastructure.

**Recommended server-side approach:** Add a new endpoint to `AuthenticationController.cs` (or a new `AuditController.cs`):

```csharp
// POST /api/audit/export
[HttpPost("export")]
[Authorize]
public IActionResult LogExport([FromBody] ExportAuditDto dto)
{
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

    _logger.LogInformation(
        "Export triggered — UserId: {UserId}, ExportType: {ExportType}, " +
        "EntityType: {EntityType}, RecordCount: {RecordCount}, Timestamp: {Timestamp}",
        userId, dto.ExportType, dto.EntityType, dto.RecordCount, DateTime.UtcNow);

    _eventBus.Publish($"ems.export.{dto.ExportType.ToLower()}", new
    {
        UserId     = userId,
        ExportType = dto.ExportType,   // "Excel" or "PDF"
        EntityType = dto.EntityType,   // "Employee", "Doctor", "Overtime", etc.
        RecordCount = dto.RecordCount,
        Timestamp  = DateTime.UtcNow
    });

    return Ok();
}
```

Add `ExportAuditDto` to `BaseLibrary/DTOs/`:

```csharp
namespace BaseLibrary.DTOs;

public class ExportAuditDto
{
    public string ExportType  { get; set; } = string.Empty;  // "Excel" | "PDF"
    public string EntityType  { get; set; } = string.Empty;  // "Employee" | "Doctor" | etc.
    public int    RecordCount { get; set; }
}
```

**Client-side call** — in each `ToolbarClickHandler` in the Razor pages (`EmployeePage.razor`, `DoctorPage.razor`, etc.), **after** the Syncfusion export is triggered, call the audit endpoint:

```csharp
private async Task ToolbarClickHandler(Syncfusion.Blazor.Navigations.ClickEventArgs args)
{
    if (args.Item.Text == "Excel Export")
    {
        await this.DefaultGrid!.ExcelExportAsync();
        // ── NEW: audit log ──
        await AuditExportAsync("Excel", "Employee");
    }
    else if (args.Item.Text == "PDF Export")
    {
        await this.DefaultGrid!.ExportToPdfAsync();
        // ── NEW: audit log ──
        await AuditExportAsync("PDF", "Employee");
    }
    // Print is handled in B.2
}

private async Task AuditExportAsync(string exportType, string entityType)
{
    try
    {
        var dto = new ExportAuditDto
        {
            ExportType  = exportType,
            EntityType  = entityType,
            RecordCount = Employees?.Count ?? 0   // use the list already loaded in the page
        };
        var client = await _getHttpClient.GetPrivateHttpClient();
        await client.PostAsJsonAsync("api/audit/export", dto);
    }
    catch
    {
        // silently ignore — audit must not break the export UX
    }
}
```

Repeat the same pattern for `DoctorPage.razor`, `OvertimePage.razor`, `SanctionPage.razor`, `VacationPage.razor` (change `entityType` accordingly).

---

### B.2 Print Log

Same pattern as export. Add a `"Print"` branch to the existing `ToolbarClickHandler` in each grid page. Syncfusion handles print natively (no await needed):

```csharp
else if (args.Item.Text == "Print")
{
    // Syncfusion handles the print dialog natively — just audit it
    await AuditExportAsync("Print", "Employee");
}
```

Add a parallel server-side endpoint if desired (reuse the same `POST /api/audit/export` endpoint with `ExportType = "Print"`), or add a dedicated one:

```csharp
// In the same AuditController or AuthenticationController:
// POST /api/audit/print
[HttpPost("print")]
[Authorize]
public IActionResult LogPrint([FromBody] PrintAuditDto dto)
{
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

    _logger.LogInformation(
        "Print triggered — UserId: {UserId}, EntityType: {EntityType}, " +
        "RecordCount: {RecordCount}, Timestamp: {Timestamp}",
        userId, dto.EntityType, dto.RecordCount, DateTime.UtcNow);

    _eventBus.Publish("ems.print.triggered", new
    {
        UserId     = userId,
        EntityType = dto.EntityType,
        RecordCount = dto.RecordCount,
        Timestamp  = DateTime.UtcNow
    });

    return Ok();
}
```

---

### B.3 Token Expired / Invalid Token Logs

There are three places to instrument:

#### B.3.1 Server-side — JWT middleware (add event handlers in Program.cs)

Inside the `AddAuthentication(...).AddJwtBearer(...)` block in `Server/Program.cs`, add event handlers:

```csharp
builder.Services.AddAuthentication(options =>
{
    // ... existing options unchanged ...
})
.AddJwtBearer(options =>
{
    // ... existing TokenValidationParameters unchanged ...

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            var logger = context.HttpContext.RequestServices
                .GetRequiredService<ILogger<Program>>();

            if (context.Exception is SecurityTokenExpiredException expEx)
            {
                logger.LogWarning(
                    "Token expired — Path: {Path}, ExpiredAt: {ExpiredAt}, " +
                    "IP: {IP}",
                    context.HttpContext.Request.Path,
                    expEx.Expires.ToString("o"),
                    context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

                context.Response.Headers["Token-Expired"] = "true";
            }
            else
            {
                logger.LogWarning(
                    "Invalid token — Path: {Path}, Error: {ErrorMessage}, " +
                    "IP: {IP}",
                    context.HttpContext.Request.Path,
                    context.Exception.Message,
                    context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
            }

            return Task.CompletedTask;
        },

        OnChallenge = context =>
        {
            // Fires whenever a request is challenged (401)
            if (!context.Handled)
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>();

                logger.LogInformation(
                    "Auth challenge (401) — Path: {Path}, ErrorDescription: {Desc}",
                    context.HttpContext.Request.Path,
                    context.ErrorDescription ?? "n/a");
            }
            return Task.CompletedTask;
        }
    };
});
```

#### B.3.2 Server-side — RefreshTokenAsync in UserAccountRepository.cs

In the `RefreshTokenAsync` method, add these log statements at the appropriate validation points:

```csharp
// When refresh token is not found in DB:
_logger.LogWarning(
    "Token refresh failed — refresh token not found in database. " +
    "PossibleTokenReuse or forged token.");

// When refresh token is found and new tokens are generated:
_logger.LogInformation(
    "Token refreshed successfully — UserId: {UserId}, NewTokenIssuedAt: {IssuedAt}",
    updateRefreshToken.UserId, DateTime.UtcNow);

// Publish to RabbitMQ after successful refresh:
_eventBus.Publish("ems.auth.token-refreshed", new
{
    UserId    = updateRefreshToken.UserId,
    Timestamp = DateTime.UtcNow
});
```

#### B.3.3 Client-side — CustomHttpHandler.cs

In the `SendAsync` method, inside the `if (response.StatusCode == HttpStatusCode.Unauthorized)` block, add:

```csharp
// After detecting 401 and before attempting refresh:
Console.WriteLine(
    $"[EMS] 401 Unauthorized detected — Path: {request.RequestUri?.PathAndQuery}. " +
    $"Token-Expired header: {response.Headers.Contains("Token-Expired")}. " +
    $"Attempting token refresh at {DateTime.UtcNow:o}");

// After successful refresh:
Console.WriteLine(
    $"[EMS] Token refreshed successfully — retrying original request: " +
    $"{request.RequestUri?.PathAndQuery}");

// If refresh fails (no stored token or refresh endpoint returns non-success):
Console.WriteLine(
    $"[EMS] Token refresh failed — no stored session or refresh endpoint returned " +
    $"{refreshResponse?.StatusCode}. User will need to log in again.");
```

> **Note:** In Blazor WebAssembly there is no server-side `ILogger`. Use `Console.WriteLine` or inject `Microsoft.Extensions.Logging.ILogger<CustomHttpHandler>` which routes to browser console. The existing codebase does not use ILogger in WASM — follow the same pattern you see in the file.

---

### B.4 Image Upload Success / Failure Logs

**File:** `Client/Pages/ContentPages/EmployeePages/AddOrUpdateEmployeePage.razor`

**Method:** `UploadImage` (the `InputFile` event handler — currently around line 325)

Add the following log statements inside that method:

```csharp
private async Task UploadImage(InputFileChangeEventArgs e)
{
    var file = e.File;

    // ── NEW: log upload attempt ──
    Console.WriteLine(
        $"[EMS] Image upload attempt — FileName: {file.Name}, " +
        $"ContentType: {file.ContentType}, SizeBytes: {file.Size}, " +
        $"Timestamp: {DateTime.UtcNow:o}");

    if (file.ContentType is not ("image/png" or "image/x-png"))
    {
        // ── EXISTING message + NEW log ──
        employeeGroup1.Photo = null;
        // (keep existing UI feedback code)

        Console.WriteLine(
            $"[EMS] Image upload FAILED — invalid file type: {file.ContentType}. " +
            $"Expected image/png. FileName: {file.Name}");

        // ── NEW: call server audit endpoint ──
        await AuditImageUploadAsync(success: false, reason: $"InvalidContentType:{file.ContentType}", fileName: file.Name);
        return;
    }

    try
    {
        var resizedFile = await file.RequestImageFileAsync("image/png", 300, 300);
        using var stream = resizedFile.OpenReadStream(maxAllowedSize: 5 * 1024 * 1024); // 5 MB guard
        using var ms     = new MemoryStream();
        await stream.CopyToAsync(ms);
        var base64 = Convert.ToBase64String(ms.ToArray());
        employeeGroup1.Photo = $"data:image/png;base64,{base64}";

        // ── NEW: success log ──
        Console.WriteLine(
            $"[EMS] Image upload SUCCESS — FileName: {file.Name}, " +
            $"OriginalSizeBytes: {file.Size}, EncodedBase64Length: {base64.Length}, " +
            $"Timestamp: {DateTime.UtcNow:o}");

        await AuditImageUploadAsync(success: true, reason: "OK", fileName: file.Name);
    }
    catch (Exception ex)
    {
        // ── NEW: exception log ──
        Console.WriteLine(
            $"[EMS] Image upload EXCEPTION — FileName: {file.Name}, " +
            $"Error: {ex.Message}, Timestamp: {DateTime.UtcNow:o}");

        await AuditImageUploadAsync(success: false, reason: $"Exception:{ex.Message}", fileName: file.Name);
    }
}

// ── NEW helper method in the same component ──────────────────────────────────
private async Task AuditImageUploadAsync(bool success, string reason, string fileName)
{
    try
    {
        var dto = new ImageUploadAuditDto
        {
            Success  = success,
            FileName = fileName,
            Reason   = reason
        };
        var client = await _getHttpClient.GetPrivateHttpClient();
        await client.PostAsJsonAsync("api/audit/image-upload", dto);
    }
    catch
    {
        // silently ignore — audit must not break the upload UX
    }
}
```

**Add `ImageUploadAuditDto`** to `BaseLibrary/DTOs/ImageUploadAuditDto.cs`:

```csharp
namespace BaseLibrary.DTOs;

public class ImageUploadAuditDto
{
    public bool   Success  { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string Reason   { get; set; } = string.Empty;
}
```

**Add server-side endpoint** (same `AuditController.cs` as the export endpoint):

```csharp
// POST /api/audit/image-upload
[HttpPost("image-upload")]
[Authorize]
public IActionResult LogImageUpload([FromBody] ImageUploadAuditDto dto)
{
    var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

    if (dto.Success)
    {
        _logger.LogInformation(
            "Image upload succeeded — UserId: {UserId}, FileName: {FileName}, Timestamp: {Timestamp}",
            userId, dto.FileName, DateTime.UtcNow);

        _eventBus.Publish("ems.image.upload-success", new
        {
            UserId    = userId,
            FileName  = dto.FileName,
            Timestamp = DateTime.UtcNow
        });
    }
    else
    {
        _logger.LogWarning(
            "Image upload failed — UserId: {UserId}, FileName: {FileName}, " +
            "Reason: {Reason}, Timestamp: {Timestamp}",
            userId, dto.FileName, dto.Reason, DateTime.UtcNow);

        _eventBus.Publish("ems.image.upload-failed", new
        {
            UserId    = userId,
            FileName  = dto.FileName,
            Reason    = dto.Reason,
            Timestamp = DateTime.UtcNow
        });
    }

    return Ok();
}
```

---

## PART C — NEW AuditController (full skeleton)

Create `Server/Controllers/AuditController.cs`. This consolidates all the new audit endpoints:

```csharp
using System.Security.Claims;
using BaseLibrary.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServerLibrary.Services.Contracts;

namespace Server.Controllers;

[ApiController]
[Route("api/audit")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<AuditController> _logger;

    public AuditController(IEventBus eventBus, ILogger<AuditController> logger)
    {
        _eventBus = eventBus;
        _logger   = logger;
    }

    // POST /api/audit/export
    [HttpPost("export")]
    public IActionResult LogExport([FromBody] ExportAuditDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        _logger.LogInformation(
            "Export triggered — UserId: {UserId}, ExportType: {ExportType}, " +
            "EntityType: {EntityType}, RecordCount: {RecordCount}",
            userId, dto.ExportType, dto.EntityType, dto.RecordCount);

        _eventBus.Publish($"ems.export.{dto.ExportType.ToLowerInvariant()}", new
        {
            UserId     = userId,
            ExportType = dto.ExportType,
            EntityType = dto.EntityType,
            RecordCount = dto.RecordCount,
            Timestamp  = DateTime.UtcNow
        });
        return Ok();
    }

    // POST /api/audit/print
    [HttpPost("print")]
    public IActionResult LogPrint([FromBody] PrintAuditDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        _logger.LogInformation(
            "Print triggered — UserId: {UserId}, EntityType: {EntityType}, RecordCount: {RecordCount}",
            userId, dto.EntityType, dto.RecordCount);

        _eventBus.Publish("ems.print.triggered", new
        {
            UserId      = userId,
            EntityType  = dto.EntityType,
            RecordCount = dto.RecordCount,
            Timestamp   = DateTime.UtcNow
        });
        return Ok();
    }

    // POST /api/audit/image-upload
    [HttpPost("image-upload")]
    public IActionResult LogImageUpload([FromBody] ImageUploadAuditDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (dto.Success)
        {
            _logger.LogInformation(
                "Image upload succeeded — UserId: {UserId}, FileName: {FileName}",
                userId, dto.FileName);
            _eventBus.Publish("ems.image.upload-success", new
            {
                UserId    = userId,
                FileName  = dto.FileName,
                Timestamp = DateTime.UtcNow
            });
        }
        else
        {
            _logger.LogWarning(
                "Image upload failed — UserId: {UserId}, FileName: {FileName}, Reason: {Reason}",
                userId, dto.FileName, dto.Reason);
            _eventBus.Publish("ems.image.upload-failed", new
            {
                UserId    = userId,
                FileName  = dto.FileName,
                Reason    = dto.Reason,
                Timestamp = DateTime.UtcNow
            });
        }
        return Ok();
    }
}
```

Add `PrintAuditDto` to `BaseLibrary/DTOs/PrintAuditDto.cs`:

```csharp
namespace BaseLibrary.DTOs;

public class PrintAuditDto
{
    public string EntityType  { get; set; } = string.Empty;
    public int    RecordCount { get; set; }
}
```

---

## PART D — CHECKLIST FOR THE IMPLEMENTING AI

Work through these in order. Check each off before moving to the next.

- [ ] **D.1** Add `RabbitMQ.Client 6.8.1` to `Server/Server.csproj`
- [ ] **D.2** Add `"RabbitMQ": { ... }` block to `Server/appsettings.json`
- [ ] **D.3** Create `ServerLibrary/Helpers/RabbitMqSettings.cs`
- [ ] **D.4** Create `ServerLibrary/Services/Contracts/IEventBus.cs`
- [ ] **D.5** Create `ServerLibrary/Services/Implementations/RabbitMqEventBus.cs`
- [ ] **D.6** Create `Server/BackgroundServices/EmsAuditConsumer.cs`
- [ ] **D.7** Register `IEventBus` (Singleton) and `EmsAuditConsumer` (HostedService) in `Server/Program.cs`
- [ ] **D.8** Inject `IEventBus` into `UserAccountRepository` — publish on login, register, token refresh
- [ ] **D.9** Add `ExportAuditDto`, `PrintAuditDto`, `ImageUploadAuditDto` to `BaseLibrary/DTOs/`
- [ ] **D.10** Create `Server/Controllers/AuditController.cs` (export, print, image-upload endpoints)
- [ ] **D.11** Add JWT `OnAuthenticationFailed` + `OnChallenge` event handlers in `Server/Program.cs`
- [ ] **D.12** Add `LogWarning` calls in `UserAccountRepository.RefreshTokenAsync` (invalid + success)
- [ ] **D.13** Add `Console.WriteLine` logs in `ClientLibrary/Helpers/CustomHttpHandler.cs` (401 detection, refresh, failure)
- [ ] **D.14** Update `UploadImage` in `AddOrUpdateEmployeePage.razor` with success/failure Console logs + server audit call
- [ ] **D.15** Update `ToolbarClickHandler` in `EmployeePage.razor`, `DoctorPage.razor`, `OvertimePage.razor`, `SanctionPage.razor`, `VacationPage.razor` with `AuditExportAsync` / `AuditPrintAsync` calls
- [ ] **D.16** Build the solution — fix any compilation errors (null safety, missing usings, etc.)
- [ ] **D.17** Verify that the application starts without RabbitMQ running (it should warn but not crash)
- [ ] **D.18** Test that a Serilog warning appears in logs when RabbitMQ is unavailable
- [ ] **D.19** When RabbitMQ IS running, verify messages appear in the `ems.audit` queue via RabbitMQ Management UI (localhost:15672)

---

## PART E — HOW TO RUN RABBITMQ LOCALLY

If RabbitMQ is not already installed, the fastest way is Docker:

```bash
docker run -d --name rabbitmq \
  -p 5672:5672 \
  -p 15672:15672 \
  rabbitmq:3-management
```

Then open the Management UI at `http://localhost:15672` (user: `guest`, password: `guest`).

You can also install it natively: https://www.rabbitmq.com/docs/download

---

## PART F — IMPORTANT SAFETY NOTES

1. **RabbitMQ failure must NEVER crash the app.** The `RabbitMqEventBus.Publish` method catches all exceptions and logs a warning. Never remove that try/catch.
2. **Client audit calls must NEVER throw.** Every `AuditExportAsync`, `AuditPrintAsync`, `AuditImageUploadAsync` helper method wraps the HTTP call in a try/catch that swallows exceptions.
3. **Do not add `[Authorize]` to the JWT event handlers** — they fire before authorization.
4. **Do not register `IEventBus` as Scoped or Transient** — the RabbitMQ connection must be a Singleton.
5. **The existing Serilog sinks (File, Console, Seq) will automatically pick up all new `_logger.Log*` calls** — no Serilog config changes are needed for the new log statements.
6. **appsettings.json contains the JWT key in plaintext** — this is already the case in the codebase; do not move it during this task.
