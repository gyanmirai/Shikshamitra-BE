using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Application service for managing assignment types
/// </summary>
[Authorize]
public class AssignmentTypeAppService : ApplicationService, IAssignmentTypeAppService
{
	private readonly IAssignmentTypeRepository _repository;
	private readonly AssignmentTypeManager _manager;
	private readonly IEducationMediumRepository _mediumRepository;
	private readonly ILogger<AssignmentTypeAppService> _logger;

	public AssignmentTypeAppService(
		IAssignmentTypeRepository repository,
		AssignmentTypeManager manager,
		IEducationMediumRepository mediumRepository,
		ILogger<AssignmentTypeAppService> logger
	)
	{
		_repository = repository;
		_manager = manager;
		_mediumRepository = mediumRepository;
		_logger = logger;
	}

	/// <summary>
	/// Creates a new assignment type
	/// </summary>
	public async Task<AssignmentTypeCreateResponseDto> CreateAsync(
		Guid academicMediumId,
		CreateAssignmentTypeDto input
	)
	{
		try
		{
			_logger.LogInformation(
				"Creating assignment type '{Name}' for academic medium {MediumId}",
				input.AssignmentTypeName,
				academicMediumId
			);

			// Create through domain manager (handles all business logic + persistence)
			var assignmentType = await _manager.CreateAsync(
				academicMediumId,
				input.AssignmentTypeName
			);

			// Map to DTO
			var dto = ObjectMapper.Map<AssignmentType, AssignmentTypeDto>(assignmentType);

			_logger.LogInformation(
				"Successfully created assignment type with ID {Id}",
				assignmentType.Id
			);

			return new AssignmentTypeCreateResponseDto
			{
				Message = "Assignment type created successfully",
				Data = dto
			};
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Failed to create assignment type '{Name}' for academic medium {MediumId}",
				input.AssignmentTypeName,
				academicMediumId
			);
			throw new UserFriendlyException(
				"Failed to create assignment type. Please try again later."
			);
		}
	}

	/// <summary>
	/// Gets all assignment types for a specific academic medium
	/// </summary>
	public async Task<AssignmentTypeListResponseDto> GetAllAsync(Guid academicMediumId)
	{
		try
		{
			_logger.LogDebug(
				"Fetching all assignment types for academic medium {MediumId}",
				academicMediumId
			);

			// Validate medium exists
			await ValidateAcademicMediumAsync(academicMediumId);

			// Fetch from repository
			var types = await _repository.GetListByAcademicMediumIdAsync(academicMediumId);

			// Map to DTOs
			var dtos = ObjectMapper.Map<List<AssignmentType>, List<AssignmentTypeDto>>(types);

			_logger.LogDebug(
				"Found {Count} assignment types for academic medium {MediumId}",
				dtos.Count,
				academicMediumId
			);

			return new AssignmentTypeListResponseDto { Data = dtos };
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Failed to fetch assignment types for academic medium {MediumId}",
				academicMediumId
			);
			throw new UserFriendlyException(
				"Failed to fetch assignment types. Please try again later."
			);
		}
	}

	/// <summary>
	/// Gets a single assignment type by ID
	/// </summary>
	public async Task<AssignmentTypeGetResponseDto> GetAsync(Guid id, Guid academicMediumId)
	{
		try
		{
			_logger.LogDebug("Fetching assignment type {Id}", id);

			var assignmentType = await _repository.GetAsync(id);

			// Verify belongs to medium
			if (assignmentType.AcademicMediumId != academicMediumId)
			{
				_logger.LogWarning(
					"Assignment type {Id} does not belong to academic medium {MediumId}",
					id,
					academicMediumId
				);
				throw new EntityNotFoundException(typeof(AssignmentType), id);
			}

			var dto = ObjectMapper.Map<AssignmentType, AssignmentTypeDto>(assignmentType);

			return new AssignmentTypeGetResponseDto { Data = dto };
		}
		catch (EntityNotFoundException)
		{
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to fetch assignment type {Id}", id);
			throw new UserFriendlyException(
				"Failed to fetch assignment type. Please try again later."
			);
		}
	}

	/// <summary>
	/// Updates an existing assignment type
	/// </summary>
	public async Task<MessageResponseDto> UpdateAsync(
		Guid id,
		Guid academicMediumId,
		UpdateAssignmentTypeDto input
	)
	{
		try
		{
			_logger.LogInformation(
				"Updating assignment type {Id} with name '{Name}'",
				id,
				input.AssignmentTypeName
			);

			var assignmentType = await _repository.GetAsync(id);

			// Verify belongs to medium
			if (assignmentType.AcademicMediumId != academicMediumId)
			{
				_logger.LogWarning(
					"Assignment type {Id} does not belong to academic medium {MediumId}",
					id,
					academicMediumId
				);
				throw new EntityNotFoundException(typeof(AssignmentType), id);
			}

			// Update through domain manager
			await _manager.UpdateAsync(assignmentType, input.AssignmentTypeName);

			// Persist changes
			await _repository.UpdateAsync(assignmentType, autoSave: true);

			_logger.LogInformation("Successfully updated assignment type {Id}", id);

			return new MessageResponseDto { Message = "Assignment type updated successfully" };
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (EntityNotFoundException)
		{
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to update assignment type {Id}", id);
			throw new UserFriendlyException(
				"Failed to update assignment type. Please try again later."
			);
		}
	}

	/// <summary>
	/// Deletes an assignment type (soft delete)
	/// </summary>
	public async Task<MessageResponseDto> DeleteAsync(Guid id, Guid academicMediumId)
	{
		try
		{
			_logger.LogInformation("Deleting assignment type {Id}", id);

			var assignmentType = await _repository.GetAsync(id);

			// Verify belongs to medium
			if (assignmentType.AcademicMediumId != academicMediumId)
			{
				_logger.LogWarning(
					"Assignment type {Id} does not belong to academic medium {MediumId}",
					id,
					academicMediumId
				);
				throw new EntityNotFoundException(typeof(AssignmentType), id);
			}

			// Soft delete (FullAuditedAggregateRoot)
			await _repository.DeleteAsync(assignmentType, autoSave: true);

			_logger.LogInformation("Successfully deleted assignment type {Id}", id);

			return new MessageResponseDto { Message = "Assignment type deleted successfully" };
		}
		catch (EntityNotFoundException)
		{
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to delete assignment type {Id}", id);
			throw new UserFriendlyException(
				"Failed to delete assignment type. Please try again later."
			);
		}
	}

	/// <summary>
	/// Validates that the academic medium exists
	/// </summary>
	private async Task ValidateAcademicMediumAsync(Guid academicMediumId)
	{
		var medium = await _mediumRepository.FindAsync(academicMediumId);

		if (medium == null)
		{
			var correlationId = Guid.NewGuid();
			_logger.LogWarning(
				"Academic medium not found. MediumId: {MediumId}, CorrelationId: {CorrelationId}",
				academicMediumId,
				correlationId
			);

			throw new BusinessException("ShikshaMitra:AcademicMediumNotFound")
				.WithData("AcademicMediumId", academicMediumId)
				.WithData("CorrelationId", correlationId);
		}
	}
}
