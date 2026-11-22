using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using ShikshaMitra.Academic.Notifications;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationTemplateRenderer_Tests
{
	private readonly Guid _tenantId = Guid.NewGuid();
	private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();
	private readonly IRepository<CommunicationTemplateRevision, Guid> _revisions = Substitute.For<IRepository<CommunicationTemplateRevision, Guid>>();
	private readonly List<CommunicationTemplateRevision> _rows = [];
	private readonly NotificationTemplateRenderer _renderer;
	private const string EventKey = "Academic.Homework.Published";

	public NotificationTemplateRenderer_Tests()
	{
		_tenant.Id.Returns(_tenantId);
		_revisions.GetQueryableAsync().Returns(_rows.AsQueryable());
		var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
			["App:ClientUrl"] = "https://school.example"
		}).Build();
		_renderer = new(_tenant, _revisions, new AsyncQueryableExecuter([]),
			new NotificationTemplateVariableValidator([new AcademicNotificationCatalogue([])], configuration));
	}

	[Fact]
	public async Task Renders_allowlisted_values_once_and_keeps_single_braces_literal()
	{
		var revision = Add(CommunicationChannelType.InApp, "Homework", "Hello {{recipient_name}}: {open} {{task_title}} {close}");
		var values = Values();
		values["task_title"] = new(NotificationVariableType.Text, "{{school_name}} exercise");
		var result = await RenderAsync(revision, values);
		result.IsSuccess.ShouldBeTrue();
		result.Body.ShouldBe("Hello Example guardian: {open} {{school_name}} exercise {close}");
		result.LinkUrl.ShouldBe("https://school.example/academic/homework/one");
		result.ContentHash.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task Email_encodes_personal_values_and_removes_unsafe_template_markup()
	{
		var revision = Add(CommunicationChannelType.Email, "Homework", "<p>Hello {{recipient_name}}</p><script>window.secret=1</script><a href=\"{{source_link}}\" onclick=\"alert(1)\">View</a>");
		var values = Values();
		values["recipient_name"] = new(NotificationVariableType.Text, "<img src=x onerror=alert(1)>");
		var result = await RenderAsync(revision, values);
		result.IsSuccess.ShouldBeTrue();
		result.Body.ShouldNotBeNull();
		result.Body!.ShouldContain("&lt;img");
		result.Body.ShouldNotContain("<img");
		result.Body.ShouldNotContain("<script");
		result.Body.ShouldNotContain("window.secret");
		result.Body.ShouldNotContain("onclick");
		result.Body!.ShouldContain("href=\"https://school.example/academic/homework/one\"");
	}

	[Fact]
	public async Task Email_subject_remains_plain_text_while_the_body_encodes_html_characters()
	{
		var revision = Add(CommunicationChannelType.Email, "Hello {{recipient_name}}", "<p>Hello {{recipient_name}}</p>");
		var values = Values();
		values["recipient_name"] = new(NotificationVariableType.Text, "A & B");
		var result = await RenderAsync(revision, values);
		result.IsSuccess.ShouldBeTrue();
		result.Subject.ShouldBe("Hello A & B");
		result.Body.ShouldNotBeNull();
		result.Body!.ShouldContain("A &amp; B");
	}

	[Fact]
	public async Task Required_values_are_enforced_even_when_the_template_does_not_reference_them()
	{
		var revision = Add(CommunicationChannelType.InApp, "Homework", "A new task is available.");
		var values = Values();
		values.Remove("recipient_name");
		var result = await RenderAsync(revision, values);
		result.IsSuccess.ShouldBeFalse();
		result.ErrorCode.ShouldBe(NotificationErrorCodes.RequiredVariableMissing);
		result.Body.ShouldBeNull();
	}

	[Theory]
	[InlineData("{{unknown_variable}}")]
	[InlineData("{{recipient.name}}")]
	[InlineData("{{#each student}}")]
	[InlineData("{{ recipient_name }}")]
	public async Task Unknown_placeholders_and_expressions_are_not_executable(string body)
	{
		var result = await RenderAsync(Add(CommunicationChannelType.InApp, "Homework", body));
		result.IsSuccess.ShouldBeFalse();
		result.ErrorCode.ShouldBe(NotificationErrorCodes.TemplateInvalid);
	}

	[Theory]
	[InlineData("https://another.example/academic/homework")]
	[InlineData("https://school.example.evil.test/homework")]
	[InlineData("http://school.example/homework")]
	[InlineData("https://attacker@school.example/homework")]
	[InlineData("//school.example/homework")]
	[InlineData("javascript:alert(1)")]
	public async Task Links_must_be_absolute_https_at_the_configured_school_origin(string link)
	{
		var revision = Add(CommunicationChannelType.Email, "Homework", "{{source_link}}");
		var values = Values();
		values["source_link"] = new(NotificationVariableType.Link, link);
		(await RenderAsync(revision, values)).IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public async Task Typed_schema_rejects_unknown_values_and_invalid_dates()
	{
		var revision = Add(CommunicationChannelType.InApp, "Homework", "{{task_title}}");
		var values = Values();
		values["student_email"] = new(NotificationVariableType.Text, "private@example.invalid");
		(await RenderAsync(revision, values)).IsSuccess.ShouldBeFalse();
		values.Remove("student_email");
		values["due_date"] = new(NotificationVariableType.Text, "tomorrow");
		(await RenderAsync(revision, values)).IsSuccess.ShouldBeFalse();
		values["due_date"] = new(NotificationVariableType.DateTime, "not-a-date");
		(await RenderAsync(revision, values)).IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public async Task Payload_limits_and_subject_newlines_fail_without_truncation()
	{
		var revision = Add(CommunicationChannelType.WebPush, "Homework", "{{task_title}}");
		var values = Values();
		values["task_title"] = new(NotificationVariableType.Text, new string('x', 241));
		(await RenderAsync(revision, values)).IsSuccess.ShouldBeFalse();
		revision = Add(CommunicationChannelType.Email, "Hello {{recipient_name}}", "{{task_title}}");
		values = Values();
		values["recipient_name"] = new(NotificationVariableType.Text, "Name\r\nBcc: another@example.invalid");
		(await RenderAsync(revision, values)).IsSuccess.ShouldBeFalse();
	}

	[Theory]
	[InlineData("recipient")]
	[InlineData("channel")]
	[InlineData("language")]
	[InlineData("schema")]
	[InlineData("event")]
	public async Task Render_context_must_match_the_selected_published_variant(string mismatch)
	{
		var revision = Add(CommunicationChannelType.InApp, "Homework", "{{task_title}}");
		var input = Input(revision);
		input = mismatch switch {
			"recipient" => input with { RecipientKind = "AssignedTeacher" },
			"channel" => input with { Channel = CommunicationChannelType.Email },
			"language" => input with { LanguageCode = "hi" },
			"schema" => input with { SchemaVersion = 2 },
			_ => input with { EventKey = "Academic.Assignment.Published" }
		};
		(await _renderer.RenderAsync(input, default)).IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public async Task Foreign_tenant_unpublished_and_missing_revisions_return_no_message_content()
	{
		var revision = Add(CommunicationChannelType.Email, "Homework", "{{task_title}}", publish: false);
		(await RenderAsync(revision)).IsSuccess.ShouldBeFalse();
		revision.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow);
		_tenant.Id.Returns(Guid.NewGuid());
		var result = await RenderAsync(revision);
		result.IsSuccess.ShouldBeFalse();
		result.Body.ShouldBeNull();
		_tenant.Id.Returns(_tenantId);
		(await _renderer.RenderAsync(Input(revision) with { RevisionId = Guid.NewGuid() }, default)).IsSuccess.ShouldBeFalse();
	}

	[Fact]
	public async Task Retirement_preserves_a_pinned_revision_and_rendering_does_not_mutate_it()
	{
		var revision = Add(CommunicationChannelType.Email, "Homework", "Hello {{recipient_name}}");
		var original = revision.ContentHash;
		revision.Retire();
		var before = await RenderAsync(revision);
		before.IsSuccess.ShouldBeTrue();
		var values = Values();
		values["recipient_name"] = new(NotificationVariableType.Text, "Different example");
		var after = await RenderAsync(revision, values);
		after.ContentHash.ShouldNotBe(before.ContentHash);
		revision.ContentHash.ShouldBe(original);
		revision.Body.ShouldBe("Hello {{recipient_name}}");
	}

	private Task<NotificationRenderResult> RenderAsync(CommunicationTemplateRevision revision,
		Dictionary<string, NotificationVariableValueDto>? values = null) =>
		_renderer.RenderAsync(Input(revision) with { Variables = values ?? Values() }, default);

	private static NotificationRenderInput Input(CommunicationTemplateRevision revision) => new(revision.Id,
		EventKey, 1, "Guardian", revision.Channel, "en", Values());

	private CommunicationTemplateRevision Add(CommunicationChannelType channel, string subject, string body, bool publish = true)
	{
		var revision = new CommunicationTemplateRevision(Guid.NewGuid(), _tenantId, Guid.NewGuid(), 1,
			EventKey, "Guardian", channel, "en", subject, body, 1);
		if (publish) revision.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow);
		_rows.Add(revision);
		return revision;
	}

	private static Dictionary<string, NotificationVariableValueDto> Values() => new() {
		["school_name"] = new(NotificationVariableType.Text, "Example school"),
		["recipient_name"] = new(NotificationVariableType.Text, "Example guardian"),
		["source_link"] = new(NotificationVariableType.Link, "https://school.example/academic/homework/one"),
		["task_title"] = new(NotificationVariableType.Text, "Example homework")
	};
}
