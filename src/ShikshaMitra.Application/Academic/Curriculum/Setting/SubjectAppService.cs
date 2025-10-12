using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Curriculum.Setting;

[RemoteService(IsEnabled = false)]
public class SubjectAppService(
	IRepository<Subject, Guid> subjectRepository,
	SubjectManager subjectManager,
	ILogger<SubjectAppService> logger
) : ApplicationService, ISubjectAppService
{
    public async Task<SubjectDto> CreateSubjectAsync(CreateSubjectDto createSubjectDto)
    {
        try
        {
            // Create a new Subject entity using the SubjectManager
            var subject = await subjectManager.CreateSubject(
                createSubjectDto.Name,
                createSubjectDto.EducationMediumId,
                createSubjectDto.Color
            );

            // Insert the newly created Subject into the repository and save changes
            await subjectRepository.InsertAsync(subject, autoSave: true);

            // Map the Subject entity to SubjectDto for returning to the client
            return ObjectMapper.Map<Subject, SubjectDto>(subject);
        }
        catch (UserFriendlyException ex)
        {
            logger.LogError(ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Error creating subject with name {SubjectName}",
                createSubjectDto.Name
            );
            throw new UserFriendlyException("A subject with the same details already exists.");
        }
    }

    public async Task<bool> DeleteSubjectAsync(Guid id)
    {
        try
        {
            // Retrieve the subject entity by its ID from the repository
            var subject = await subjectRepository.GetAsync(id);

            // If the subject does not exist, return false indicating deletion is not possible
            if (subject == null)
            {
                return false;
            }

            // Delete the subject entity from the repository
            await subjectRepository.DeleteAsync(id);

            // Return true to indicate successful deletion
            return true;
        }
        catch (UserFriendlyException ex)
        {
            logger.LogError(ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error deleting subject with ID {SubjectId}", id);
            throw new UserFriendlyException("Error occurred while deleting subject");
        }
    }

	public async Task<List<SubjectDto>> GetListAsync(Guid educationMediumId)
	{
		try
		{
            // Retrieve all subjects that match the specified EducationMediumId from the repository
            var subjects = await subjectRepository.GetListAsync(x =>
                x.EducationMediumId == educationMediumId
            );

            // Map the list of Subject entities to a list of SubjectDto objects for returning to the client
            return ObjectMapper.Map<List<Subject>, List<SubjectDto>>(subjects);
		}
		catch (UserFriendlyException ex)
		{
			logger.LogError(ex.Message);
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Error retrieving subjects for medium {EducationMediumId}",
				educationMediumId
			);
			throw new UserFriendlyException("Error occurred while retrieving subjects");
		}
	}

	public async Task<SubjectDto> UpdateSubjectAsync(Guid id, UpdateSubjectDto updateSubjectDto)
	{
		try
		{
            // Update the subject entity using the SubjectManager with the provided details
            var subject = await subjectManager.UpdateAsync(
                id,
                updateSubjectDto.Name,
                updateSubjectDto.Color,
                null
            );

            // Save the updated subject entity to the repository
            await subjectRepository.UpdateAsync(subject, autoSave: true);

            // Map the updated Subject entity to SubjectDto for returning to the client
            return ObjectMapper.Map<Subject, SubjectDto>(subject);
		}
		catch (UserFriendlyException ex)
		{
			logger.LogError(ex.Message);
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error updating subject with ID {SubjectId}", id);
			throw new UserFriendlyException("Error occurred while updating subject");
		}
	}
}
