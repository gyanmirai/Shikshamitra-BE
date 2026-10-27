using System;
using System.Linq;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students;

public class StudentCredentialEmailTemplate_Tests
{
	[Fact]
	public void First_credentials_show_the_four_details_and_an_unlinked_store_badge()
	{
		var (state, user) = Sample();
		var document = Render(state, user, "DemoOnly!4827", "https://school.example/account/reset-password?token=sample", true);
		Value(document, "School code").ShouldBe("DDVS");
		Value(document, "Username").ShouldBe("parent@example.com");
		Value(document, "Temporary password").ShouldBe("DemoOnly!4827");
		Value(document, "Student").ShouldBe("Aarav");
		var badge = document.QuerySelector("img[alt='Get it on Google Play']");
		badge.ShouldNotBeNull();
		new Uri(badge.GetAttribute("src")!).Host.ShouldBe("play.google.com");
		badge.Closest("a").ShouldBeNull();
		document.QuerySelectorAll("a").ShouldBeEmpty();
		document.Body!.TextContent.ShouldNotContain("coming soon", Case.Insensitive);
		document.Body.TextContent.ShouldContain("Change your temporary password when you first sign in.");
	}

	[Fact]
	public void Existing_account_details_never_invent_or_repeat_a_temporary_password()
	{
		var (state, user) = Sample();
		var document = Render(state, user, null, null, false);
		Details(document).Length.ShouldBe(3);
		document.Body!.TextContent.ShouldNotContain("Temporary password");
		document.Body.TextContent.ShouldContain("Use your existing password");
		document.QuerySelectorAll("a").ShouldBeEmpty();
	}

	[Theory]
	[InlineData(true, "Open secure setup link")]
	[InlineData(false, "Reset your password")]
	public void Recovery_without_a_resent_password_keeps_one_text_only_owner_link(bool pending, string label)
	{
		var (state, user) = Sample();
		const string url = "https://school.example/account/reset-password?userId=sample&resetToken=encoded%2Btoken&tenantId=school";
		var document = Render(state, user, null, url, pending);
		var link = document.QuerySelectorAll("a").Single();
		link.TextContent.ShouldBe(label);
		link.GetAttribute("href").ShouldBe(url);
		link.GetAttribute("role").ShouldBeNull();
		document.QuerySelectorAll("button").ShouldBeEmpty();
		document.Body!.TextContent.ShouldNotContain("Temporary password");
	}

	[Theory]
	[InlineData("javascript:alert(1)")]
	[InlineData("data:text/html,sample")]
	[InlineData("/relative-reset")]
	public void Invalid_recovery_destinations_are_not_rendered_as_links(string destination)
	{
		var (state, user) = Sample();
		Render(state, user, null, destination, true).QuerySelectorAll("a").ShouldBeEmpty();
	}

	[Fact]
	public void School_student_username_greeting_and_password_are_rendered_as_text()
	{
		var (state, user) = Sample();
		const string unsafeText = "<img src=x onerror=alert(1)> & \"school\"";
		state = state with { SchoolName = unsafeText, StudentName = "<script>student</script>" };
		user.Name = unsafeText;
		var document = Render(state, user, "<strong>secret & value</strong>", null, false);
		document.QuerySelectorAll("script,[onerror],strong").ShouldBeEmpty();
		document.QuerySelectorAll("img").Length.ShouldBe(1);
		Value(document, "School code").ShouldBe(unsafeText);
		Value(document, "Student").ShouldBe("<script>student</script>");
		Value(document, "Temporary password").ShouldBe("<strong>secret & value</strong>");
		document.Body!.TextContent.ShouldContain("Hello " + unsafeText + ",");
		var unsafeUser = new IdentityUser(Guid.NewGuid(), unsafeText, "sample@example.com");
		Value(Render(state, unsafeUser, null, null, false), "Username").ShouldBe(unsafeText);
	}

	[Fact]
	public void Long_passwords_remain_complete_and_have_narrow_client_wrapping()
	{
		var (state, user) = Sample();
		var password = "Sm!9" + new string('A', 96) + "a";
		var document = Render(state, user, password, null, true);
		Value(document, "Temporary password").ShouldBe(password);
		var secret = Details(document).Single(row => row.QuerySelector("th")!.TextContent == "Temporary password")
			.QuerySelector("td span")!;
		secret.GetAttribute("style")!.ShouldContain("word-break:break-all");
		document.QuerySelector("meta[name='viewport']").ShouldNotBeNull();
		document.QuerySelector("style")!.TextContent.ShouldContain("max-width:480px");
	}

	[Fact]
	public void Missing_optional_greeting_and_blocked_images_keep_readable_content()
	{
		var (state, user) = Sample();
		user.Name = null;
		var document = Render(state, user, "DemoOnly!4827", null, true);
		document.Body!.TextContent.ShouldContain("Hello,");
		document.Body.TextContent.ShouldContain("Need help? Contact your school office.");
		document.QuerySelector("img")!.GetAttribute("alt").ShouldBe("Get it on Google Play");
		document.QuerySelector("div")!.TextContent.ShouldNotContain("DemoOnly!4827");
	}

	private static IElement[] Details(IDocument document) =>
		document.QuerySelectorAll("table[aria-label='Account details'] tr").ToArray();

	private static string Value(IDocument document, string label) =>
		Details(document).Single(row => row.QuerySelector("th")!.TextContent.Trim() == label)
			.QuerySelector("td")!.TextContent.Trim();

	private static IDocument Render(StudentCredentialEmailEligibility state, IdentityUser user,
		string? password, string? resetUrl, bool pending) =>
		new HtmlParser().ParseDocument(StudentCredentialEmailTemplate.Build(state, user, password, resetUrl, pending));

	private static (StudentCredentialEmailEligibility State, IdentityUser User) Sample()
	{
		var user = new IdentityUser(Guid.NewGuid(), "parent@example.com", "parent@example.com") { Name = "Rohan" };
		var state = new StudentCredentialEmailEligibility(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), user.Id,
			user.Email!, "Aarav", true, true, null, "https://school.example/login", "DDVS");
		return (state, user);
	}
}
