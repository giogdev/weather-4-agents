using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Xml.XPath;

namespace Weather4Agents.API.OpenApi;

/// <summary>
/// Injects XML documentation comments (summary) on schema properties into the OpenAPI definition.
/// Loads XML files from all Weather4Agents assemblies present in the output directory.
/// </summary>
internal sealed class XmlDocumentationSchemaTransformer : IOpenApiSchemaTransformer
{
    private readonly IReadOnlyList<XPathNavigator> _navigators;

    public XmlDocumentationSchemaTransformer()
    {
        var xmlFiles = Directory.GetFiles(AppContext.BaseDirectory, "Weather4Agents.*.xml");

        _navigators = xmlFiles
            .Where(File.Exists)
            .Select(path =>
            {
                var doc = new XPathDocument(path);
                return doc.CreateNavigator();
            })
            .ToList();
    }

    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (_navigators.Count == 0 || schema.Properties is not { Count: > 0 })
            return Task.CompletedTask;

        var type = context.JsonTypeInfo.Type;

        foreach (var (propName, propSchema) in schema.Properties)
        {
            var propInfo = type.GetProperties()
                .FirstOrDefault(p => string.Equals(p.Name, propName, StringComparison.OrdinalIgnoreCase));

            if (propInfo is null)
                continue;

            var memberName = $"P:{type.FullName}.{propInfo.Name}";
            var summary = GetSummary(memberName);

            if (!string.IsNullOrEmpty(summary))
                propSchema.Description = summary;
        }

        return Task.CompletedTask;
    }

    private string? GetSummary(string memberName)
    {
        foreach (var navigator in _navigators)
        {
            var node = navigator.SelectSingleNode($"/doc/members/member[@name='{memberName}']");
            var summary = node?.SelectSingleNode("summary")?.Value?.Trim();
            if (!string.IsNullOrEmpty(summary))
                return summary;
        }

        return null;
    }
}
