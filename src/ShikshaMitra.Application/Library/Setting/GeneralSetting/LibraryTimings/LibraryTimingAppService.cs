using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Library;
using ShikshaMitra.Library.Shared;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;

[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_LibraryHours.View)]
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

	[Authorize(ShikshaMitraSubmodulePermissions.Library.Settings_LibraryHours.Add)]
	public async Task<GenericUpdatedResponse> UpdateHoursAsync(Guid academicYearId, LibraryHoursUpdateRequest input)
	{
		ValidateHours(input);

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

	private static void ValidateHours(LibraryHoursUpdateRequest input)
	{
		var duplicateDay = input.Hours
			.GroupBy(x => x.Day.Trim().ToLower())
			.FirstOrDefault(x => x.Count() > 1);

		if (duplicateDay != null)
		{
			throw LibraryException(LibraryErrorCodes.LibraryHoursInvalid, $"Duplicate library-hours row for '{duplicateDay.Key}'.");
		}

		foreach (var hour in input.Hours)
		{
			if (string.IsNullOrWhiteSpace(hour.Day))
			{
				throw LibraryException(LibraryErrorCodes.LibraryHoursInvalid, "Library-hours day is required.");
			}

			if (hour.Status is < 0 or > 2)
			{
				throw LibraryException(LibraryErrorCodes.LibraryHoursInvalid, "Library-hours status must be Open, Closed, or Holiday.");
			}

			if (hour.Status == 0 &&
				(!TimeSpan.TryParse(hour.StartTime, out var startTime) ||
				!TimeSpan.TryParse(hour.EndTime, out var endTime) ||
				startTime >= endTime))
			{
				throw LibraryException(LibraryErrorCodes.LibraryHoursInvalid, $"Start time must be earlier than end time for {hour.Day}.");
			}
		}
	}

	private static BusinessException LibraryException(string code, string message)
	{
		return new BusinessException(code: code, message: message);
	}
}
