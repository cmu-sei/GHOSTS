using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace ghosts.api.Infrastructure.Formatters;

public class MarkdownOutputFormatter : TextOutputFormatter
{
    public MarkdownOutputFormatter()
    {
        SupportedMediaTypes.Add("text/markdown");
        SupportedEncodings.Add(Encoding.UTF8);
    }

    protected override bool CanWriteType(Type type)
    {
        // You can customize this list
        return type != null && (typeof(IEnumerable<object>).IsAssignableFrom(type) || type.IsClass);
    }

    public override bool CanWriteResult(OutputFormatterCanWriteContext context)
    {
        var acceptHeader = context.HttpContext.Request.Headers["Accept"].ToString();
        return acceptHeader.Contains("text/markdown", StringComparison.OrdinalIgnoreCase);
    }

    public override async Task WriteResponseBodyAsync(OutputFormatterWriteContext context, Encoding selectedEncoding)
    {
        if (context.Object != null)
        {
            var type = context.Object.GetType();
            var sb = new StringBuilder();

            if (context.Object is IEnumerable enumerable && type.IsGenericType)
            {
                var elementType = type.GetGenericArguments().FirstOrDefault() ?? typeof(object);
                var props = elementType.GetProperties();

                sb.AppendLine("| " + string.Join(" | ", props.Select(p => p.Name)) + " |");
                sb.AppendLine("|" + string.Join("|", props.Select(_ => "---")) + "|");

                foreach (var item in enumerable)
                {
                    sb.AppendLine("| " + string.Join(" | ", props.Select(p => FormatValue(p.GetValue(item)))) + " |");
                }
            }
            else
            {
                var props = type.GetProperties();
                sb.AppendLine("| Property | Value |");
                sb.AppendLine("|----------|-------|");
                foreach (var prop in props)
                {
                    var value = prop.GetValue(context.Object)?.ToString() ?? "";
                    sb.AppendLine($"| {prop.Name} | {value} |");
                }
            }

            await context.HttpContext.Response.WriteAsync(sb.ToString());
        }
    }

    private static string FormatValue(object value, int depth = 0)
    {
        if (value == null) return "";

        if (depth > 3) return "...";

        var type = value.GetType();

        if (type.IsPrimitive || value is string || value is DateTime || value is Guid || value is decimal)
            return value.ToString();

        if (value is IEnumerable e && !(value is string))
        {
            var items = new List<string>();
            foreach (var item in e)
            {
                items.Add(FormatValue(item, depth + 1));
            }
            return "[" + string.Join(", ", items) + "]";
        }

        var props = type.GetProperties();
        var parts = props.Select(p =>
        {
            object val;
            try { val = p.GetValue(value); }
            catch { val = "<?>"; }
            return $"{p.Name}: {FormatValue(val, depth + 1)}";
        });

        return "{" + string.Join(", ", parts) + "}";
    }
}
