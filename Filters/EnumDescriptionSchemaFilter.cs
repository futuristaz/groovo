using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.ComponentModel;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;

[ExcludeFromCodeCoverage]
public class EnumDescriptionSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type.IsEnum)
        {
            schema.Description ??= string.Empty;
            schema.Description += "<p><b>Possible values:</b></p><ul>";

            foreach (var name in Enum.GetNames(context.Type))
            {
                var member = context.Type.GetMember(name).First();
                var description = member.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
                var value = (int)Enum.Parse(context.Type, name);
                schema.Description += $"<li><b>{value} = {name}</b>: {description}</li>";
            }

            schema.Description += "</ul>";
        }
    }
}
