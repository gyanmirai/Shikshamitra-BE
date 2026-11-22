using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;

namespace ShikshaMitra.Communication.Notifications;

public partial class NotificationTemplateRevisionService
{
	public virtual async Task<NotificationTemplateDto> AppendLegacyDraftAsync(Guid templateId, CreateUpdateCommunicationTemplateDto input,
		CancellationToken cancellationToken = default)
	{
		var header = await LoadHeaderAsync(templateId, cancellationToken);
		var previous = await LastRevisionAsync(templateId, cancellationToken) ?? throw new BusinessException(NotificationErrorCodes.TemplateInvalid);
		await AuthorizeAsync(previous.EventKey, header.AcademicMediumId, NotificationTemplateAction.Write, cancellationToken);
		if (input.AcademicMediumId != header.AcademicMediumId ||
			!string.Equals(input.LanguageCode, previous.LanguageCode, StringComparison.OrdinalIgnoreCase) ||
			input.Channels.Count != 0 && (input.Channels.Count != 1 || input.Channels[0] != previous.Channel) ||
			!string.IsNullOrWhiteSpace(input.TargetRole) && input.TargetRole != previous.RecipientKind)
			throw new BusinessException(NotificationErrorCodes.UseWorkflow);
		return await CreateAsync(new(previous.EventKey, previous.RecipientKind, previous.Channel, previous.LanguageCode,
			input.TemplateName, input.Subject, input.Body, header.AcademicMediumId, header.Id, header.ConcurrencyStamp), cancellationToken);
	}
}
