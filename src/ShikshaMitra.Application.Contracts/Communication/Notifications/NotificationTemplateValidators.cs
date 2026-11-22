using System;
using FluentValidation;

namespace ShikshaMitra.Communication.Notifications;

public class CreateNotificationTemplateValidator : AbstractValidator<CreateNotificationTemplateDto>
{
	public CreateNotificationTemplateValidator()
	{
		RuleFor(x => x.EventKey).NotEmpty().MaximumLength(160);
		RuleFor(x => x.RecipientKind).NotEmpty().MaximumLength(64);
		RuleFor(x => x.Channel).IsInEnum().NotEqual(CommunicationChannelType.Sms);
		RuleFor(x => x.LanguageCode).NotEmpty().MaximumLength(16).Matches(@"^[a-zA-Z]{2,3}(?:-[a-zA-Z0-9]{2,8})*$");
		RuleFor(x => x.TemplateName).NotEmpty().MaximumLength(160);
		RuleFor(x => x.Subject).MaximumLength(256);
		RuleFor(x => x.Subject).NotEmpty().When(x => x.Channel != CommunicationChannelType.WhatsApp);
		RuleFor(x => x.Body).NotEmpty().MaximumLength(65536);
		RuleFor(x => x.ExistingTemplateId).NotEqual(Guid.Empty).When(x => x.ExistingTemplateId.HasValue);
		RuleFor(x => x.AcademicMediumId).NotEqual(Guid.Empty).When(x => x.AcademicMediumId.HasValue);
		RuleFor(x => x.HeaderConcurrencyStamp).NotEmpty().MaximumLength(40).When(x => x.ExistingTemplateId.HasValue);
	}
}
public class UpdateNotificationTemplateDraftValidator : AbstractValidator<UpdateNotificationTemplateDraftDto>
{
	public UpdateNotificationTemplateDraftValidator()
	{
		RuleFor(x => x.RevisionId).NotEmpty();
		RuleFor(x => x.Subject).MaximumLength(256);
		RuleFor(x => x.Body).NotEmpty().MaximumLength(65536);
		RuleFor(x => x.ConcurrencyStamp).NotEmpty().MaximumLength(40);
	}
}
public class PreviewNotificationTemplateDraftValidator : AbstractValidator<PreviewNotificationTemplateDraftDto>
{
	public PreviewNotificationTemplateDraftValidator()
	{
		RuleFor(x => x.EventKey).NotEmpty().MaximumLength(160);
		RuleFor(x => x.RecipientKind).NotEmpty().MaximumLength(64);
		RuleFor(x => x.Channel).IsInEnum().NotEqual(CommunicationChannelType.Sms);
		RuleFor(x => x.LanguageCode).NotEmpty().MaximumLength(16).Matches(@"^[a-zA-Z]{2,3}(?:-[a-zA-Z0-9]{2,8})*$");
		RuleFor(x => x.Subject).NotNull().MaximumLength(256);
		RuleFor(x => x.Subject).NotEmpty().When(x => x.Channel != CommunicationChannelType.WhatsApp);
		RuleFor(x => x.Body).NotEmpty().MaximumLength(65536);
		RuleFor(x => x.AcademicMediumId).NotEqual(Guid.Empty).When(x => x.AcademicMediumId.HasValue);
	}
}
public class NotificationTemplateRevisionCommandValidator : AbstractValidator<NotificationTemplateRevisionCommandDto>
{
	public NotificationTemplateRevisionCommandValidator()
	{
		RuleFor(x => x.RevisionId).NotEmpty(); RuleFor(x => x.ConcurrencyStamp).NotEmpty().MaximumLength(40);
	}
}
public class RestoreNotificationTemplateValidator : AbstractValidator<RestoreNotificationTemplateDto>
{
	public RestoreNotificationTemplateValidator() => RuleFor(x => x.HeaderConcurrencyStamp).NotEmpty().MaximumLength(40);
}
public class GetNotificationTemplateListValidator : AbstractValidator<GetNotificationTemplateListInput>
{
	public GetNotificationTemplateListValidator()
	{
		RuleFor(x => x.EventKey).NotEmpty().MaximumLength(160);
		RuleFor(x => x.SkipCount).GreaterThanOrEqualTo(0);
		RuleFor(x => x.MaxResultCount).InclusiveBetween(1, 100);
		RuleFor(x => x.Search).MaximumLength(160);
		RuleFor(x => x.AcademicMediumId).NotEqual(Guid.Empty).When(x => x.AcademicMediumId.HasValue);
	}
}
