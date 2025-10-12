using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Curriculum.Setting;
using ShikshaMitra.Academic.HomeworkAssignment.Setting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.ObjectMapping;

namespace ShikshaMitra.Academic.Examination.Setting;


[RemoteService(IsEnabled = false)]
public class ExamTypeAppService(
	IRepository<ExamType, Guid> examTypeRepository,	
	ILogger<SubjectAppService> logger
	) : ApplicationService, IExamTypeAppService
{
	public async Task<ExamTypeDto> CreateExamTypeAsync(CreateExamTypeDto createExamTypeDto)
	{
		try
		{
			//Validate that the examtype name is not null, empty, or whitespace
			if (string.IsNullOrWhiteSpace(createExamTypeDto.Name))
			{
				throw new UserFriendlyException("Exam type name is required.");
			}

			// Validate that the EducationMediumId is provided and not empty
			if (createExamTypeDto.EducationMediumId == Guid.Empty)
			{
				throw new UserFriendlyException("Education Medium is required.");
			}

			// Check for duplicate EXAMTYPEtype name within the same EducationMedium
			var examtypeexists = await examTypeRepository.AnyAsync(
			x => x.Name.ToLower() == createExamTypeDto.Name.Trim().ToLower() &&
			x.EducationMediumId == createExamTypeDto.EducationMediumId
			);

			if (examtypeexists)
			{
				// Throw exception if a duplicate is found
				throw new UserFriendlyException(
					$"Exam type '{createExamTypeDto.Name}' already examtypeexists for this tenant. Please choose a different name."
				);
			}

			//Create a new ExamType entity
			var newExamType = new ExamType(
				GuidGenerator.Create(),
				CurrentTenant.Id,
				createExamTypeDto.Name.Trim(),
				createExamTypeDto.EducationMediumId,
				createExamTypeDto.Description
			);

			//Insert the new entity into the repository and save changes
			await examTypeRepository.InsertAsync(newExamType, autoSave: true);

			logger.LogInformation("ExamType created successfully: {Name}", createExamTypeDto.Name);

			//Map to DTO and return
			return ObjectMapper.Map<ExamType, ExamTypeDto>(newExamType);
		}
		catch (UserFriendlyException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error while creating ExamType {Name}", createExamTypeDto.Name);
			throw new UserFriendlyException("An unexpected error occurred while creating the exam type. Please try again.");
		}
	}
    public async Task<bool> DeleteExamTypeAsync(Guid id)
    {
        try
        {
            var homeworkTypeEntity = await examTypeRepository.FindAsync(id);

            if (homeworkTypeEntity == null)
                throw new UserFriendlyException("Homework type not found.");

            await examTypeRepository.DeleteAsync(homeworkTypeEntity);

            logger.LogInformation("HomeworkType deleted successfully: {Name}", homeworkTypeEntity.Name);

            return true;
        }
        catch (UserFriendlyException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error while deleting HomeworkType {Id}", id);
            throw new UserFriendlyException("Unable to delete homework type. Please try again later.");
        }
    }
	public async Task<List<ExamTypeDto>> GetExamTypeListAsync(Guid educationMediumId)
	{
		try
		{

			// Declare a list to hold the examtypes retrieved from the repository
			List<ExamType> examTypes;

			// Check if an academic medium ID is provided and is not empty
			if (educationMediumId != Guid.Empty)
			{
				// Fetch examtypes filtered by the specified EducationMediumId
				examTypes = await examTypeRepository.GetListAsync(
					x => x.EducationMediumId == educationMediumId);
			}
			else
			{
				// Fetch all examtypes if no specific EducationMediumId is provided
				examTypes = await examTypeRepository.GetListAsync();
			}

			// Map the list of examType entities to a list of examTypeDto objects and return
			return ObjectMapper.Map<List<ExamType>, List<ExamTypeDto>>(examTypes);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error while fetching homework types.");
			throw new UserFriendlyException("Unable to fetch homework types. Please try again later.");
		}
	}
	public async Task<ExamTypeDto> UpdateExamTypeAsync(Guid id, UpdateExamTypeDto updateExamTypeDto)
	{
		try
		{

			var examType = await examTypeRepository.FindAsync(id);

			// If the entity does not exist, throw a user-friendly exception
			if (examType == null)
				throw new UserFriendlyException("Homework type not found.");

			//Validate input name
			if (string.IsNullOrWhiteSpace(updateExamTypeDto.Name))
				throw new UserFriendlyException("Exam type name is required.");

			// Check if another homework type with the same name exists in the same academic medium
			var homeworktypeexist = await examTypeRepository.AnyAsync(x =>
			x.Id != id &&
			x.EducationMediumId == examType.EducationMediumId &&
			x.Name.ToLower() == updateExamTypeDto.Name.Trim().ToLower()
			);

			// If a duplicate is found, throw a user-friendly exception
			if (homeworktypeexist)
				throw new UserFriendlyException($"Homework type '{updateExamTypeDto.Name}' already homeworktypeexist in this academic medium.");

			// Update the entity's name with the new value after trimming whitespace
			examType.SetName(updateExamTypeDto.Name.Trim());
			examType.SetDescription(updateExamTypeDto.Description);

			// Persist the updated entity to the repository with auto-save enabled
			await examTypeRepository.UpdateAsync(examType, autoSave: true);

			// Log the successful update operation
			logger.LogInformation("examtype updated successfully: {Name}", examType.Name);

			// Map the updated entity to its corresponding DTO and return it
			return ObjectMapper.Map<ExamType, ExamTypeDto>(examType);
		}
		catch (UserFriendlyException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error while updating HomeworkType {Id}", id);
			throw new UserFriendlyException("Unable to update homework type. Please try again later.");
		}
	}
}
