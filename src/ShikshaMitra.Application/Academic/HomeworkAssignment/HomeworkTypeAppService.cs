using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Application service for managing homework types
/// </summary>
[Authorize]
public class HomeworkTypeAppService : ApplicationService, IHomeworkTypeAppService
{
	private readonly IHomeworkTypeRepository _repository;
	private readonly HomeworkTypeManager _manager;
	private readonly IEducationMediumRepository _mediumRepository;
	private readonly ILogger<HomeworkTypeAppService> _logger;
	private readonly IRepository<HomeworkAssignmentEntity, Guid> _homeworkRepository;

	public HomeworkTypeAppService(
		IHomeworkTypeRepository repository,
		HomeworkTypeManager manager,
		IEducationMediumRepository mediumRepository,
		ILogger<HomeworkTypeAppService> logger,
		IRepository<HomeworkAssignmentEntity, Guid> homeworkRepository
	)
	{
		_repository = repository;
		_manager = manager;
		_mediumRepository = mediumRepository;
		_logger = logger;
		_homeworkRepository = homeworkRepository;
	}

	/// <summary>
	/// Creates a new homework type
	/// </summary>
	public async Task<HomeworkTypeCreateResponseDto> CreateAsync(
		Guid academicMediumId,
		CreateHomeworkTypeDto input
	)
	{
		try
		{
			_logger.LogInformation(
				"Creating homework type '{Name}' for academic medium {MediumId}",
				input.HomeworkTypeName,
				academicMediumId
			);

			// Create through domain manager (handles all business logic + persistence)
			var homeworkType = await _manager.CreateAsync(
				academicMediumId,
				input.HomeworkTypeName,
				input.Description,
				input.DefaultSubmissionType,
				input.DefaultMarks,
				input.DefaultDueDays,
				input.EvaluationRequired,
				input.DefaultEvaluationMethod,
				input.Status
			);

			// Map to DTO
			var dto = ObjectMapper.Map<HomeworkType, HomeworkTypeDto>(homeworkType);
			dto.UsageCount = 0;

			_logger.LogInformation(
				"Successfully created homework type with ID {Id}",
				homeworkType.Id
			);

			return new HomeworkTypeCreateResponseDto
			{
				Message = "Homework type created successfully",
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
				"Failed to create homework type '{Name}' for academic medium {MediumId}",
				input.HomeworkTypeName,
				academicMediumId
			);
			throw new UserFriendlyException(
				"Failed to create homework type. Please try again later."
			);
		}
	}

	/// <summary>
	/// Gets all homework types for a specific academic medium
	/// </summary>
	public async Task<HomeworkTypeListResponseDto> GetAllAsync(Guid academicMediumId)
	{
		try
		{
			_logger.LogDebug(
				"Fetching all homework types for academic medium {MediumId}",
				academicMediumId
			);

			// Validate medium exists
			await ValidateAcademicMediumAsync(academicMediumId);

			// Fetch from repository
			var types = await _repository.GetListByAcademicMediumIdAsync(academicMediumId);

			// Map to DTOs
			var dtos = ObjectMapper.Map<List<HomeworkType>, List<HomeworkTypeDto>>(types);

			foreach (var dto in dtos)
			{
				dto.UsageCount = await _homeworkRepository.CountAsync(h => h.HomeworkTypeId == dto.Id);
			}

			_logger.LogDebug(
				"Found {Count} homework types for academic medium {MediumId}",
				dtos.Count,
				academicMediumId
			);

			return new HomeworkTypeListResponseDto { Data = dtos };
		}
		catch (BusinessException)
		{
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Failed to fetch homework types for academic medium {MediumId}",
				academicMediumId
			);
			throw new UserFriendlyException(
				"Failed to fetch homework types. Please try again later."
			);
		}
	}

	/// <summary>
	/// Gets a single homework type by ID
	/// </summary>
	public async Task<HomeworkTypeGetResponseDto> GetAsync(Guid id, Guid academicMediumId)
	{
		try
		{
			_logger.LogDebug("Fetching homework type {Id}", id);

			var homeworkType = await _repository.GetAsync(id);

			// Verify belongs to medium
			if (homeworkType.AcademicMediumId != academicMediumId)
			{
				_logger.LogWarning(
					"Homework type {Id} does not belong to academic medium {MediumId}",
					id,
					academicMediumId
				);
				throw new EntityNotFoundException(typeof(HomeworkType), id);
			}

			var dto = ObjectMapper.Map<HomeworkType, HomeworkTypeDto>(homeworkType);
			dto.UsageCount = await _homeworkRepository.CountAsync(h => h.HomeworkTypeId == dto.Id);

			return new HomeworkTypeGetResponseDto { Data = dto };
		}
		catch (EntityNotFoundException)
		{
			throw;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to fetch homework type {Id}", id);
			throw new UserFriendlyException(
				"Failed to fetch homework type. Please try again later."
			);
		}
	}

	/// <summary>
	/// Updates an existing homework type
	/// </summary>
	public async Task<MessageResponseDto> UpdateAsync(
		Guid id,
		Guid academicMediumId,
		UpdateHomeworkTypeDto input
	)
	{
		try
		{
			_logger.LogInformation(
				"Updating homework type {Id} with name '{Name}'",
				id,
				input.HomeworkTypeName
			);

			var homeworkType = await _repository.GetAsync(id);

			// Verify belongs to medium
			if (homeworkType.AcademicMediumId != academicMediumId)
			{
				_logger.LogWarning(
					"Homework type {Id} does not belong to academic medium {MediumId}",
					id,
					academicMediumId
				);
				throw new EntityNotFoundException(typeof(HomeworkType), id);
			}

			// Update through domain manager
			await _manager.UpdateAsync(
				homeworkType,
				input.HomeworkTypeName,
				input.Description,
				input.DefaultSubmissionType,
				input.DefaultMarks,
				input.DefaultDueDays,
				input.EvaluationRequired,
				input.DefaultEvaluationMethod,
				input.Status
			);

			// Persist changes
			await _repository.UpdateAsync(homeworkType, autoSave: true);

			_logger.LogInformation("Successfully updated homework type {Id}", id);

			return new MessageResponseDto { Message = "Homework type updated successfully" };
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
			_logger.LogError(ex, "Failed to update homework type {Id}", id);
			throw new UserFriendlyException(
				"Failed to update homework type. Please try again later."
			);
		}
	}

	/// <summary>
	/// Deletes a homework type (soft delete)
	/// </summary>
	public async Task<MessageResponseDto> DeleteAsync(Guid id, Guid academicMediumId)
	{
		try
		{
			_logger.LogInformation("Deleting homework type {Id}", id);

			var homeworkType = await _repository.GetAsync(id);

			// Verify belongs to medium
			if (homeworkType.AcademicMediumId != academicMediumId)
			{
				_logger.LogWarning(
					"Homework type {Id} does not belong to academic medium {MediumId}",
					id,
					academicMediumId
				);
				throw new EntityNotFoundException(typeof(HomeworkType), id);
			}

			// Block deletion if the homework type is in use
			var usageParts = new System.Collections.Generic.List<string>();

			var homeworkCount = await _homeworkRepository.CountAsync(h => h.HomeworkTypeId == id);
			if (homeworkCount > 0)
				usageParts.Add($"{homeworkCount} homework(s)");

			if (usageParts.Count > 0)
			{
				throw new BusinessException(HomeworkAssignmentErrorCodes.HomeworkTypeIsInUse)
					.WithData("message", $"Cannot delete '{homeworkType.Name}' because it is in use: {string.Join(", ", usageParts)}. Please remove these usages first.");
			}

			// Block if it's the last active homework type
			if (homeworkType.Status == TaskTypeStatus.Active)
			{
				var activeCount = await _repository.CountAsync(t =>
					t.AcademicMediumId == homeworkType.AcademicMediumId &&
					t.Status == TaskTypeStatus.Active &&
					t.Id != homeworkType.Id
				);
				if (activeCount == 0)
				{
					throw new BusinessException(HomeworkAssignmentErrorCodes.AtLeastOneActiveHomeworkTypeMustRemain)
						.WithData("message", "At least one active homework type must remain. You cannot delete this type.");
				}
			}

			// Soft delete (FullAuditedAggregateRoot)
			await _repository.DeleteAsync(homeworkType, autoSave: true);

			_logger.LogInformation("Successfully deleted homework type {Id}", id);

			return new MessageResponseDto { Message = "Homework type deleted successfully" };
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
			_logger.LogError(ex, "Failed to delete homework type {Id}", id);
			throw new UserFriendlyException(
				"Failed to delete homework type. Please try again later."
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
