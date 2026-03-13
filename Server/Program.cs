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
using System.Text;

// ── Serilog bootstrap logger (captures startup errors before host builds) ─────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Serilog: replace default .NET logging ────────────────────────────────
    builder.Host.UseSerilog((ctx, services, cfg) =>
    {
        cfg
            // Read MinimumLevel overrides and extra sinks from appsettings
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)

            // Context enrichment — appears on every log event
            .Enrich.FromLogContext()                             // per-request properties (CorrelationId)
            .Enrich.WithMachineName()                           // hostname (useful in multi-server deployments)
            .Enrich.WithEnvironmentName()                       // Development / Staging / Production
            .Enrich.WithProperty("Application", "EMS-API")     // identify this service in Seq

            // Sinks
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.Seq(
                ctx.Configuration["Serilog:SeqUrl"] ?? "http://localhost:5341");
    });

    // ── Services ─────────────────────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.Configure<JwtSection>(builder.Configuration.GetSection("JwtSection"));
    var jwtSection = builder.Configuration.GetSection(nameof(JwtSection)).Get<JwtSection>();

    builder.Services.AddDbContext<AppDbContext>(options =>
    {
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection") ??
                throw new InvalidOperationException("Sorry, your connection is not found"));
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSection.Key!))
        };
    });

    builder.Services.AddScoped<IUserAccount, UserAccountRepository>();

    builder.Services.AddScoped<IGenericRepositoryInterface<GeneralDepartment>, GeneralDepartmentRepository>();
    builder.Services.AddScoped<IGenericRepositoryInterface<Department>, DepartmentRepository>();
    builder.Services.AddScoped<IGenericRepositoryInterface<Branch>, BranchRepository>();

    builder.Services.AddScoped<IGenericRepositoryInterface<Country>, CountryRepository>();
    builder.Services.AddScoped<IGenericRepositoryInterface<City>, CityRepository>();
    builder.Services.AddScoped<IGenericRepositoryInterface<Town>, TownRepository>();

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
        options.AddPolicy("AllowBlazorWasm", b =>
            b.WithOrigins("https://localhost:7201")
             .AllowAnyMethod()
             .AllowAnyHeader()
             .AllowCredentials()
        );
    });

    // ── Pipeline ─────────────────────────────────────────────────────────────
    var app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseMiddleware<CorrelationIdMiddleware>();   // push CorrelationId before request log
    app.UseSerilogRequestLogging(opts =>
    {
        // Enrich the structured request completion log with extra fields
        opts.EnrichDiagnosticContext = (diagCtx, httpCtx) =>
        {
            diagCtx.Set("RequestHost", httpCtx.Request.Host.Value ?? "");
            diagCtx.Set("RequestScheme", httpCtx.Request.Scheme);
            diagCtx.Set("UserAgent", httpCtx.Request.Headers["User-Agent"].ToString());
        };
    });
    app.UseCors("AllowBlazorWasm");
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
