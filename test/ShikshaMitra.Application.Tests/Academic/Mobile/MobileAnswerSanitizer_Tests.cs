using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Academic.Mobile;

public class MobileAnswerSanitizer_Tests
{
	private readonly MobileAnswerSanitizer sanitizer = new();
	[Fact]
	public void Sanitizes_attributes_and_preserves_supported_formatting()
	{
		var answer = sanitizer.Sanitize(new() { TextAnswerHtml = "<p onclick='bad()' style='color:red'>A &amp; B</p><ul><li><u>Item</u></li></ul><pre>code</pre><script>bad()</script>" });
		answer.Text.ShouldBe("A & B\nItem\ncode");
		answer.Html.ShouldNotContain("onclick"); answer.Html.ShouldNotContain("style"); answer.Html.ShouldNotContain("script");
		answer.Html.ShouldContain("<u>Item</u>");
	}
	[Fact]
	public void Counts_decoded_utf16_text_without_terminal_editor_newline()
	{
		var text = new string('x', 99998) + "😀";
		sanitizer.Sanitize(new() { TextAnswerHtml = $"<p><strong>{text}</strong></p>" }).Text!.Length.ShouldBe(100000);
		Should.Throw<BusinessException>(() => sanitizer.Sanitize(new() { TextAnswerHtml = $"<p>{text}x</p>" }));
		sanitizer.Sanitize(new() { TextAnswerHtml = "<p>&nbsp;</p>" }).Text.ShouldBe("\u00a0");
	}
	[Fact]
	public void Rejects_ambiguous_answers_and_oversize_html()
	{
		Should.Throw<BusinessException>(() => sanitizer.Sanitize(new() { TextAnswer = "plain", TextAnswerHtml = "<p>html</p>" }));
		Should.Throw<BusinessException>(() => sanitizer.Sanitize(new() { TextAnswerHtml = new string('界', 400000) }));
	}
}
