using System.Diagnostics;
using System.Text;
using Gdzie.Kupic.Auth;
using Gdzie.Kupic.Domain;
using Gdzie.Kupic.Location;
using Gdzie.Kupic.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using Serilog.Context;
using Serilog.Formatting.Compact;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, _, cfg) =>
    {
        var serviceName = ctx.Configuration["ServiceName"] ?? "GdzieKupicService";
        var seqUrl      = ctx.Configuration["Seq:Url"];
        var seqApiKey   = ctx.Configuration["Seq:ApiKey"];

        cfg.ReadFrom.Configuration(ctx.Configuration)
           .Enrich.FromLogContext()
           .Enrich.WithMachineName()
           .Enrich.WithEnvironmentName()
           .Enrich.WithProperty("ServiceName", serviceName);

        if (ctx.HostingEnvironment.IsDevelopment())
            cfg.WriteTo.Console();
        else
            cfg.WriteTo.Console(new CompactJsonFormatter());

        if (!string.IsNullOrEmpty(seqUrl))
            cfg.WriteTo.Seq(seqUrl, apiKey: string.IsNullOrEmpty(seqApiKey) ? null : seqApiKey);
    });

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.CustomSchemaIds(type => type.FullName?.Replace('+', '.'));

        options.SwaggerDoc("v1", new Microsoft.OpenApi.OpenApiInfo
        {
            Title = "Gdzie.Kupic.API",
            Version = "v1",
        });

        options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.ParameterLocation.Header,
            Description = "Enter a valid JWT access token.",
        });

        options.AddSecurityRequirement((_) => new Microsoft.OpenApi.OpenApiSecurityRequirement
        {
            {
                new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer"),
                new List<string>()
            },
        });
    });

    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Frontend", policy => policy
            .WithOrigins(builder.Configuration["Cors:AllowedOrigin"] ?? "http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod());
    });

    builder.Services.AddControllers();
    builder.Services.InstallDomain();
    // Skipped under WebApplicationFactory-based integration tests: the test factory registers
    // AppDbContext with the EF Core InMemory provider instead, so the real Npgsql provider
    // must never be registered here (EF Core does not allow two providers in the same
    // service collection).
    if (!builder.Environment.IsEnvironment("Testing"))
    {
        builder.Services.InstallStorageModule(builder.Configuration);
    }
    builder.Services.InstallLocationModule(builder.Configuration);
    builder.Services.InstallAuthModule(builder.Configuration);

    var jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
        ?? throw new InvalidOperationException($"Missing '{JwtSettings.SectionName}' configuration section.");

    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            };
        });

    builder.Services.AddAuthorization();

    var app = builder.Build();

    // Skipped under WebApplicationFactory-based integration tests: the test factory replaces
    // the DbContext with the EF Core InMemory provider (which does not support relational
    // migrations) and seeds/creates the schema itself.
    if (!app.Environment.IsEnvironment("Testing"))
    {
        await app.UseStorageModule();
    }

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Gdzie.Kupic.API v1");
        });
    }

    // Enrich every log entry within a request with TraceId and CorrelationId
    app.Use(async (context, next) =>
    {
        var traceId       = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault() ?? traceId;

        using (LogContext.PushProperty("TraceId", traceId))
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await next();
        }
    });

    // Single structured log line per request: method, path, status, elapsed
    app.UseSerilogRequestLogging();

    app.UseCors("Frontend");

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "GdzieKupicService" }));

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

// Exposes the implicitly-generated top-level Program class so it can be referenced
// by WebApplicationFactory<Program> in integration tests.
public partial class Program;
