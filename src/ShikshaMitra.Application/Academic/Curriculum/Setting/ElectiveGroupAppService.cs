using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Curriculum.Setting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Curriculum;


[RemoteService(IsEnabled = false)]
public class ElectiveGroupAppService(
	IRepository<ElectiveGroup, Guid> ElectiveGroupRepository,
	IRepository<Subject, Guid> subjectRepository,
	ElectiveGroupManager ElectiveGroupManager,
	ILogger<ElectiveGroupAppService> logger
) : ApplicationService, IElectiveGroupAppService
{
    public async Task<ElectiveGroupDto> CreateelectivegroupAsync(CreateElectiveGroupDto createElectiveGruopDto)
    {
        try
        {
            // Create a new ElectiveGroup using the manager, which handles validation and business logic
            var electiveGroup = await ElectiveGroupManager.CreateAsync(
                createElectiveGruopDto.Name,
                createElectiveGruopDto.Color,
                createElectiveGruopDto.EducationMediumId,
                createElectiveGruopDto.SubjectIds
            );

            // Persist the new ElectiveGroup to the repository
            await ElectiveGroupRepository.InsertAsync(electiveGroup, autoSave: true);

            // Get the IDs of the subjects associated with the ElectiveGroup
            var subjectIds = electiveGroup.Subjects.Select(s => s.SubjectId).ToList();

            // Fetch subject details for mapping to DTOs
            var subjects = await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id));

            // Map the ElectiveGroup entity to its DTO representation
            var dto = ObjectMapper.Map<ElectiveGroup, ElectiveGroupDto>(electiveGroup);

            // Map the associated subjects to their DTOs
            dto.Subjects = subjects.Select(s => new SubjectDto
            {
                Id = s.Id,
                Name = s.Name,
                Color = s.Color
            }).ToList();

            return dto;
        }
        catch (UserFriendlyException ex)
        {           
            logger.LogError(
                ex,
                "A business error occurred while creating an elective group: {Message}",
                ex.Message
            );
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating elective group with name {Name}", createElectiveGruopDto.Name);
            throw new UserFriendlyException(
                "An error occurred while creating the elective group."
            );
        }
    }

	

  public async Task<ElectiveGroupDto> UpdateelectivegroupAsync(Guid id, UpdateElectiveGroupDto updateElectiveGroupDto)
    {
        try
        {
            // Update the ElectiveGroup entity using the manager, which handles validation and business logic
            var ElectiveGroup = await ElectiveGroupManager.UpdateAsync(
                id,
                updateElectiveGroupDto.Name,
                updateElectiveGroupDto.Color,
                updateElectiveGroupDto.SubjectIds
            );

            // Persist the updated ElectiveGroup to the repository
            await ElectiveGroupRepository.UpdateAsync(ElectiveGroup, autoSave: true);

            // Re-fetch the entity with all details to ensure correct mapping
            return await GetelectivegroupAsync(id);
        }
        catch (UserFriendlyException ex)
        {
            logger.LogError(
                ex,
                "A business error occurred while updating elective subject {Id}: {Message}",
                id,
                ex.Message
            );
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating elective subject with ID {Id}", id);
            throw new UserFriendlyException(
                "An error occurred while updating the elective subject."
            );
        }
    }

    public async Task<bool> DeleteelectivegroupAsync(Guid id)
    {
        try
        {
            // Delete the ElectiveGroup entity from the repository
            await ElectiveGroupRepository.DeleteAsync(id, autoSave: true);

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting elective subject with ID {Id}", id);
            throw new UserFriendlyException(
                "An error occurred while deleting the elective subject."
            );
        }
    }

    public async Task<ElectiveGroupDto> GetelectivegroupAsync(Guid id)
    {
        try
        {
            // Retrieve the ElectiveGroup entity by its ID
            var ElectiveGroup = await ElectiveGroupRepository.FindAsync(id);

            return ElectiveGroup == null
                ? throw new UserFriendlyException("Elective subject not found.")
                // Map the ElectiveGroup entity to its DTO representation
                : ObjectMapper.Map<ElectiveGroup, ElectiveGroupDto>(ElectiveGroup);
        }
        catch (UserFriendlyException ex)
        {
            logger.LogError(
                ex,
                "A business error occurred while retrieving elective subject {Id}: {Message}",
                id,
                ex.Message
            );
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving elective subject with ID {Id}", id);
            throw new UserFriendlyException(
                "An error occurred while retrieving the elective subject."
            );
        }
    }

    public async Task<List<ElectiveGroupDto>> GetListelectivegroupAsync(Guid academicMediumId)
    {
        try
        {
            // Retrieve all ElectiveGroup entities for the specified EducationMedium
            List<ElectiveGroup> electiveGroups = await ElectiveGroupRepository.GetListAsync(
                x => x.EducationMediumId == academicMediumId
            );

            // Map the list of ElectiveGroup entities to their DTO representations
            return ObjectMapper.Map<List<ElectiveGroup>, List<ElectiveGroupDto>>(electiveGroups);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error retrieving elective subjects list");
            throw new UserFriendlyException(
                "An error occurred while retrieving the elective subjects."
            );
        }
    }
}