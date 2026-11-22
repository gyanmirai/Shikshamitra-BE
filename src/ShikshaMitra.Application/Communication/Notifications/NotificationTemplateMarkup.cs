using System;
using System.Linq;
using AngleSharp.Html.Parser;
using Ganss.Xss;

namespace ShikshaMitra.Communication.Notifications;

internal static class NotificationTemplateMarkup
{
	public static string? SanitizeEmail(string body, Func<string?, bool> allowedLink)
	{
		var parser = new HtmlParser();
		var source = parser.ParseDocument(body);
		foreach (var node in source.QuerySelectorAll("script,style,iframe,object,embed,template,svg,math").ToArray()) node.Remove();
		if (source.QuerySelectorAll("a[href]").Any(anchor => !allowedLink(anchor.GetAttribute("href")))) return null;
		var sanitizer = new HtmlSanitizer { KeepChildNodes = true };
		sanitizer.AllowedTags.Clear();
		foreach (var tag in new[] { "p", "br", "strong", "b", "em", "i", "ul", "ol", "li", "a" }) sanitizer.AllowedTags.Add(tag);
		sanitizer.AllowedAttributes.Clear();
		sanitizer.AllowedAttributes.Add("href");
		sanitizer.AllowedCssProperties.Clear();
		sanitizer.AllowedSchemes.Clear();
		sanitizer.AllowedSchemes.Add("https");
		var clean = sanitizer.Sanitize(source.Body!.InnerHtml);
		var result = parser.ParseDocument(clean);
		return result.QuerySelectorAll("a[href]").Any(anchor => !allowedLink(anchor.GetAttribute("href"))) ? null : result.Body!.InnerHtml;
	}
}
