using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Academic.Curriculum.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Application service for managing elective groups
/// Implements CRUD operations with academic medium scoping and security validation
/// </summary>
public class ElectiveGroupAppService(
	IRepository<ElectiveGroup, Guid> ElectiveGroupRepository,
	ElectiveGroupManager ElectiveGroupManager,
	ILogger<ElectiveGroupAppService> logger
) : ApplicationService, IElectiveGroupAppService
{
	/// <summary>
	/// Get all elective groups for a specific education medium with optional search
	/// </summary>
	public async Task<List<ElectiveGroupDto>> GetListAsync(
		Guid academicMediumId,
		string? searchTerm = null
	)
	{
		try
		{
			var query = await ElectiveGroupRepository.WithDetailsAsync(eg => eg.Subjects);

			var electiveGroups = query
				.Where(eg => eg.AcademicMediumId == academicMediumId)
				.WhereIf(
					!string.IsNullOrWhiteSpace(searchTerm),
					eg => eg.Name.Contains(searchTerm!)
				)
				.OrderBy(eg => eg.Name)
				.ToList();

			return ObjectMapper.Map<List<ElectiveGroup>, List<ElectiveGroupDto>>(electiveGroups);
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Error retrieving elective groups for medium {MediumId}",
				academicMediumId
			);
			throw new UserFriendlyException(
				"An error occurred while retrieving elective groups."
			);
		}
	}

	/// <summary>
	/// Get a single elective group by ID with medium verification
	/// </summary>
	public async Task<ElectiveGroupDto> GetAsync(Guid academicMediumId, Guid id)
	{
		try
		{
			var electiveGroup = await ElectiveGroupRepository.GetAsync(id, includeDetails: true);

			// Verify the group belongs to the specified medium
			if (electiveGroup.AcademicMediumId != academicMediumId)
			{
				throw new EntityNotFoundException(typeof(ElectiveGroup), id);
			}

			return ObjectMapper.Map<ElectiveGroup, ElectiveGroupDto>(electiveGroup);
		}
		catch (EntityNotFoundException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Error retrieving elective group {Id} for medium {MediumId}",
				id,
				academicMediumId
			);
			throw new UserFriendlyException(
				"An error occurred while retrieving the elective group."
			);
		}
	}

	/// <summary>
	/// Create a new elective group
	/// </summary>
	public async Task<ElectiveGroupDto> CreateAsync(
		Guid academicMediumId,
		CreateUpdateElectiveGroupDto input
	)
	{
		try
		{
			var electiveGroup = await ElectiveGroupManager.CreateAsync(
				input.Name,
				input.Color,
				academicMediumId, // From route parameter
				input.SubjectIds
			);

			// Ensure the entity is saved before re-fetching with details
			await ElectiveGroupRepository.InsertAsync(electiveGroup, autoSave: true);

			// Re-fetch with details for proper mapping
			return await GetAsync(academicMediumId, electiveGroup.Id);
		}
		catch (BusinessException ex)
		{
			logger.LogError(ex, "Business error creating elective group: {Code}", ex.Code);
			// Rethrow BusinessException - ABP middleware will handle localization
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Error creating elective group '{Name}' for medium {MediumId}",
				input.Name,
				academicMediumId
			);
			throw new UserFriendlyException(
				"An error occurred while creating the elective group."
			);
		}
	}

	/// <summary>
	/// Update an existing elective group
	/// </summary>
	public async Task<ElectiveGroupDto> UpdateAsync(
		Guid academicMediumId,
		Guid id,
		CreateUpdateElectiveGroupDto input
	)
	{
		try
		{
			var electiveGroup = await ElectiveGroupManager.UpdateAsync(
				id,
				input.Name,
				input.Color,
				academicMediumId, // Verify medium
				input.SubjectIds
			);

			await ElectiveGroupRepository.UpdateAsync(electiveGroup, autoSave: true);

			return await GetAsync(academicMediumId, id);
		}
		catch (BusinessException ex)
		{
			logger.LogError(
				ex,
				"Business error updating elective group {Id}: {Code}",
				id,
				ex.Code
			);
			// Rethrow BusinessException - ABP middleware will handle localization
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Error updating elective group {Id} for medium {MediumId}",
				id,
				academicMediumId
			);
			throw new UserFriendlyException(
				"An error occurred while updating the elective group."
			);
		}
	}

	/// <summary>
	/// Delete an elective group
	/// </summary>
	public async Task DeleteAsync(Guid academicMediumId, Guid id)
	{
		try
		{
			var electiveGroup = await ElectiveGroupRepository.GetAsync(id);

			// Verify the group belongs to the specified medium
			if (electiveGroup.AcademicMediumId != academicMediumId)
			{
				throw new EntityNotFoundException(typeof(ElectiveGroup), id);
			}

			await ElectiveGroupRepository.DeleteAsync(id);

			logger.LogInformation(
				"Deleted elective group {Id} from medium {MediumId}",
				id,
				academicMediumId
			);
		}
		catch (EntityNotFoundException)
		{
			throw;
		}
		catch (Exception ex)
		{
			logger.LogError(
				ex,
				"Error deleting elective group {Id} from medium {MediumId}",
				id,
				academicMediumId
			);
			throw new UserFriendlyException(
				"An error occurred while deleting the elective group."
			);
		}
	}
}
