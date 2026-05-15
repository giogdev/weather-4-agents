using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System.Reflection;
using System.Xml.XPath;

namespace Weather4Agents.API.OpenApi;

/// <summary>
/// Injects XML documentation comments (summary, param descriptions) into the OpenAPI definition.
/// </summary>
internal sealed class XmlDocumentationTransformer : IOpenApiOperationTransformer
{
    private readonly XPathNavigator? _navigator;

    public XmlDocumentationTransformer()
    {
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (!File.Exists(xmlPath))
            return;

        var document = new XPathDocument(xmlPath);
        _navigator = document.CreateNavigator();
    }

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (_navigator is null)
            return Task.CompletedTask;

        if (context.Description.ActionDescriptor is not ControllerActionDescriptor descriptor)
            return Task.CompletedTask;

        var memberName = GetXmlMemberName(descriptor.MethodInfo);
        var node = _navigator.SelectSingleNode($"/doc/members/member[@name='{memberName}']");
        if (node is null)
            return Task.CompletedTask;

        var summary = node.SelectSingleNode("summary")?.Value?.Trim();
        if (!string.IsNullOrEmpty(summary))
            operation.Summary = summary;

        foreach (var parameter in operation.Parameters ?? [])
        {
            var paramNode = node.SelectSingleNode($"param[@name='{parameter.Name}']");
            var description = paramNode?.Value?.Trim();
            if (!string.IsNullOrEmpty(description))
                parameter.Description = description;
        }

        return Task.CompletedTask;
    }

    private static string GetXmlMemberName(MethodInfo method)
    {
        var declaringType = method.DeclaringType!.FullName;
        var parameters = method.GetParameters();

        if (parameters.Length == 0)
            return $"M:{declaringType}.{method.Name}";

        var paramTypes = string.Join(",", parameters.Select(p => GetTypeName(p.ParameterType)));
        return $"M:{declaringType}.{method.Name}({paramTypes})";
    }

    private static string GetTypeName(Type type)
    {
        if (type.IsGenericType)
        {
            var name = type.GetGenericTypeDefinition().FullName!;
            name = name[..name.IndexOf('`')];
            var args = string.Join(",", type.GetGenericArguments().Select(GetTypeName));
            return $"{name}{{{args}}}";
        }

        return type.FullName ?? type.Name;
    }
}
