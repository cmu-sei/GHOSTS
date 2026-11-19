using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Html;

namespace Ghosts.Pandora.Infrastructure;

/// <summary>
/// Renders post, comment and message text safely. Text from users, NPCs and LLM workflows is
/// encoded; the only markup kept is the image tag Pandora itself appends to a post or comment
/// for an uploaded image (see FilesController.FileInputModel.SaveImageAsync).
/// </summary>
public static partial class MessageHtml
{
    [GeneratedRegex("""(?:<br\s*/?>|\s)?<img src="(/images/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/image\.(?:jpg|png))"\s*/?>""",
        RegexOptions.IgnoreCase)]
    private static partial Regex UploadedImage();

    public static string ToHtml(string message)
    {
        if (string.IsNullOrEmpty(message))
            return string.Empty;

        var html = new StringBuilder();
        var position = 0;
        foreach (Match match in UploadedImage().Matches(message))
        {
            html.Append(WebUtility.HtmlEncode(message[position..match.Index]));
            html.Append($"<br/><img src=\"{match.Groups[1].Value}\"/>");
            position = match.Index + match.Length;
        }
        html.Append(WebUtility.HtmlEncode(message[position..]));
        return html.ToString();
    }

    public static IHtmlContent Render(string message) => new HtmlString(ToHtml(message));
}
