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
using DocumentFormat.OpenXml.Vml.Office;

namespace ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;
public class LibraryTimingAppService(
	IRepository<LibraryTiming, Guid> _libraryTimingRepository,
	IGuidGenerator guidGenerator
) : ApplicationService, ILibraryTimingAppService
{
	public async Task<LibraryTimingDto> CreateAsync(CreateUpdateLibraryTimingDto createLibraryTimingDto)
	{
		try
		{
			var libraryTimingEntity = new LibraryTiming(
				guidGenerator.Create(),
				createLibraryTimingDto.Day,
				createLibraryTimingDto.OpeningTime,
				createLibraryTimingDto.ClosingTime,
				CurrentTenant.Id ?? Guid.Empty
			);

			await _libraryTimingRepository.InsertAsync(libraryTimingEntity, autoSave: true);
			return ObjectMapper.Map<LibraryTiming, LibraryTimingDto>(libraryTimingEntity);
		}
		catch (Exception ex)
		{
			throw new ApplicationException("An error occurred while creating the library timing.", ex);
		}
	}

	public async Task<LibraryTimingDto> UpdateAsync(Guid id, CreateUpdateLibraryTimingDto updateLibraryTimingDto)
	{
		try
		{
			var libraryTimingEntity = await _libraryTimingRepository.FindAsync(id);
			if (libraryTimingEntity == null)
			{
				throw new EntityNotFoundException(typeof(LibraryTiming), id);
			}

			libraryTimingEntity.OpeningTime = updateLibraryTimingDto.OpeningTime;
			libraryTimingEntity.ClosingTime = updateLibraryTimingDto.ClosingTime;

			await _libraryTimingRepository.UpdateAsync(libraryTimingEntity, autoSave: true);
			return ObjectMapper.Map<LibraryTiming, LibraryTimingDto>(libraryTimingEntity);
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
			var libraryTimingEntity = await _libraryTimingRepository.FindAsync(x => x.Day == dayOfWeek);
			if (libraryTimingEntity == null)
			{
				throw new EntityNotFoundException($"No library timing found for day {dayOfWeek}.");
			}

			await _libraryTimingRepository.DeleteAsync(libraryTimingEntity);
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
			var libraryTimingEntity = await _libraryTimingRepository.GetAsync(id);
			return ObjectMapper.Map<LibraryTiming, LibraryTimingDto>(libraryTimingEntity);
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
