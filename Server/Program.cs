using BaseLibrary.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
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

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.Configure<JwtSection>(builder.Configuration.GetSection("JwtSection"));
    var jwtSection = builder.Configuration.GetSection(nameof(JwtSection)).Get<JwtSection>();

    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ??
            throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured."));
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
    builder.Services.AddScoped<IGenericRepositoryInterface<Town>, TownRepository>();
    builder.Services.AddScoped<ICountrySyncService, CountrySyncService>();

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

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowBlazorWasm", policy =>
            policy.WithOrigins("https://localhost:7201")
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials());
    });

    var app = builder.Build();

    app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
    app.UseMiddleware<CorrelationIdMiddleware>();

    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";

        opts.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value);
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("UserAgent",
                httpContext.Request.Headers["User-Agent"].ToString());
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