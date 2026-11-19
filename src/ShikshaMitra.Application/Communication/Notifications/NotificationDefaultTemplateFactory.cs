using System;
using System.Text.RegularExpressions;
using Volo.Abp;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Communication.Notifications;

public sealed record NotificationDefaultTemplateContent(string Subject, string Body);

public sealed partial class NotificationDefaultTemplateFactory : ITransientDependency
{
	public NotificationDefaultTemplateContent Create(NotificationEventDefinition definition, string recipientKind,
		CommunicationChannelType channel, string language)
	{
		if (!string.Equals(language, "en", StringComparison.OrdinalIgnoreCase)) throw new BusinessException(NotificationErrorCodes.TemplateInvalid);
		var action = definition.EventKey.Split('.')[^1];
		var title = Words().Replace(action, "$1 $2");
		var subject = definition.Category + ": " + title;
		var message = (recipientKind, definition.EventKey) switch
		{
			("AssignedTeacher", AcademicNotificationEventKeys.HomeworkOverdue or AcademicNotificationEventKeys.AssignmentOverdue) =>
				"{{task_title}} had {{missing_submission_count}} missing submissions when this summary was prepared.",
			("AssignedTeacher", AcademicNotificationEventKeys.HomeworkSubmitted or AcademicNotificationEventKeys.AssignmentSubmitted) =>
				"Submissions recorded for {{task_title}} during this digest window: {{submitted_count}}.",
			_ => subject + "."
		};
		var body = channel == CommunicationChannelType.WebPush ? "An academic update is available. Open the school website for details." :
			$"Hello {{{{recipient_name}}}},\n{message}\nView details: {{{{source_link}}}}\n{{{{school_name}}}}";
		if (channel == CommunicationChannelType.Email)
			body = $"<p>Hello {{{{recipient_name}}}},</p><p>{message}</p><p><a href=\"{{{{source_link}}}}\">View details</a></p><p>{{{{school_name}}}}</p>";
		return new(subject, body);
	}
	[GeneratedRegex("([a-z])([A-Z])", RegexOptions.CultureInvariant)]
	private static partial Regex Words();
}
