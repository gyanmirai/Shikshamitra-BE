using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;

[RemoteService(false)]
public class LibraryTimingAppService(IRepository<LibraryTiming, Guid> _repository)
	: ApplicationService, ILibraryTimingAppService
{
	public async Task<PaginatedResponse<LibraryTimingDto>> GetListAsync(Guid academicYearId, int page = 1, int pageSize = 10)
	{
		var query = await _repository.GetQueryableAsync();
		query = query.Where(x => x.AcademicYearId == academicYearId);

		var totalCount = await AsyncExecuter.CountAsync(query);
		var totalPages = (int) Math.Ceiling(totalCount / (double) pageSize);

		var items = await AsyncExecuter.ToListAsync(
			query
				.OrderBy(x => x.Day)
				.Skip((page - 1) * pageSize)
				.Take(pageSize)
		);

		return new PaginatedResponse<LibraryTimingDto>
		{
			TotalCount = totalCount,
			CurrentPage = page,
			PageSize = pageSize,
			TotalPages = totalPages,
			Items = ObjectMapper.Map<List<LibraryTiming>, List<LibraryTimingDto>>(items)
		};
	}

	public async Task<GenericUpdatedResponse> UpdateHoursAsync(Guid academicYearId, LibraryHoursUpdateRequest input)
	{
		var query = await _repository.GetQueryableAsync();
		var existing = await AsyncExecuter.ToListAsync(
			query.Where(x => x.AcademicYearId == academicYearId)
		);

		foreach (var hourItem in input.Hours)
		{
			var record = existing.FirstOrDefault(x => x.Day == hourItem.Day);
			if (record != null)
			{
				record.StartTime = hourItem.StartTime;
				record.EndTime = hourItem.EndTime;
				record.Status = hourItem.Status;
				await _repository.UpdateAsync(record);
			}
			else
			{
				var newRecord = new LibraryTiming(
					GuidGenerator.Create(),
					hourItem.Day,
					hourItem.StartTime,
					hourItem.EndTime,
					hourItem.Status,
					academicYearId,
					CurrentTenant.Id
				);
				await _repository.InsertAsync(newRecord);
			}
		}

		return new GenericUpdatedResponse
		{
			Id = academicYearId.ToString(),
			Message = "Library hours updated successfully."
		};
	}
}
