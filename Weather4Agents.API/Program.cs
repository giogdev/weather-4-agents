using Scalar.AspNetCore;
using Weather4Agents.API.OpenApi;
using Weather4Agents.Application;
using Weather4Agents.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Environment-specific settings (appsettings.{Environment}.json) are loaded by the host
// based on ASPNETCORE_ENVIRONMENT; secrets come from User Secrets in Development and
// environment variables in Production — never from committed files.

builder.Services.AddControllers();
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

var app = builder.Build();

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
app.MapControllers();

app.Run();

// Exposes the implicit entry-point class to WebApplicationFactory<Program> in integration tests.
public partial class Program { }
