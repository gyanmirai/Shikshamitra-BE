using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false)]
public sealed class NotificationTemplatePreviewService(INotificationTemplateReadService reader, ICurrentTenant tenant,
	IRepository<CommunicationTemplateRevision, Guid> revisions, IAsyncQueryableExecuter executer,
	NotificationTemplateVariableValidator validator, NotificationTemplateRenderer renderer, IConfiguration configuration) : ITransientDependency
{
	public async Task<NotificationTemplatePreviewDto> PreviewAsync(Guid templateId, Guid revisionId, CancellationToken cancellationToken = default)
	{
		await reader.GetAsync(templateId, cancellationToken);
		if (tenant.Id is not Guid tenantId) throw new AbpAuthorizationException();
		var revision = await executer.FirstOrDefaultAsync((await revisions.GetQueryableAsync()).Where(x => x.Id == revisionId &&
			x.TemplateId == templateId && x.TenantId == tenantId && !x.IsDeleted), cancellationToken)
			?? throw new EntityNotFoundException(typeof(CommunicationTemplateRevision), revisionId);
		var definition = validator.GetDefinition(revision.EventKey);
		var link = (configuration["App:ClientUrl"] ?? string.Empty).TrimEnd('/') + "/";
		var variables = definition.Variables.ToDictionary(x => x.Key, field => new NotificationVariableValueDto(field.Type, field.Type switch {
			NotificationVariableType.Date => "2000-01-15",
			NotificationVariableType.DateTime => "2000-01-15T10:00:00+00:00",
			NotificationVariableType.Number => "1",
			NotificationVariableType.Link => link,
			_ => "Example " + field.Key.Replace("_name", string.Empty, StringComparison.Ordinal).Replace('_', ' ')
		}), StringComparer.Ordinal);
		var input = new NotificationRenderInput(revision.Id, revision.EventKey, revision.SchemaVersion, revision.RecipientKind,
			revision.Channel, revision.LanguageCode, variables);
		return new(true, renderer.RenderPreview(revision, input));
	}
}
