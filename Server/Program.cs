using BaseLibrary.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Server.BackgroundServices;
using Server.Middleware;
using Server.Services;
using ServerLibrary.Data;
using ServerLibrary.Helpers;
using ServerLibrary.Features.HRIntelligence;
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
    var seedDemoDataOnStartup = builder.Configuration.GetValue<bool>("SeedDemoDataOnStartup");

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

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
        {
            Title   = "EMS API",
            Version = "v1"
        });

        // Add JWT Bearer auth to Swagger UI — click the padlock to paste your token
        const string bearerScheme = "Bearer";
        var jwtScheme = new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            BearerFormat = "JWT",
            Name         = "Authorization",
            In           = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type         = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme       = "bearer",
            Description  = "Paste your JWT token here (without 'Bearer ' prefix)."
        };
        var jwtRef = new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.Models.OpenApiSecurityScheme
                {
                    Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    {
                        Id   = bearerScheme,
                        Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme
                    }
                },
                Array.Empty<string>()
            }
        };

        options.AddSecurityDefinition(bearerScheme, jwtScheme);
        options.AddSecurityRequirement(jwtRef);
    });

    builder.Services.Configure<JwtSection>(builder.Configuration.GetSection("JwtSection"));
    var jwtSection = builder.Configuration.GetSection(nameof(JwtSection)).Get<JwtSection>();

    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ??
            throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured."),
            sql => sql.MigrationsAssembly("ServerLibrary")
            );
    });

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtSection!.Issuer,
            ValidAudience = jwtSection!.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSection.Key!))
        };
    });

    builder.Services.AddScoped<IUserAccount, UserAccountRepository>();

    builder.Services.AddScoped<IGenericRepositoryInterface<GeneralDepartment>, GeneralDepartmentRepository>();
    builder.Services.AddScoped<IGenericRepositoryInterface<Department>, DepartmentRepository>();
    builder.Services.AddScoped<IGenericRepositoryInterface<Branch>, BranchRepository>();

    builder.Services.AddScoped<IGenericRepositoryInterface<Country>, CountryRepository>();
    builder.Services.AddScoped<ICountryRepository, CountryRepository>();
    builder.Services.AddScoped<IGenericRepositoryInterface<City>, CityRepository>();
    builder.Services.AddScoped<ICityRepository, CityRepository>();
    builder.Services.AddScoped<IGenericRepositoryInterface<Town>, TownRepository>();
    builder.Services.AddScoped<ITownRepository, TownRepository>();
    builder.Services.AddScoped<ICountrySyncService, CountrySyncService>();
    builder.Services.AddScoped<ICapitalSyncService, CapitalSyncService>();

    builder.Services.AddHttpClient("RestCountries", client =>
    {
        client.BaseAddress = new Uri("https://restcountries.com/");
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    builder.Services.AddScoped<IGenericRepositoryInterface<Overtime>, OvertimeRepository>();
    builder.Services.AddScoped<IGenericRepositoryInterface<OvertimeType>, OvertimeTypeRepository>();

    builder.Services.AddScoped<IGenericRepositoryInterface<Sanction>, SanctionRepository>();
    builder.Services.AddScoped<IGenericRepositoryInterface<SanctionType>, SanctionTypeRepository>();

    builder.Services.AddScoped<IGenericRepositoryInterface<Vacation>, VacationRepository>();
    builder.Services.AddScoped<IGenericRepositoryInterface<VacationType>, VacationTypeRepository>();

    builder.Services.AddScoped<IGenericRepositoryInterface<Doctor>, DoctorRepository>();
    builder.Services.AddScoped<IGenericRepositoryInterface<Employee>, EmployeeRepository>();

    // Sentiment Analysis — singleton so the ML.NET model is trained once at startup
    builder.Services.AddSingleton<ISentimentService, SentimentService>();
    builder.Services.AddScoped<IEmployeeNoteRepository, EmployeeNoteRepository>();
    builder.Services.AddScoped<IHRRiskService, HRRiskService>();
    builder.Services.AddScoped<IHRAnalyticsService, HRAnalyticsService>();

    builder.Services.AddMemoryCache();

    // ── RabbitMQ / Event Bus ──────────────────────────────────────────────────
    // Bind RabbitMQ settings from appsettings.json → "RabbitMQ" section.
    // RabbitMqEventBus is a singleton (one shared connection per process).
    // App starts normally even if the broker is unreachable — events are dropped
    // with a Warning log and normal request flow is unaffected.
    builder.Services.Configure<RabbitMqSettings>(
        builder.Configuration.GetSection("RabbitMQ"));
    builder.Services.AddSingleton<IEventBus, RabbitMqEventBus>();
    builder.Services.AddHostedService<EmsAuditConsumer>();
    builder.Services.AddHostedService<SentimentWarmupService>();

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowBlazorWasm", policy =>
            policy.WithOrigins("https://localhost:7201")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials());
    });

    var app = builder.Build();

    // Force RabbitMqEventBus (singleton) to connect at startup — before the first
    // HTTP request arrives — so the "RabbitMQ connected" log is never associated
    // with any user's CorrelationId or UserId from the Serilog LogContext.
    app.Services.GetRequiredService<IEventBus>();

    // Always apply pending EF migrations on startup (Development and Production).
    // DevelopmentDataSeeder.SeedAsync also calls MigrateAsync, but it is only
    // invoked in Development, so Production would otherwise never migrate.
    using (var migrateScope = app.Services.CreateScope())
    {
        var dbContext = migrateScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    if (seedDemoDataOnStartup)
    {
        await DevelopmentDataSeeder.SeedAsync(app.Services);
    }

    // Seed HR analytics notes in ALL environments when EmployeeNotes is empty.
    // Ensures Trend, Morale, and Risk charts populate on first load.
    using (var analyticsScope = app.Services.CreateScope())
    {
        var analyticsDb     = analyticsScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var analyticsLogger = analyticsScope.ServiceProvider
            .GetService<ILoggerFactory>()?.CreateLogger("AnalyticsSeed");
        await DevelopmentDataSeeder.SeedAnalyticsNotesAsync(analyticsDb, analyticsLogger);
    }

    // One-time idempotent fixup: replace legacy "seed" placeholder in
    // EmployeeNotes.CreatedByUserId with the real admin display name.
    // Runs on every startup; no-op once all rows are already fixed.
    using (var fixScope = app.Services.CreateScope())
    {
        var fixDb     = fixScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fixLogger = fixScope.ServiceProvider
            .GetService<ILoggerFactory>()?.CreateLogger("SeederIdentityFix");
        await DevelopmentDataSeeder.FixSeederIdentityAsync(fixDb, fixLogger);
    }

    app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();

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
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("UserAgent",
                httpContext.Request.Headers["User-Agent"].ToString());
            diagnosticContext.Set("ContentType",
                httpContext.Response.ContentType ?? string.Empty);
        };
    });

    app.UseHttpsRedirection();

    // Swagger is enabled in all environments so the API is explorable during
    // demos and coursework marking regardless of ASPNETCORE_ENVIRONMENT.
    app.UseSwagger();
    app.UseSwaggerUI();

    app.UseCors("AllowBlazorWasm");
    app.UseAuthentication();
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
    Log.CloseAndFlush();
}
