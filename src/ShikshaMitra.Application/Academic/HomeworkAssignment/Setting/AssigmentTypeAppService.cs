using Microsoft.Extensions.Logging;
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
public class AssigmentTypeAppService(
	IRepository<AssigmentType, Guid> _assigmentTypeRepository,
	ILogger<AssigmentTypeAppService> _logger
	) : ApplicationService, IAssigmentTypeAppService
{
   
	public async Task<AssigmentTypeDto> CreateAssigmentTypeAsync(CreateAssigmentTypeDto createAssigmentTypeDto)
    {
        try
        {
            // Validate that the assignment type name is not null, empty, or whitespace
            if (string.IsNullOrWhiteSpace(createAssigmentTypeDto.Name))
            {
                throw new UserFriendlyException("Assignment type name is required.");
            }

            // Validate that the EducationMediumId is provided and not empty
            if (createAssigmentTypeDto.EducationMediumId == Guid.Empty)
            {
                throw new UserFriendlyException("Education Medium is required.");
            }

            // Check for duplicate assignment type name within the same EducationMedium
            var exists = await _assigmentTypeRepository.AnyAsync(x =>
                x.Name.ToLower() == createAssigmentTypeDto.Name.Trim().ToLower() &&
                x.EducationMediumId == createAssigmentTypeDto.EducationMediumId
            );

            if (exists)
            {
                // Throw exception if a duplicate is found
                throw new UserFriendlyException(
                    $"Assignment type '{createAssigmentTypeDto.Name}' already exists for this education medium. Please choose a different name."
                );
            }

            // Create a new AssigmentType entity with the provided details
            var newEntity = new AssigmentType(
                GuidGenerator.Create(),
                CurrentTenant.Id,
                createAssigmentTypeDto.Name.Trim(),
                createAssigmentTypeDto.EducationMediumId
            );

            // Insert the new entity into the repository and save changes
            await _assigmentTypeRepository.InsertAsync(newEntity, autoSave: true);

            _logger.LogInformation("AssigmentType created successfully: {Name} for EducationMediumId {EducationMediumId}",
                createAssigmentTypeDto.Name, createAssigmentTypeDto.EducationMediumId);

            // Map to DTO
            return ObjectMapper.Map<AssigmentType, AssigmentTypeDto>(newEntity);
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while creating AssigmentType {Name} for EducationMediumId {EducationMediumId}",
                createAssigmentTypeDto.Name, createAssigmentTypeDto.EducationMediumId);
            throw new UserFriendlyException("An unexpected error occurred while creating the assignment type. Please try again.");
        }
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        try
        {
            var assigmentTypeEntity = await _assigmentTypeRepository.FindAsync(id);

            if (assigmentTypeEntity == null)
                throw new UserFriendlyException("Assignment type not found.");

            await _assigmentTypeRepository.DeleteAsync(assigmentTypeEntity);

            _logger.LogInformation("AssigmentType deleted successfully: {Name}", assigmentTypeEntity.Name);

            return true;
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error while deleting AssigmentType {Id}", id);
            throw new UserFriendlyException("Unable to delete assignment type. Please try again later.");
        }
    }
    public async Task<List<AssigmentTypeDto>> GetListAsync(Guid? educationMediumId)
    {
        try
        {
            List<AssigmentType> assigmentTypes;

            if (educationMediumId is not null && educationMediumId != Guid.Empty)
            {
                assigmentTypes = await _assigmentTypeRepository.GetListAsync(
                    x => x.EducationMediumId == educationMediumId);
            }
            else
            {
                assigmentTypes = await _assigmentTypeRepository.GetListAsync();
            }

            return ObjectMapper.Map<List<AssigmentType>, List<AssigmentTypeDto>>(assigmentTypes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while fetching assignment types.");
            throw new UserFriendlyException("Unable to fetch assignment types. Please try again later.");
        }
	}
	
  public async Task<AssigmentTypeDto> UpdateAsync(Guid id, UpdateAssigmentTypeDto updateAssigmentTypeDto)
    {
        try
        {
            // Attempt to find the assignment type entity by its unique identifier
            var assigmentType = await _assigmentTypeRepository.FindAsync(id);

            // If the entity does not exist, throw a user-friendly exception
            if (assigmentType == null)
                throw new UserFriendlyException("Assignment type not found.");

            // Check if another assignment type with the same name exists in the same education medium (excluding the current entity)
            var assigmentTypeExist = await _assigmentTypeRepository.AnyAsync(x =>
                x.Id != id &&
                x.EducationMediumId == assigmentType.EducationMediumId &&
                x.Name.ToLower() == updateAssigmentTypeDto.Name.Trim().ToLower()
            );

            // If a duplicate is found, throw a user-friendly exception
            if (assigmentTypeExist)
                throw new UserFriendlyException($"Assignment type '{updateAssigmentTypeDto.Name}' already exists in this education medium.");

            // Update the entity's name with the new value after trimming whitespace
            assigmentType.SetName(updateAssigmentTypeDto.Name.Trim());

            // Persist the updated entity to the repository with auto-save enabled
            await _assigmentTypeRepository.UpdateAsync(assigmentType, autoSave: true);

            // Log the successful update operation
            _logger.LogInformation("AssigmentType updated successfully: {Name}", assigmentType.Name);

            // Map the updated entity to its corresponding DTO and return it
            return ObjectMapper.Map<AssigmentType, AssigmentTypeDto>(assigmentType);
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while updating AssigmentType {Id}", id);
            throw new UserFriendlyException("Unable to update assignment type. Please try again later.");
        }
    }
}
