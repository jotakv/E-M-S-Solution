using BaseLibrary.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Server.BackgroundServices;
using Server.Middleware;
using ServerLibrary.Data;
using ServerLibrary.Helpers;
using ServerLibrary.Repositories.Contracts;
using ServerLibrary.Repositories.Implementations;
using ServerLibrary.Services.Contracts;
using ServerLibrary.Services.Implementations;
using System.Text;

// ──────────────────────────────────────────────────────────────────────────────
// Stage-1 bootstrap logger: active before the host is fully built so any
// startup failure is still captured by the configured sinks.
// ──────────────────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [Bootstrap] {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    Log.Information(
        "Starting EMS Server — Environment: {Environment} | Machine: {Machine}",
        Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
        Environment.MachineName);

    var builder = WebApplication.CreateBuilder(args);

    // ──────────────────────────────────────────────────────────────────────────
    // Stage-2: replace the bootstrap logger with the full configuration from
    // appsettings.json (Console + rolling File + Seq, enrichers, etc.)
    // ──────────────────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((context, services, configuration) =>
    {
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentName()
            .Enrich.WithEnvironmentUserName()
            .Enrich.WithThreadId()
            .Enrich.WithProcessId();
    });

    // ── Controllers & API docs ────────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // ── JWT configuration ─────────────────────────────────────────────────────
    builder.Services.Configure<JwtSection>(builder.Configuration.GetSection("JwtSection"));
    var jwtSection = builder.Configuration.GetSection(nameof(JwtSection)).Get<JwtSection>();

    // ── RabbitMQ and Event Bus ─────────────────────────────────────────────────
    builder.Services.Configure<RabbitMqSettings>(builder.Configuration.GetSection("RabbitMQ"));
    builder.Services.AddSingleton<IEventBus, RabbitMqEventBus>();
    builder.Services.AddHostedService<EmsAuditConsumer>();

    // ── Database ──────────────────────────────────────────────────────────────
    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ??
            throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured."));
    });

    // ── Authentication ────────────────────────────────────────────────────────
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime         = true,
            ValidIssuer              = jwtSection!.Issuer,
            ValidAudience            = jwtSection!.Audience,
            IssuerSigningKey         = new SymmetricSecurityKey(
                                           Encoding.UTF8.GetBytes(jwtSection.Key!))
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILogger<Program>>();

                if (context.Exception is SecurityTokenExpiredException expEx)
                {
                    logger.LogWarning(
                        "Token expired — Path: {Path}, ExpiredAt: {ExpiredAt}, IP: {IP}",
                        context.HttpContext.Request.Path,
                        expEx.Expires.ToString("o"),
                        context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");

                    context.Response.Headers["Token-Expired"] = "true";
                }
                else
                {
                    logger.LogWarning(
                        "Invalid token — Path: {Path}, Error: {ErrorMessage}, IP: {IP}",
                        context.HttpContext.Request.Path,
                        context.Exception.Message,
                        context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
                }
                return Task.CompletedTask;
            },

            OnChallenge = context =>
            {
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

    // ── Repository registrations ──────────────────────────────────────────────
    builder.Services.AddScoped<IUserAccount, UserAccountRepository>();

    builder.Services.AddScoped<IGenericRepositoryInterface<GeneralDepartment>, GeneralDepartmentRepository>();
    builder.Services.AddScoped<IGenericRepositoryInterface<Department>,        DepartmentRepository>();
    builder.Services.AddScoped<IGenericRepositoryInterface<Branch>,            BranchRepository>();

    builder.Services.AddScoped<IGenericRepositoryInterface<Country>, CountryRepository>();
    builder.Services.AddScoped<IGenericRepositoryInterface<City>,    CityRepository>();
    builder.Services.AddScoped<IGenericRepositoryInterface<Town>,    TownRepository>();

    builder.Services.AddScoped<IGenericRepositoryInterface<Overtime>,     OvertimeRepository>();
    builder.Services.AddScoped<IGenericRepositoryInterface<OvertimeType>, OvertimeTypeRepository>();

    builder.Services.AddScoped<IGenericRepositoryInterface<Sanction>,     SanctionRepository>();
    builder.Services.AddScoped<IGenericRepositoryInterface<SanctionType>, SanctionTypeRepository>();

    builder.Services.AddScoped<IGenericRepositoryInterface<Vacation>,     VacationRepository>();
    builder.Services.AddScoped<IGenericRepositoryInterface<VacationType>, VacationTypeRepository>();

    builder.Services.AddScoped<IGenericRepositoryInterface<Doctor>,   DoctorRepository>();
    builder.Services.AddScoped<IGenericRepositoryInterface<Employee>, EmployeeRepository>();

    // ── CORS ──────────────────────────────────────────────────────────────────
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowBlazorWasm", policy =>
            policy.WithOrigins("https://localhost:7201")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials());
    });

    // ─────────────────────────────────────────────────────────────────────────
    var app = builder.Build();
    // ─────────────────────────────────────────────────────────────────────────

    // Global exception handler — must be first so it wraps all other middleware.
    app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

    // Correlation-ID middleware: reads/generates X-Correlation-ID and pushes it
    // into the Serilog LogContext for the lifetime of each request.
    app.UseMiddleware<CorrelationIdMiddleware>();

    // Structured HTTP access logging (one entry per request with Method, Path,
    // StatusCode, Elapsed, etc. as first-class structured properties).
    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

        // ── Performance-aware log level ───────────────────────────────────────
        // Slow requests (>1 s) are promoted to Warning so they surface in Seq
        // dashboards without a custom query filter.  5xx and exceptions → Error.
        opts.GetLevel = (httpContext, elapsed, ex) =>
        {
            if (ex != null || httpContext.Response.StatusCode >= 500)
                return Serilog.Events.LogEventLevel.Error;
            if (elapsed > 1000 || httpContext.Response.StatusCode >= 400)
                return Serilog.Events.LogEventLevel.Warning;
            return Serilog.Events.LogEventLevel.Information;
        };

        opts.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost",   httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("UserAgent",
                httpContext.Request.Headers["User-Agent"].ToString());
            diagnosticContext.Set("ContentType",
                httpContext.Response.ContentType ?? string.Empty);
        };
    });

    app.UseHttpsRedirection();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseCors("AllowBlazorWasm");
    app.UseAuthentication();

    // Audit enrichment: runs AFTER authentication so HttpContext.User is populated.
    // Pushes UserId, IpAddress, RequestPath, and RequestId into every log entry.
    app.UseMiddleware<AuditEnrichmentMiddleware>();

    app.UseAuthorization();

    app.MapControllers();

    Log.Information("EMS Server is ready and accepting requests");

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "EMS Server terminated unexpectedly during startup");
}
finally
{
    // Flush all buffered log entries (File / Seq) before the process exits.
    Log.CloseAndFlush();
}
