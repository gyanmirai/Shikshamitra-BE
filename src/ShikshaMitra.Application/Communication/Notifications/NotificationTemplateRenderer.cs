using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false)]
public sealed class NotificationTemplateRenderer(ICurrentTenant tenant,
	IRepository<CommunicationTemplateRevision, Guid> revisions, IAsyncQueryableExecuter executer,
	NotificationTemplateVariableValidator validator) : INotificationTemplateRenderer, ITransientDependency
{
	public async Task<NotificationRenderResult> RenderAsync(NotificationRenderInput input, CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		if (tenant.Id is not Guid tenantId) return Failure(NotificationErrorCodes.TemplateInvalid);
		var query = await revisions.GetQueryableAsync();
		var revision = await executer.FirstOrDefaultAsync(query.Where(x => x.TenantId == tenantId && x.Id == input.RevisionId &&
			x.IsPublished && !x.IsDeleted), cancellationToken);
		if (revision == null) return Failure(NotificationErrorCodes.TemplateInvalid);
		return RenderContent(revision, input);
	}

	internal NotificationRenderResult RenderPreview(CommunicationTemplateRevision revision, NotificationRenderInput input)
	{
		if (!tenant.Id.HasValue || revision.TenantId != tenant.Id || revision.Id != input.RevisionId || revision.IsDeleted)
			return Failure(NotificationErrorCodes.TemplateInvalid);
		return RenderContent(revision, input);
	}

	private NotificationRenderResult RenderContent(CommunicationTemplateRevision revision, NotificationRenderInput input)
	{
		var error = validator.ValidateRender(input, revision);
		if (error != null) return Failure(error);
		var subject = validator.Substitute(revision.Subject, input);
		var body = validator.Substitute(revision.Body, input, encodeHtml: input.Channel == CommunicationChannelType.Email);
		if (input.Channel == CommunicationChannelType.Email)
		{
			var clean = NotificationTemplateMarkup.SanitizeEmail(body, validator.IsAllowedLink);
			if (clean == null) return Failure(NotificationErrorCodes.TemplateInvalid);
			body = clean;
		}
		if (!NotificationTemplateVariableValidator.WithinLimits(input.Channel, subject, body))
			return Failure(NotificationErrorCodes.TemplateInvalid);
		var link = input.Variables["source_link"].Value;
		var hash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
			revision.Id, input.Channel, input.RecipientKind, input.LanguageCode, Subject = subject, Body = body, Link = link
		})));
		return new(true, null, subject, body, link, hash);
	}

	private static NotificationRenderResult Failure(string error) => new(false, error, null, null, null, null);
}
