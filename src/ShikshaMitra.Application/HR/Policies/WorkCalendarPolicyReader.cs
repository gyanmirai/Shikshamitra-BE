using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp;

namespace ShikshaMitra.HR.Policies;

[RemoteService(false)]
public class WorkCalendarPolicyReader(
	IRepository<WorkCalendarPolicy, Guid> repository,
	WorkCalendarHolidaySnapshotService holidaySnapshotService)
	: ApplicationService, ITransientDependency
{
	public async Task<PagedResultDto<WorkCalendarPolicyDto>> GetListAsync(PolicyListRequestDto input)
	{
		var query = await repository.GetQueryableAsync();
		query = query.Where(x => x.AcademicYearId == input.AcademicYearId);
		if (!string.IsNullOrWhiteSpace(input.Search))
		{
			var search = input.Search.Trim().ToLower();
			query = query.Where(x => x.Name.ToLower().Contains(search));
		}

		var total = await AsyncExecuter.CountAsync(query);
		var rows = await AsyncExecuter.ToListAsync(query
			.OrderBy(x => x.Name)
			.Skip(input.SkipCount)
			.Take(input.MaxResultCount <= 0 ? 50 : input.MaxResultCount));

		var dtos = rows.Select(HrPolicyMapping.ToDto).ToList();
		var holidaySummaries = await holidaySnapshotService.GetHolidaySummariesAsync(rows.Select(x => x.Id).ToList());
		foreach (var dto in dtos)
		{
			dto.HolidaySummary = holidaySummaries.GetValueOrDefault(dto.Id) ?? new WorkCalendarHolidaySummaryDto();
		}

		return new PagedResultDto<WorkCalendarPolicyDto>(total, dtos);
	}

	public async Task<WorkCalendarPolicyDto> GetAsync(Guid id)
	{
		var dto = HrPolicyMapping.ToDto(await repository.GetAsync(id));
		dto.Holidays = await GetHolidaysAsync(id);
		dto.HolidaySummary = HrPolicyMapping.BuildHolidaySummary(dto.Holidays);
		return dto;
	}

	public async Task<List<WorkCalendarHolidayDto>> GetHolidaysAsync(Guid id)
	{
		return await holidaySnapshotService.GetHolidaysAsync(id);
	}

	public async Task<List<WorkCalendarHolidayDto>> GetHolidayImportPreviewAsync(Guid academicYearId)
	{
		return await holidaySnapshotService.GetImportPreviewAsync(academicYearId);
	}
}
