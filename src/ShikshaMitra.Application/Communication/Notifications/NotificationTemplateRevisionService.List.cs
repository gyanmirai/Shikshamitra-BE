using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Communication.Notifications;

public partial class NotificationTemplateRevisionService
{
	public virtual async Task<PagedResultDto<NotificationTemplateDto>> GetListAsync(GetNotificationTemplateListInput input,
		CancellationToken cancellationToken = default)
	{
		var tenantId = RequireTenant();
		validator.GetDefinition(input.EventKey);
		await AuthorizeAsync(input.EventKey, input.AcademicMediumId, NotificationTemplateAction.Read, cancellationToken);
		if (input.SkipCount < 0 || input.MaxResultCount is < 1 or > 100 || input.Search?.Length > 160)
			throw new BusinessException(NotificationErrorCodes.TemplateInvalid);
		var query = from header in await headers.GetQueryableAsync()
			join revision in await revisions.GetQueryableAsync() on header.CurrentNotificationRevisionId equals (System.Guid?)revision.Id
			where header.TenantId == tenantId && revision.TenantId == tenantId && header.Id == revision.TemplateId &&
				!header.IsDeleted && !revision.IsDeleted && revision.EventKey == input.EventKey &&
				(header.AcademicMediumId == null || header.AcademicMediumId == input.AcademicMediumId)
			select new { Header = header, Revision = revision };
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim();
			query = query.Where(x => x.Header.TemplateName.Contains(search));
		}
		var count = await executer.CountAsync(query, cancellationToken);
		var page = await executer.ToListAsync(query.OrderBy(x => x.Header.TemplateName).ThenBy(x => x.Header.Id)
			.Skip(input.SkipCount).Take(input.MaxResultCount), cancellationToken);
		return new(count, page.Select(x => Map(x.Header, x.Revision)).ToArray());
	}
}
