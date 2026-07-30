using System;
using System.Threading.Tasks;
using ShikshaMitra.Extensions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Domain manager for AssignmentType aggregate
/// Encapsulates business logic and validation rules
/// </summary>
public class AssignmentTypeManager : DomainService
{
	private readonly IAssignmentTypeRepository _repository;
	private readonly IEducationMediumRepository _mediumRepository;
	private readonly IDataFilter _dataFilter;

	private string EntityName => this.GetType().ToEntityNameFromManager();

	public AssignmentTypeManager(
		IAssignmentTypeRepository repository,
		IEducationMediumRepository mediumRepository,
		IDataFilter dataFilter
	)
	{
		_repository = repository;
		_mediumRepository = mediumRepository;
		_dataFilter = dataFilter;
	}

	/// <summary>
	/// Creates a new assignment type with validation.
	/// If a soft-deleted record with the same name exists it is restored in-place
	/// (reusing its ID) to avoid violating the unique index on {Name, AcademicMediumId}.
	/// </summary>
	public async Task<AssignmentType> CreateAsync(
		Guid academicMediumId,
		string name,
		string? description = null,
		SubmissionType? defaultSubmissionType = null,
		decimal? defaultMarks = null,
		int? defaultDueDays = null,
		bool evaluationRequired = true,
		EvaluationMethod? defaultEvaluationMethod = null,
		TaskTypeStatus status = TaskTypeStatus.Active
	)
	{
		var normalizedName = name?.Trim() ?? string.Empty;

		// Validate academic medium exists
		await ValidateAcademicMediumExistsAsync(academicMediumId);

		// Check including soft-deleted rows — the unique DB index does not filter on IsDeleted,
		// so re-creating a previously deleted name would hit a constraint violation.
		AssignmentType? existing;
		using (_dataFilter.Disable<ISoftDelete>())
		{
			existing = await _repository.FindByNameAndAcademicMediumIdAsync(normalizedName, academicMediumId);
		}

		if (existing != null && !existing.IsDeleted)
		{
			throw new BusinessException(HomeworkAssignmentErrorCodes.DuplicateAssignmentType)
				.WithData("Name", normalizedName);
		}

		if (existing != null && existing.IsDeleted)
		{
			// Restore the soft-deleted record rather than inserting a duplicate row.
			existing.IsDeleted = false;
			existing.DeletionTime = null;
			existing.DeleterId = null;
				existing.UpdateInfo(
					normalizedName,

				description,
				defaultSubmissionType,
				defaultMarks,
				defaultDueDays,
				evaluationRequired,
				defaultEvaluationMethod,
				status
			);
			return await _repository.UpdateAsync(existing, autoSave: true);
		}

			var entity = new AssignmentType(
				GuidGenerator.Create(),
				normalizedName,

			academicMediumId,
			CurrentTenant.Id,
			description,
			defaultSubmissionType,
			defaultMarks,
			defaultDueDays,
			evaluationRequired,
			defaultEvaluationMethod,
			status
		);
		return await _repository.InsertAsync(entity, autoSave: true);
	}

	/// <summary>
	/// Updates an existing assignment type with validation
	/// </summary>
	public async Task<AssignmentType> UpdateAsync(
		AssignmentType assignmentType,
		string newName,
		string? description = null,
		SubmissionType? defaultSubmissionType = null,
		decimal? defaultMarks = null,
		int? defaultDueDays = null,
		bool evaluationRequired = true,
		EvaluationMethod? defaultEvaluationMethod = null,
		TaskTypeStatus status = TaskTypeStatus.Active
	)
	{
		Check.NotNull(assignmentType, nameof(assignmentType));
		var normalizedName = newName?.Trim() ?? string.Empty;

		// Validate new name doesn't conflict with existing assignment types
		if (!string.Equals(assignmentType.Name, normalizedName, StringComparison.Ordinal))
		{
			await ValidateAssignmentTypeAsync(
				normalizedName,
				assignmentType.AcademicMediumId,
				assignmentType.Id
			);
		}

		// Validation check: At least one active assignment type must remain
		if (status != TaskTypeStatus.Active && assignmentType.Status == TaskTypeStatus.Active)
		{
			var activeCount = await _repository.CountAsync(t =>
				t.AcademicMediumId == assignmentType.AcademicMediumId &&
				t.Status == TaskTypeStatus.Active &&
				t.Id != assignmentType.Id
			);
			if (activeCount == 0)
			{
				throw new BusinessException(HomeworkAssignmentErrorCodes.AtLeastOneActiveAssignmentTypeMustRemain)
					.WithData("message", "At least one active assignment type must remain. You cannot deactivate or archive this type.");
			}
		}

		// Update info
		assignmentType.UpdateInfo(
			normalizedName,
			description,
			defaultSubmissionType,
			defaultMarks,
			defaultDueDays,
			evaluationRequired,
			defaultEvaluationMethod,
			status
		);

		return assignmentType;
	}

	/// <summary>
	/// Validates that an assignment type name is unique within an academic medium
	/// </summary>
	/// <param name="name">Assignment type name</param>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <param name="excludeId">ID to exclude from duplicate check (for updates)</param>
	/// <exception cref="BusinessException">Thrown when duplicate name found</exception>
	private async Task ValidateAssignmentTypeAsync(
		string name,
		Guid academicMediumId,
		Guid? excludeId = null
	)
	{
		var existing = await _repository.FindByNameAndAcademicMediumIdAsync(
			name,
			academicMediumId
		);

		if (existing != null && existing.Id != excludeId)
		{
			throw new BusinessException(HomeworkAssignmentErrorCodes.DuplicateAssignmentType)
				.WithData("Name", name);
		}
	}

	/// <summary>
	/// Validates that the academic medium exists
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID to validate</param>
	/// <exception cref="BusinessException">Thrown when academic medium not found</exception>
	private async Task ValidateAcademicMediumExistsAsync(Guid academicMediumId)
	{
		var medium = await _mediumRepository.FindAsync(academicMediumId);

		if (medium == null)
		{
			throw new BusinessException(HomeworkAssignmentErrorCodes.AcademicMediumNotFound)
				.WithData("AcademicMediumId", academicMediumId);
		}
	}
}
