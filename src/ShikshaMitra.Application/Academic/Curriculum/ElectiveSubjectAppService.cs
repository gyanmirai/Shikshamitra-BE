using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Curriculum;

/*
 * Note: This implementation assumes you have created:
 * 1. An `IElectiveSubjectAppService` interface in your Application.Contracts project.
 * 2. `ElectiveSubjectDto` and `CreateUpdateElectiveSubjectDto` in your Application.Contracts project.
 * 3. An AutoMapper profile to map `ElectiveSubject` -> `ElectiveSubjectDto`.
 */
public class ElectiveSubjectAppService(
	IRepository<ElectiveSubject, Guid> electiveSubjectRepository,
	ElectiveSubjectManager electiveSubjectManager,
	ILogger<ElectiveSubjectAppService> logger
) : ApplicationService, IElectiveSubjectAppService
{
	public async Task<ElectiveSubjectDto> CreateAsync(CreateUpdateElectiveSubjectDto input)
	{
		try
		{
			var electiveSubject = await electiveSubjectManager.CreateAsync(
				input.Name,
				input.Color,
				input.EducationMediumId,
				input.SubjectIds
			);

			await electiveSubjectRepository.InsertAsync(electiveSubject, autoSave: true);

			// Re-fetch the entity with all details to ensure correct mapping
			return await GetAsync(electiveSubject.Id);
		}
		catch (UserFriendlyException ex)
		{
			logger.LogError(
				ex,
				"A business error occurred while creating an elective subject: {Message}",
				ex.Message
			);
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error creating elective subject with name {Name}", input.Name);
			throw new UserFriendlyException(
				"An error occurred while creating the elective subject."
			);
		}
	}

	public async Task<ElectiveSubjectDto> UpdateAsync(Guid id, CreateUpdateElectiveSubjectDto input)
	{
		try
		{
			var electiveSubject = await electiveSubjectManager.UpdateAsync(
				id,
				input.Name,
				input.Color,
				input.EducationMediumId,
				input.SubjectIds
			);

			await electiveSubjectRepository.UpdateAsync(electiveSubject, autoSave: true); // Saves the changes from the manager

			// Re-fetch the entity with all details to ensure correct mapping
			return await GetAsync(id);
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

	public async Task DeleteAsync(Guid id)
	{
		try
		{
			await electiveSubjectRepository.DeleteAsync(id, autoSave: true);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error deleting elective subject with ID {Id}", id);
			throw new UserFriendlyException(
				"An error occurred while deleting the elective subject."
			);
		}
	}

	public async Task<ElectiveSubjectDto> GetAsync(Guid id)
	{
		try
		{
			var electiveSubject = await electiveSubjectRepository.FindAsync(
				id,
				includeDetails: true
			);

			return electiveSubject == null
				? throw new UserFriendlyException("Elective subject not found.")
				: ObjectMapper.Map<ElectiveSubject, ElectiveSubjectDto>(electiveSubject);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error retrieving elective subject with ID {Id}", id);
			throw new UserFriendlyException(
				"An error occurred while retrieving the elective subject."
			);
		}
	}

	public async Task<List<ElectiveSubjectDto>> GetListAsync()
	{
		try
		{
			var electiveSubjects = (
				await electiveSubjectRepository.WithDetailsAsync(x => x.Subjects)
			).ToList();

			return ObjectMapper.Map<List<ElectiveSubject>, List<ElectiveSubjectDto>>(
				electiveSubjects
			);
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
