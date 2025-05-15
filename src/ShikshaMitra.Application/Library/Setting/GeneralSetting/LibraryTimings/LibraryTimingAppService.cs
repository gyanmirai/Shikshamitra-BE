using ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

public class LibraryTimingAppService(IRepository<LibraryTiming, Guid> _repository)
	: ApplicationService, ILibraryTimingAppService
{
	public async Task<List<LibraryTimingDto>> GetListAsync()
	{
		var items = await _repository.GetListAsync();
		return ObjectMapper.Map<List<LibraryTiming>, List<LibraryTimingDto>>(items);
	}

	public async Task<LibraryTimingDto> CreateAsync(CreateUpdateLibraryTimingDto input)
	{
		var newTiming = new LibraryTiming(
			GuidGenerator.Create(),
			input.Day,
			input.OpeningTime,
			input.ClosingTime
		);

		var inserted = await _repository.InsertAsync(newTiming);
		return ObjectMapper.Map<LibraryTiming, LibraryTimingDto>(inserted);
	}

	public async Task<LibraryTimingDto> UpdateAsync(Guid id, CreateUpdateLibraryTimingDto input)
	{
		var existing = await _repository.GetAsync(id);
		existing.OpeningTime = input.OpeningTime;
		existing.ClosingTime = input.ClosingTime;

		var updated = await _repository.UpdateAsync(existing);
		return ObjectMapper.Map<LibraryTiming, LibraryTimingDto>(updated);
	}

	public async Task DeleteAsync(int day)
	{
		if (!Enum.IsDefined(typeof(DayOfWeek), day))
		{
			throw new ArgumentException("Invalid day value. It must be between 0 (Sunday) and 6 (Saturday).");
		}

		var dayOfWeek = (DayOfWeek) day;
		var existing = await _repository.FindAsync(x => x.Day == dayOfWeek);
		if (existing != null)
		{
			await _repository.DeleteAsync(existing);
		}
	}
}
