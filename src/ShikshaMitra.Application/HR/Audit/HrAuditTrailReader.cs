using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;

namespace ShikshaMitra.HR.Audit;

[RemoteService(false)]
public class HrAuditTrailReader(IRepository<HrAuditEvent, Guid> repository)
	: ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<HrAuditEventDto>> GetListAsync(
		HrAuditEventListRequestDto input,
		string[]? allowedEntityTypes)
	{
		var query = await repository.GetQueryableAsync();
		if (allowedEntityTypes is not null)
		{
			query = query.Where(x => allowedEntityTypes.Contains(x.EntityType));
		}

		query = HrAuditTrailQuery.ApplyFilters(query, input);
		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(HrAuditTrailQuery.ApplySorting(query, input.Sorting)
			.Skip(input.SkipCount)
			.Take(HrAuditTrailQuery.NormalizeMaxResultCount(input.MaxResultCount)));

		return new PagedResultDto<HrAuditEventDto>(total, rows.Select(HrAuditTrailProjection.ToDto).ToList());
	}
}
