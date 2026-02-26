using Microsoft.AspNetCore.Mvc;
using Weather4Agents.Application.Interfaces.Scrapers;

namespace Weather4Agents.API.Controllers;

[ApiController]
[Route("api/configurations")]
public class ConfigurationsController : ControllerBase
{
    private readonly IWeatherProviderResolver _resolver;

    public ConfigurationsController(IWeatherProviderResolver resolver)
    {
        _resolver = resolver;
    }

    /// <summary>Returns the list of available weather provider names.</summary>
    [HttpGet("providers")]
    public IActionResult GetProviders()
    {
        var providers = _resolver.GetAvailableProviders();
        return Ok(providers);
    }
}
