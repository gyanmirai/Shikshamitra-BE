using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Academic.Notifications;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class WhatsAppTemplateCompiler_Tests
{
	private readonly WhatsAppTemplateCompiler _compiler = new(new([new AcademicNotificationCatalogue([])],
		new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
			["App:ClientUrl"] = "https://school.example"
		}).Build()));

	[Fact]
	public void Compiles_and_reuses_the_exact_header_and_body_positions_from_the_published_revision()
	{
		var revision = Revision("Reminder for {{student_name}}", "Hello {{recipient_name}}. {{student_name}} can open {{source_link}}.", "en-US");
		var template = _compiler.Compile(revision);

		template.Header.ShouldBe("Reminder for {{1}}");
		template.Body.ShouldBe("Hello {{1}}. {{2}} can open {{3}}.");
		template.ProviderLanguage.ShouldBe("en_US");
		template.Parameters.ShouldBe(new WhatsAppTemplateParameter[] {
			new("header", 1, "student_name"), new("body", 1, "recipient_name"), new("body", 2, "student_name"), new("body", 3, "source_link")
		});
		var components = _compiler.Bind(revision, _compiler.EncodeParameters(template), Variables());
		components.Select(x => x.Type).ShouldBe(new[] { "header", "body" });
		components[0].Parameters.ShouldBe(new[] { "Aarav" });
		components[1].Parameters.ShouldBe(new[] { "Primary Guardian", "Aarav", "https://school.example/portal/homework" });
	}

	[Fact]
	public void Rejects_stored_parameter_reordering_and_missing_or_unsafe_values()
	{
		var revision = Revision("", "Hello {{recipient_name}}. View {{source_link}}.", "en");
		var template = _compiler.Compile(revision);
		var wrong = template with { Parameters = template.Parameters.Reverse().ToArray() };
		Should.Throw<BusinessException>(() => _compiler.Bind(revision, _compiler.EncodeParameters(wrong), Variables()))
			.Code.ShouldBe(NotificationErrorCodes.TemplateInvalid);
		var noLink = Variables();
		noLink.Remove("source_link");
		Should.Throw<BusinessException>(() => _compiler.Bind(revision, _compiler.EncodeParameters(template), noLink))
			.Code.ShouldBe(NotificationErrorCodes.RequiredVariableMissing);
		var badLink = Variables();
		badLink["source_link"] = new(NotificationVariableType.Link, "https://other-school.example/portal/homework");
		Should.Throw<BusinessException>(() => _compiler.Bind(revision, _compiler.EncodeParameters(template), badLink))
			.Code.ShouldBe(NotificationErrorCodes.TemplateInvalid);
	}

	[Fact]
	public void A_draft_or_a_non_whatsapp_revision_cannot_be_compiled_for_submission()
	{
		Should.Throw<BusinessException>(() => _compiler.Compile(Revision("Homework", "Review the task.", "en", publish: false)))
			.Code.ShouldBe(NotificationErrorCodes.TemplateInvalid);
		Should.Throw<BusinessException>(() => _compiler.Compile(Revision("Homework", "Review the task.", "en", channel: CommunicationChannelType.Email)))
			.Code.ShouldBe(NotificationErrorCodes.TemplateInvalid);
	}

	private static CommunicationTemplateRevision Revision(string header, string body, string language, bool publish = true,
		CommunicationChannelType channel = CommunicationChannelType.WhatsApp)
	{
		var value = new CommunicationTemplateRevision(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
			"Academic.Homework.Published", "Guardian", channel, language, header, body, 1);
		if (publish) value.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow);
		return value;
	}

	private static Dictionary<string, NotificationVariableValueDto> Variables() => new() {
		["recipient_name"] = new(NotificationVariableType.Text, "Primary Guardian"),
		["student_name"] = new(NotificationVariableType.Text, "Aarav"),
		["source_link"] = new(NotificationVariableType.Link, "https://school.example/portal/homework"),
		["school_name"] = new(NotificationVariableType.Text, "School")
	};
}
