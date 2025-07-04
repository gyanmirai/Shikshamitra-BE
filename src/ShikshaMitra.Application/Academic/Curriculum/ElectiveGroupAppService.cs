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
 * 1. An `IElectiveGroupAppService` interface in your Application.Contracts project.
 * 2. `ElectiveGroupDto` and `CreateUpdateElectiveGroupDto` in your Application.Contracts project.
 * 3. An AutoMapper profile to map `ElectiveGroup` -> `ElectiveGroupDto`.
 */
public class ElectiveGroupAppService(
	IRepository<ElectiveGroup, Guid> ElectiveGroupRepository,
	ElectiveGroupManager ElectiveGroupManager,
	ILogger<ElectiveGroupAppService> logger
) : ApplicationService, IElectiveGroupAppService
{
	public async Task<ElectiveGroupDto> CreateAsync(CreateUpdateElectiveGroupDto input)
	{
		try
		{
			var ElectiveGroup = await ElectiveGroupManager.CreateAsync(
				input.Name,
				input.Color,
				input.EducationMediumId,
				input.SubjectIds
			);

			await ElectiveGroupRepository.InsertAsync(ElectiveGroup);

			// Re-fetch the entity with all details to ensure correct mapping
			return await GetAsync(ElectiveGroup.Id);
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

	public async Task<ElectiveGroupDto> UpdateAsync(Guid id, CreateUpdateElectiveGroupDto input)
	{
		try
		{
			var ElectiveGroup = await ElectiveGroupManager.UpdateAsync(
				id,
				input.Name,
				input.Color,
				input.SubjectIds
			);

			await ElectiveGroupRepository.UpdateAsync(ElectiveGroup, autoSave: true); // Saves the changes from the manager

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
			await ElectiveGroupRepository.DeleteAsync(id, autoSave: true);
		}
		catch (Exception ex)
		{
			logger.LogError(ex, "Error deleting elective subject with ID {Id}", id);
			throw new UserFriendlyException(
				"An error occurred while deleting the elective subject."
			);
		}
	}

	public async Task<ElectiveGroupDto> GetAsync(Guid id)
	{
		try
		{
			var ElectiveGroup = await ElectiveGroupRepository.FindAsync(id);

			return ElectiveGroup == null
				? throw new UserFriendlyException("Elective subject not found.")
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

	public async Task<List<ElectiveGroupDto>> GetListAsync()
	{
		try
		{
			var ElectiveGroups = (
				await ElectiveGroupRepository.WithDetailsAsync(x => x.Subjects)
			).ToList();

			return ObjectMapper.Map<List<ElectiveGroup>, List<ElectiveGroupDto>>(ElectiveGroups);
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
