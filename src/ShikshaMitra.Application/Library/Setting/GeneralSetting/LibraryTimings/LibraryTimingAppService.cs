using ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;
using System.Linq;

namespace ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;
public class LibraryTimingAppService(
	IRepository<LibraryTiming, Guid> _libraryTimingRepository,
	IGuidGenerator guidGenerator
) : ApplicationService, ILibraryTimingAppService
{
	public async Task<LibraryTimingDto> CreateAsync(CreateUpdateLibraryTimingDto input)
	{
		try
		{
			var entity = new LibraryTiming(
				guidGenerator.Create(),
				input.Day,
				input.OpeningTime,
				input.ClosingTime,
				CurrentTenant.Id ?? Guid.Empty
			);

			await _libraryTimingRepository.InsertAsync(entity, autoSave: true);
			return ObjectMapper.Map<LibraryTiming, LibraryTimingDto>(entity);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while creating the library timing.", ex);
		}
	}

	public async Task<LibraryTimingDto> UpdateAsync(Guid id, CreateUpdateLibraryTimingDto input)
	{
		try
		{
			var entity = await _libraryTimingRepository.FindAsync(id);
			if (entity == null)
			{
				throw new EntityNotFoundException(typeof(LibraryTiming), id);
			}

			entity.OpeningTime = input.OpeningTime;
			entity.ClosingTime = input.ClosingTime;

			await _libraryTimingRepository.UpdateAsync(entity, autoSave: true);
			return ObjectMapper.Map<LibraryTiming, LibraryTimingDto>(entity);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while updating the library timing.", ex);
		}
	}

	public async Task DeleteAsync(int day)
	{
		try
		{
			if (!Enum.IsDefined(typeof(DayOfWeek), day))
			{
				throw new ArgumentException("Invalid day value. It must be between 0 (Sunday) and 6 (Saturday).");
			}

			var dayOfWeek = (DayOfWeek) day;
			var entity = await _libraryTimingRepository.FindAsync(x => x.Day == dayOfWeek);
			if (entity == null)
			{
				throw new EntityNotFoundException($"No library timing found for day {dayOfWeek}.");
			}

			await _libraryTimingRepository.DeleteAsync(entity);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while deleting the library timing.", ex);
		}
	}

	public async Task<LibraryTimingDto> GetAsync(Guid id)
	{
		try
		{
			var entity = await _libraryTimingRepository.GetAsync(id);
			return ObjectMapper.Map<LibraryTiming, LibraryTimingDto>(entity);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while retrieving the library timing.", ex);
		}
	}

    public async Task<List<LibraryTimingDto>> GetListAsync()
    {
        try
        {
            var tenantId = CurrentTenant.Id;
            var queryable = await _libraryTimingRepository.GetQueryableAsync();

            if (tenantId.HasValue)
            {
                queryable = queryable.Where(x => x.TenantId == tenantId.Value);
            }

            var items = await AsyncExecuter.ToListAsync(queryable);
            return ObjectMapper.Map<List<LibraryTiming>, List<LibraryTimingDto>>(items);
        }
        catch (Exception ex)
        {
            throw new ApplicationException("An error occurred while retrieving the library timings.", ex);
        }
    }
}
