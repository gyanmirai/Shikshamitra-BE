using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Communication.Notifications;

[RemoteService(false)]
public sealed class NotificationTemplateLegacyCompatibilityService(ICurrentTenant tenant,
	IRepository<CommunicationTemplateRevision, Guid> revisions, IAsyncQueryableExecuter executer,
	IDataFilter filters, NotificationTemplateRevisionService owner) : ITransientDependency
{
	public async Task<bool> TryAppendDraftAsync(CommunicationTemplate header, CreateUpdateCommunicationTemplateDto input)
	{
		if (!await IsBoundAsync(header.Id)) return false;
		await owner.AppendLegacyDraftAsync(header.Id, input);
		return true;
	}
	public async Task EnsureCanDeleteHeaderAsync(Guid templateId)
	{
		if (await IsBoundAsync(templateId)) throw new BusinessException(NotificationErrorCodes.TemplateImmutable);
	}
	public async Task EnsureGenericWorkflowAsync(string campaignType, Guid? templateId)
	{
		if (string.Equals(campaignType?.Trim(), "AcademicNotification", StringComparison.OrdinalIgnoreCase) ||
			templateId.HasValue && await IsBoundAsync(templateId.Value)) throw new BusinessException(NotificationErrorCodes.UseWorkflow);
	}
	private async Task<bool> IsBoundAsync(Guid templateId)
	{
		if (tenant.Id is not Guid tenantId) return false;
		using var includeDeletedDrafts = filters.Disable<ISoftDelete>();
		return await executer.AnyAsync((await revisions.GetQueryableAsync()).Where(x => x.TenantId == tenantId && x.TemplateId == templateId));
	}
}
