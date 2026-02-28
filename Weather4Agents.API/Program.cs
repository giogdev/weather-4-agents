using Scalar.AspNetCore;
using Weather4Agents.Application;
using Weather4Agents.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

#if DEBUG
builder.Configuration.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true);
#endif

#if RELEASE
builder.Configuration.AddJsonFile("appsettings.Release.json", optional: true, reloadOnChange: true);
#endif

builder.Services.AddControllers();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Info.Title = "Weather4Agents API";
        document.Info.Version = "v1";
        document.Info.Description = "Middleware API to retrieve weather forecast data for AI agents.";
        return Task.CompletedTask;
    });
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
   
}

app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.Title = "Weather4Agents API";
    options.Theme = ScalarTheme.DeepSpace;
});

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
