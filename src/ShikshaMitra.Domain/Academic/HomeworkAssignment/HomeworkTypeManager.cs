using System;
using System.Threading.Tasks;
using ShikshaMitra.Extensions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// Domain manager for HomeworkType aggregate
/// Encapsulates business logic and validation rules
/// </summary>
public class HomeworkTypeManager : DomainService
{
	private readonly IHomeworkTypeRepository _repository;
	private readonly IEducationMediumRepository _mediumRepository;
	private readonly IDataFilter _dataFilter;

	private string EntityName => this.GetType().ToEntityNameFromManager();

	public HomeworkTypeManager(
		IHomeworkTypeRepository repository,
		IEducationMediumRepository mediumRepository,
		IDataFilter dataFilter
	)
	{
		_repository = repository;
		_mediumRepository = mediumRepository;
		_dataFilter = dataFilter;
	}

	/// <summary>
	/// Creates a new homework type with validation.
	/// If a soft-deleted record with the same name exists it is restored in-place
	/// (reusing its ID) to avoid violating the unique index on {Name, AcademicMediumId}.
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <param name="name">Homework type name</param>
	/// <returns>Persisted HomeworkType entity (inserted or restored)</returns>
	/// <exception cref="BusinessException">Thrown when validation fails</exception>
	public async Task<HomeworkType> CreateAsync(Guid academicMediumId, string name)
	{
		// Validate academic medium exists
		await ValidateAcademicMediumExistsAsync(academicMediumId);

		// Check including soft-deleted rows — the unique DB index does not filter on IsDeleted,
		// so re-creating a previously deleted name would hit a constraint violation.
		HomeworkType? existing;
		using (_dataFilter.Disable<ISoftDelete>())
		{
			existing = await _repository.FindByNameAndAcademicMediumIdAsync(name, academicMediumId);
		}

		if (existing != null && !existing.IsDeleted)
		{
			throw new BusinessException(HomeworkAssignmentErrorCodes.DuplicateHomeworkType)
				.WithData("Name", name);
		}

		if (existing != null && existing.IsDeleted)
		{
			// Restore the soft-deleted record rather than inserting a duplicate row.
			existing.IsDeleted = false;
			existing.DeletionTime = null;
			existing.DeleterId = null;
			existing.SetName(name);
			return await _repository.UpdateAsync(existing, autoSave: true);
		}

		var entity = new HomeworkType(GuidGenerator.Create(), name, academicMediumId, CurrentTenant.Id);
		return await _repository.InsertAsync(entity, autoSave: true);
	}

	/// <summary>
	/// Updates an existing homework type with validation
	/// </summary>
	/// <param name="homeworkType">Homework type to update</param>
	/// <param name="newName">New name for the homework type</param>
	/// <returns>Updated HomeworkType entity</returns>
	/// <exception cref="BusinessException">Thrown when validation fails</exception>
	public async Task<HomeworkType> UpdateAsync(HomeworkType homeworkType, string newName)
	{
		Check.NotNull(homeworkType, nameof(homeworkType));

		// If name hasn't changed, return as-is
		if (homeworkType.Name == newName)
		{
			return homeworkType;
		}

		// Validate new name doesn't conflict with existing homework types
		await ValidateHomeworkTypeAsync(
			newName,
			homeworkType.AcademicMediumId,
			homeworkType.Id
		);

		// Update name
		homeworkType.SetName(newName);

		return homeworkType;
	}

	/// <summary>
	/// Validates that a homework type name is unique within an academic medium
	/// </summary>
	/// <param name="name">Homework type name</param>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <param name="excludeId">ID to exclude from duplicate check (for updates)</param>
	/// <exception cref="BusinessException">Thrown when duplicate name found</exception>
	private async Task ValidateHomeworkTypeAsync(
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
			throw new BusinessException(HomeworkAssignmentErrorCodes.DuplicateHomeworkType)
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
