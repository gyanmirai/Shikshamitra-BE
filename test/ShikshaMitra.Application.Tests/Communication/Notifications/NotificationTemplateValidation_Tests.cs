using System;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Communication.Notifications;

public class NotificationTemplateValidation_Tests
{
	[Fact]
	public async Task Invalid_authoring_commands_have_field_validation_errors()
	{
		var validator = new CreateNotificationTemplateValidator();
		var invalid = new CreateNotificationTemplateDto("", "", CommunicationChannelType.Sms, "", "", "", "");
		var result = await validator.ValidateAsync(invalid);
		result.IsValid.ShouldBeFalse();
		foreach (var field in new[] { "EventKey", "RecipientKind", "Channel", "LanguageCode", "TemplateName", "Body" })
			result.Errors.ShouldContain(x => x.PropertyName == field);
		var valid = invalid with { EventKey = "Academic.Homework.Published", RecipientKind = "Guardian", Channel = CommunicationChannelType.Email,
			LanguageCode = "en", TemplateName = "Homework", Subject = "Homework", Body = "{{source_link}}" };
		(await validator.ValidateAsync(valid)).IsValid.ShouldBeTrue();
		(await validator.ValidateAsync(valid with { ExistingTemplateId = Guid.NewGuid() })).IsValid.ShouldBeFalse();
		(await validator.ValidateAsync(valid with { ExistingTemplateId = Guid.NewGuid(), HeaderConcurrencyStamp = "stamp" })).IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task Draft_and_revision_commands_require_real_ids_and_concurrency_tokens()
	{
		(await new UpdateNotificationTemplateDraftValidator().ValidateAsync(new UpdateNotificationTemplateDraftDto(Guid.Empty, "Subject", "Body", ""))).IsValid.ShouldBeFalse();
		(await new NotificationTemplateRevisionCommandValidator().ValidateAsync(new NotificationTemplateRevisionCommandDto(Guid.Empty, ""))).IsValid.ShouldBeFalse();
		(await new NotificationTemplateRevisionCommandValidator().ValidateAsync(new NotificationTemplateRevisionCommandDto(Guid.NewGuid(), "stamp"))).IsValid.ShouldBeTrue();
		(await new GetNotificationTemplateListValidator().ValidateAsync(new GetNotificationTemplateListInput { EventKey = "Academic.Homework.Published", MaxResultCount = 101 })).IsValid.ShouldBeFalse();
	}
}
