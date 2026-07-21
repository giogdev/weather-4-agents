using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Weather4Agents.API.Errors;
using Weather4Agents.API.Filters;
using Weather4Agents.API.OpenApi;
using Weather4Agents.API.Settings;
using Weather4Agents.Application;
using Weather4Agents.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Environment-specific settings (appsettings.{Environment}.json) are loaded by the host
// based on ASPNETCORE_ENVIRONMENT; secrets come from User Secrets in Development and
// environment variables in Production — never from committed files.

builder.Services.AddControllers();

// Central error handling: domain exceptions and unexpected failures are turned into
// ProblemDetails responses by GlobalExceptionHandler instead of per-controller try/catch.
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddOpenApi(options =>
{
    options.AddOperationTransformer(new XmlDocumentationTransformer());
    options.AddSchemaTransformer(new XmlDocumentationSchemaTransformer());
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Weather4Agents API";
        document.Info.Version = "v1";
        document.Info.Description = "Middleware API to retrieve weather forecast data for AI agents or custom integrations";
        document.Info.Contact = new()
        {
            Name = "@giogdev",
            Url = new Uri("https://github.com/giogdev/weather-4-agents"),
        };
        return Task.CompletedTask;
    });
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// System clock; integration tests replace this with a fake to pin time.
builder.Services.AddSingleton(TimeProvider.System);

// Opt-in location whitelist: the filter reads WeatherScraping settings and rejects
// non-configured locations before the action runs (so before any scrape). Registered as a
// singleton so the normalized whitelist is built once, not per request; IOptions is singleton
// too, so this lifetime is safe.
builder.Services.AddSingleton<ServableLocationFilter>();

// Per-IP fixed-window rate limiting on the weather endpoints. Settings are validated on start;
// the partitioner reads the validated IOptions instance so there is a single source of truth.
builder.Services.AddOptions<RateLimitingSettings>()
    .Bind(builder.Configuration.GetSection(RateLimitingSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy(RateLimitingSettings.PolicyName, httpContext =>
    {
        var settings = httpContext.RequestServices
            .GetRequiredService<IOptions<RateLimitingSettings>>().Value;

        // A disabled limiter still needs a policy so the controller attribute resolves;
        // GetNoLimiter lets every request through.
        if (!settings.Enabled)
            return RateLimitPartition.GetNoLimiter("disabled");

        // Partition by client IP. The in-memory test host has no remote IP, so all requests
        // share a single partition there — which is exactly what the 429 test relies on.
        var clientKey = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(clientKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = settings.PermitLimit,
            Window = TimeSpan.FromSeconds(settings.WindowSeconds),
            QueueLimit = settings.QueueLimit,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        });
    });

    // Reject with a ProblemDetails (consistent with the rest of the API) and advertise Retry-After.
    options.OnRejected = async (context, cancellationToken) =>
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString(NumberFormatInfo.InvariantInfo);

        var problemDetailsService = context.HttpContext.RequestServices
            .GetRequiredService<IProblemDetailsService>();

        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context.HttpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many requests",
                Detail = "Rate limit exceeded. Please retry later."
            }
        });
    };
});

var app = builder.Build();

// Route unhandled exceptions through GlobalExceptionHandler before anything else.
app.UseExceptionHandler();

// OpenAPI document and Scalar UI are intentionally exposed in every environment:
// the API is meant for agents and self-hosted LAN deployments, where the schema
// is part of the product surface.
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "Weather4Agents API";
    options.Theme = ScalarTheme.DeepSpace;
});

//app.UseHttpsRedirection();
//app.UseAuthorization();
app.UseRateLimiter();
app.MapControllers();

app.Run();

// Exposes the implicit entry-point class to WebApplicationFactory<Program> in integration tests.
public partial class Program { }
