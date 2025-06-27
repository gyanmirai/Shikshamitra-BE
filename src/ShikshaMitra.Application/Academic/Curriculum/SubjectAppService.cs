using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Curriculum;

[RemoteService(IsEnabled = false)]
public class SubjectAppService(
	IRepository<Subject, Guid> subjectRepository,
	SubjectManager subjectManager,
	ILogger<SubjectAppService> logger
) : ApplicationService, ISubjectAppService
{
	public async Task<SubjectDto> CreateAsync(CreateUpdateSubjectDto createSubjectDto)
	{
		try
		{
			Subject subject = await subjectManager.CreateSubject(
				createSubjectDto.Name,
				createSubjectDto.Color,
				createSubjectDto.EducationMediumId
			);

			await subjectRepository.InsertAsync(subject);

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
				"Error creating subject with name {SubjectName} for medium {EducationMediumId}",
				createSubjectDto.Name,
				createSubjectDto.EducationMediumId
			);
			throw new UserFriendlyException("Error occurred while creating subject");
		}
	}

	public async Task DeleteAsync(Guid id)
	{
		try
		{
			var subject = await subjectRepository.GetAsync(id);
			await subjectRepository.DeleteAsync(id);
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
			List<Subject> subjects = await subjectRepository.GetListAsync(m =>
				m.EducationMediumId == educationMediumId
			);

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

	public async Task<SubjectDto> UpdateAsync(Guid id, CreateUpdateSubjectDto updateSubjectDto)
	{
		try
		{
			var subject = await subjectManager.UpdateAsync(
				id,
				updateSubjectDto.Name,
				updateSubjectDto.Color,
				updateSubjectDto.EducationMediumId
			);

			await subjectRepository.UpdateAsync(subject, autoSave: true);

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
