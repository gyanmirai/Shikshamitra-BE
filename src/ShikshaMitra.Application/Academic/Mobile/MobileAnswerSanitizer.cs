using System;
using System.Linq;
using System.Text;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Ganss.Xss;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Academic.Mobile;

public record MobileAnswer(string? Text, string? Html);

public class MobileAnswerSanitizer : ITransientDependency
{
	public MobileAnswer Sanitize(MobileSubmissionInput input)
	{
		if (input.TextAnswer != null && input.TextAnswerHtml != null)
			throw new BusinessException(MobileSubmissionErrorCodes.AmbiguousAnswer);
		if (input.TextAnswerHtml == null)
		{
			ValidateLength(input.TextAnswer);
			return new(input.TextAnswer, null);
		}
		if (Encoding.UTF8.GetByteCount(input.TextAnswerHtml) > 1024 * 1024)
			throw new BusinessException(MobileSubmissionErrorCodes.SubmissionTooLarge);
		var parser = new HtmlParser();
		var source = parser.ParseDocument(input.TextAnswerHtml);
		foreach (var element in source.QuerySelectorAll("script,style,iframe,object,embed,template,svg,math").ToList()) element.Remove();
		var sanitizer = new HtmlSanitizer { KeepChildNodes = true };
		sanitizer.AllowedTags.Clear();
		foreach (var tag in new[] { "p", "br", "strong", "b", "em", "i", "u", "ul", "ol", "li", "code", "pre" })
			sanitizer.AllowedTags.Add(tag);
		sanitizer.AllowedAttributes.Clear();
		sanitizer.AllowedCssProperties.Clear();
		var html = sanitizer.Sanitize(source.Body!.InnerHtml);
		var document = parser.ParseDocument(html);
		var text = new StringBuilder();
		foreach (var node in document.Body!.ChildNodes) AppendText(node, text);
		// A final block newline is structural (the same terminal newline used by Quill).
		if (text.Length > 0 && text[^1] == '\n') text.Length--;
		var plain = text.ToString();
		ValidateLength(plain);
		return new(plain, html);
	}

	private static void ValidateLength(string? text)
	{
		if (text?.Length > 100000) throw new BusinessException(MobileSubmissionErrorCodes.SubmissionTooLarge);
	}

	private static void AppendText(INode node, StringBuilder text)
	{
		if (node is IText value) { text.Append(value.Data); return; }
		if (node is not IElement element) return;
		if (element.LocalName == "br") { text.Append('\n'); return; }
		var start = text.Length;
		foreach (var child in element.ChildNodes) AppendText(child, text);
		if (new[] { "p", "li", "pre" }.Contains(element.LocalName) &&
			(text.Length == start || text[^1] != '\n')) text.Append('\n');
	}
}
