using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Setting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.HomeworkAssignment.Setting;

[RemoteService(IsEnabled = false)]
public class HomeworkTypeAppService(
	IRepository<HomeWorkType, Guid> _homeworkTypeRepository,
	ILogger<HomeworkTypeAppService> _logger
	) : ApplicationService, IHomeworkTypeAppService
{

	public async Task<HomeWorkTypeDto> CreateHomeWorkTypeAsync(CreateHomeworkTypeDto createHomewokTypeDto)
	{
		try
		{
            // Validate that the homework type name is not null, empty, or whitespace
            if (string.IsNullOrWhiteSpace(createHomewokTypeDto.Name))
            {
                throw new UserFriendlyException("Homework type name is required.");
            }

            // Validate that the EducationMediumId is provided and not empty
            if (createHomewokTypeDto.EducationMediumId == Guid.Empty)
            {
                throw new UserFriendlyException("Education Medium is required.");
            }

            // Check for duplicate homework type name within the same EducationMedium
            var exists = await _homeworkTypeRepository.AnyAsync(x =>
            x.Name.ToLower() == createHomewokTypeDto.Name.Trim().ToLower() &&
            x.EducationMediumId == createHomewokTypeDto.EducationMediumId
            );

            if (exists)
            {
                // Throw exception if a duplicate is found
                throw new UserFriendlyException(
                $"Homework type '{createHomewokTypeDto.Name}' already homeworktypeexist for this education medium. Please choose a different name."
                );
            }

            // Create a new HomeWorkType entity with the provided details
            var newentity = new HomeWorkType(
            GuidGenerator.Create(),
            CurrentTenant.Id,
            createHomewokTypeDto.Name.Trim(),
            createHomewokTypeDto.EducationMediumId
            );

            // Insert the new entity into the repository and save changes
            await _homeworkTypeRepository.InsertAsync(newentity, autoSave: true);

			_logger.LogInformation("HomeworkType created successfully: {Name} for EducationMediumId {EducationMediumId}",
				createHomewokTypeDto.Name, createHomewokTypeDto.EducationMediumId);

			// Map to DTO
			return ObjectMapper.Map<HomeWorkType, HomeWorkTypeDto>(newentity);
		}
		catch (UserFriendlyException)
		{
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error while creating HomeworkType {Name} for EducationMediumId {EducationMediumId}",
				createHomewokTypeDto.Name, createHomewokTypeDto.EducationMediumId);
			throw new UserFriendlyException("An unexpected error occurred while creating the homework type. Please try again.");
		}
	}

    public async Task<bool> DeleteAsync(Guid homeworkTypeId)
    {
        try
        {
            var homeworkTypeEntity = await _homeworkTypeRepository.FindAsync(homeworkTypeId);

            if (homeworkTypeEntity == null)
                throw new UserFriendlyException("Homework type not found.");

            await _homeworkTypeRepository.DeleteAsync(homeworkTypeEntity);

            _logger.LogInformation("HomeworkType deleted successfully: {Name}", homeworkTypeEntity.Name);

            return true;
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error while deleting HomeworkType {Id}", homeworkTypeId);
            throw new UserFriendlyException("Unable to delete homework type. Please try again later.");
        }
    }

    public async Task<List<HomeWorkTypeDto>> GetListAsync(Guid? academicMediumId)
    {
        try
        {

            // Declare a list to hold the homework types retrieved from the repository
            List<HomeWorkType> homeworkTypes;

            // Check if an academic medium ID is provided and is not empty
            if (academicMediumId is not null && academicMediumId != Guid.Empty)
            {
                // Fetch homework types filtered by the specified EducationMediumId
                homeworkTypes = await _homeworkTypeRepository.GetListAsync(
                    x => x.EducationMediumId == academicMediumId);
            }
            else
            {
                // Fetch all homework types if no specific EducationMediumId is provided
                homeworkTypes = await _homeworkTypeRepository.GetListAsync();
            }

            // Map the list of HomeWorkType entities to a list of HomeWorkTypeDto objects and return
            return ObjectMapper.Map<List<HomeWorkType>, List<HomeWorkTypeDto>>(homeworkTypes);
		}
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while fetching homework types.");
            throw new UserFriendlyException("Unable to fetch homework types. Please try again later.");
        }
    }

	public async Task<HomeWorkTypeDto> UpdateAsync(Guid id, UpdateHomeworkTypeDto updateHomeworkTypeDto)
	{
		try
        {
            // Attempt to find the homework type entity by its unique identifier
            var homeworktype = await _homeworkTypeRepository.FindAsync(id);

            // If the entity does not exist, throw a user-friendly exception
            if (homeworktype == null)
                throw new UserFriendlyException("Homework type not found.");

            // Check if another homework type with the same name exists in the same academic medium (excluding the current entity)
            var homeworktypeexist = await _homeworkTypeRepository.AnyAsync(x =>
            x.Id != id &&
            x.EducationMediumId == homeworktype.EducationMediumId &&
            x.Name.ToLower() == updateHomeworkTypeDto.Name.Trim().ToLower()
            );

            // If a duplicate is found, throw a user-friendly exception
            if (homeworktypeexist)
                throw new UserFriendlyException($"Homework type '{updateHomeworkTypeDto.Name}' already homeworktypeexist in this academic medium.");

            // Update the entity's name with the new value after trimming whitespace
            homeworktype.SetName(updateHomeworkTypeDto.Name.Trim());

            // Persist the updated entity to the repository with auto-save enabled
            await _homeworkTypeRepository.UpdateAsync(homeworktype, autoSave: true);

            // Log the successful update operation
            _logger.LogInformation("HomeworkType updated successfully: {Name}", homeworktype.Name);

            // Map the updated entity to its corresponding DTO and return it
            return ObjectMapper.Map<HomeWorkType, HomeWorkTypeDto>(homeworktype);
		}
		catch (UserFriendlyException)
		{
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error while updating HomeworkType {Id}", id);
			throw new UserFriendlyException("Unable to update homework type. Please try again later.");
		}
	}
}


