using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.TenantSettings.Reports;

public partial class InstituteReportAppService
{
	public async Task<PagedResultDto<InstituteReportHistoryRowDto>> GetHistoryAsync(InstituteReportHistoryInput input)
	{
		var types = await access.ReadableTypesAsync(User);
		var query = (await jobs.GetQueryableAsync()).Where(x => x.TenantId == Tenant && x.RequestedBy == User && Enumerable.Contains(types, x.ReportTypeId));
		var dateMatches = await InstituteReportHistoryDates.MatchingIdsAsync(query, input, AsyncExecuter);
		var filtered = InstituteReportHistoryQuery.Apply(InstituteReportHistoryQuery.Project(query, DateTime.UtcNow), input, dateMatches);
		var count = await AsyncExecuter.LongCountAsync(filtered);
		var sorted = InstituteReportHistoryQuery.Sort(filtered, input.Sorting);
		var rows = await AsyncExecuter.ToListAsync(sorted.Select(x => x.Job)
			.Skip(Math.Max(0, input.SkipCount)).Take(Math.Clamp(input.MaxResultCount, 1, 100)));
		return new(count, rows.Select(Summary).ToList());
	}
	public async Task<List<InstituteReportHistoryRowDto>> GetStatusesAsync(List<Guid> ids)
	{
		if (ids == null || ids.Count > 50) throw new BusinessException("InstituteReports:InvalidRequest");
		var types = await access.ReadableTypesAsync(User);
		var selected = ids.Distinct().ToArray();
		var query = (await jobs.GetQueryableAsync()).Where(x => x.TenantId == Tenant && x.RequestedBy == User
			&& Enumerable.Contains(selected, x.Id) && Enumerable.Contains(types, x.ReportTypeId));
		return (await AsyncExecuter.ToListAsync(query)).Select(Summary).ToList();
	}
	private static InstituteReportHistoryRowDto Summary(InstituteReportJob job)
	{
		var full = Map(job);
		return new() { Id = full.Id, ReportTypeId = full.Request.ReportType, ReportType = full.ReportType, Format = full.Format,
			Status = full.Status, FileName = full.FileName, ErrorMessage = full.ErrorMessage, CreationTime = full.CreationTime,
			CompletedAt = full.CompletedAt, ExpiresAt = full.ExpiresAt, IsDynamic = full.IsDynamic };
	}
}
