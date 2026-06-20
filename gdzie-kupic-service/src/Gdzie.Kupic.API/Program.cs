using System.Diagnostics;
using Gdzie.Kupic.Storage;
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

        cfg.ReadFrom.Configuration(ctx.Configuration)   // reads Serilog:MinimumLevel from appsettings
           .Enrich.FromLogContext()
           .Enrich.WithMachineName()
           .Enrich.WithEnvironmentName()
           .Enrich.WithProperty("ServiceName", serviceName);

        // Human-readable console in Development, compact JSON everywhere else
        if (ctx.HostingEnvironment.IsDevelopment())
            cfg.WriteTo.Console();
        else
            cfg.WriteTo.Console(new CompactJsonFormatter());

        if (!string.IsNullOrEmpty(seqUrl))
            cfg.WriteTo.Seq(seqUrl, apiKey: string.IsNullOrEmpty(seqApiKey) ? null : seqApiKey);
    });

    builder.Services.AddOpenApi();
    builder.Services.AddControllers();
    builder.Services.AddStorage(builder.Configuration);

    var app = builder.Build();

    await app.UseStorage();

    if (app.Environment.IsDevelopment())
        app.MapOpenApi();

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

    app.MapControllers();
    app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "GdzieKupicService" }));

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
