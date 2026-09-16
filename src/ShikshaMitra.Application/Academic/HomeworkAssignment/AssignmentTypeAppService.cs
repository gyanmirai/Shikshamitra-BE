using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Application service for managing assignment types
/// </summary>
[Authorize]
[RemoteService(IsEnabled = false)]
public class AssignmentTypeAppService : ApplicationService, IAssignmentTypeAppService
{
	private readonly IAssignmentTypeRepository _repository;
	private readonly AssignmentTypeManager _manager;
	private readonly IEducationMediumRepository _mediumRepository;
	private readonly ILogger<AssignmentTypeAppService> _logger;
	private readonly IRepository<AssignmentEntity, Guid> _assignmentRepository;

	public AssignmentTypeAppService(
		IAssignmentTypeRepository repository,
		AssignmentTypeManager manager,
		IEducationMediumRepository mediumRepository,
		ILogger<AssignmentTypeAppService> logger,
		IRepository<AssignmentEntity, Guid> assignmentRepository
	)
	{
		_repository = repository;
		_manager = manager;
		_mediumRepository = mediumRepository;
		_logger = logger;
		_assignmentRepository = assignmentRepository;
	}

	/// <summary>
	/// Creates a new assignment type
	/// </summary>
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsAssignmentTypes.Add)]
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
				input.AssignmentTypeName,
				input.Description,
				input.DefaultSubmissionType,
				input.DefaultMarks,
				input.DefaultDueDays,
				input.EvaluationRequired,
				input.DefaultEvaluationMethod,
				input.Status
			);

			// Map to DTO
			var dto = ObjectMapper.Map<AssignmentType, AssignmentTypeDto>(assignmentType);
			dto.UsageCount = 0;

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
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsAssignmentTypes.View)]
	public async Task<AssignmentTypeListResponseDto> GetAllAsync(Guid academicMediumId, bool includeArchived = false)
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
			if (!includeArchived)
			{
				types = types.Where(type => type.Status != TaskTypeStatus.Archived).ToList();
			}

			// Map to DTOs
			var dtos = ObjectMapper.Map<List<AssignmentType>, List<AssignmentTypeDto>>(types);

			var typeIds = dtos.Select(dto => dto.Id).ToList();
			var usageCounts = new Dictionary<Guid, int>();
			if (typeIds.Count > 0)
			{
				var assignments = await _assignmentRepository.GetQueryableAsync();
				var counts = await AsyncExecuter.ToListAsync(assignments
					.Where(assignment => typeIds.Contains(assignment.AssignmentTypeId))
					.GroupBy(assignment => assignment.AssignmentTypeId)
					.Select(group => new { TypeId = group.Key, Count = group.Count() }));
				usageCounts = counts.ToDictionary(item => item.TypeId, item => item.Count);
			}

			foreach (var dto in dtos)
			{
				dto.UsageCount = usageCounts.GetValueOrDefault(dto.Id);
			}

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
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsAssignmentTypes.View)]
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
			dto.UsageCount = await _assignmentRepository.CountAsync(a => a.AssignmentTypeId == dto.Id);

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
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsAssignmentTypes.Add)]
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
			await _manager.UpdateAsync(
				assignmentType,
				input.AssignmentTypeName,
				input.Description,
				input.DefaultSubmissionType,
				input.DefaultMarks,
				input.DefaultDueDays,
				input.EvaluationRequired,
				input.DefaultEvaluationMethod,
				input.Status
			);

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
	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsAssignmentTypes.Delete)]
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

			// Block deletion if the assignment type is in use
			var usageParts = new System.Collections.Generic.List<string>();

			var assignmentCount = await _assignmentRepository.CountAsync(a => a.AssignmentTypeId == id);
			if (assignmentCount > 0)
				usageParts.Add($"{assignmentCount} assignment(s)");

			if (usageParts.Count > 0)
			{
				throw new BusinessException(HomeworkAssignmentErrorCodes.AssignmentTypeIsInUse)
					.WithData("message", $"Cannot delete '{assignmentType.Name}' because it is in use: {string.Join(", ", usageParts)}. Please archive it to stop future usage.");
			}

			// Block if it's the last active assignment type
			if (assignmentType.Status == TaskTypeStatus.Active)
			{
				var activeCount = await _repository.CountAsync(t =>
					t.AcademicMediumId == assignmentType.AcademicMediumId &&
					t.Status == TaskTypeStatus.Active &&
					t.Id != assignmentType.Id
				);
				if (activeCount == 0)
				{
					throw new BusinessException(HomeworkAssignmentErrorCodes.AtLeastOneActiveAssignmentTypeMustRemain)
						.WithData("message", "At least one active assignment type must remain. You cannot delete this type.");
				}
			}

			// Soft delete (FullAuditedAggregateRoot)
			await _repository.DeleteAsync(assignmentType, autoSave: true);

			_logger.LogInformation("Successfully deleted assignment type {Id}", id);

			return new MessageResponseDto { Message = "Assignment type deleted successfully" };
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
			_logger.LogError(ex, "Failed to delete assignment type {Id}", id);
			throw new UserFriendlyException(
				"Failed to delete assignment type. Please try again later."
			);
		}
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsAssignmentTypes.Delete)]
	public async Task<MessageResponseDto> ArchiveAsync(Guid id, Guid academicMediumId)
	{
		var assignmentType = await GetAssignmentTypeForMediumAsync(id, academicMediumId);

		await _manager.UpdateAsync(
			assignmentType,
			assignmentType.Name,
			assignmentType.Description,
			assignmentType.DefaultSubmissionType,
			assignmentType.DefaultMarks,
			assignmentType.DefaultDueDays,
			assignmentType.EvaluationRequired,
			assignmentType.DefaultEvaluationMethod,
			TaskTypeStatus.Archived
		);

		await _repository.UpdateAsync(assignmentType, autoSave: true);

		return new MessageResponseDto { Message = "Assignment type archived successfully" };
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Academics.HomeworkAndAssignment_SettingsAssignmentTypes.Add)]
	public async Task<MessageResponseDto> RestoreAsync(Guid id, Guid academicMediumId)
	{
		var assignmentType = await GetAssignmentTypeForMediumAsync(id, academicMediumId);

		await _manager.UpdateAsync(
			assignmentType,
			assignmentType.Name,
			assignmentType.Description,
			assignmentType.DefaultSubmissionType,
			assignmentType.DefaultMarks,
			assignmentType.DefaultDueDays,
			assignmentType.EvaluationRequired,
			assignmentType.DefaultEvaluationMethod,
			TaskTypeStatus.Active
		);

		await _repository.UpdateAsync(assignmentType, autoSave: true);

		return new MessageResponseDto { Message = "Assignment type restored successfully" };
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

			throw new BusinessException(HomeworkAssignmentErrorCodes.AcademicMediumNotFound)
				.WithData("AcademicMediumId", academicMediumId)
				.WithData("CorrelationId", correlationId);
		}
	}

	private async Task<AssignmentType> GetAssignmentTypeForMediumAsync(Guid id, Guid academicMediumId)
	{
		var assignmentType = await _repository.GetAsync(id);

		if (assignmentType.AcademicMediumId != academicMediumId)
		{
			_logger.LogWarning(
				"Assignment type {Id} does not belong to academic medium {MediumId}",
				id,
				academicMediumId
			);
			throw new EntityNotFoundException(typeof(AssignmentType), id);
		}

		return assignmentType;
	}
}
