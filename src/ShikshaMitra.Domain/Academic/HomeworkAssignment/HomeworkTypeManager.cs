using System;
using System.Threading.Tasks;
using ShikshaMitra.Extensions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
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

	private string EntityName => this.GetType().ToEntityNameFromManager();

	public HomeworkTypeManager(
		IHomeworkTypeRepository repository,
		IEducationMediumRepository mediumRepository
	)
	{
		_repository = repository;
		_mediumRepository = mediumRepository;
	}

	/// <summary>
	/// Creates a new homework type with validation
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <param name="name">Homework type name</param>
	/// <returns>Created HomeworkType entity</returns>
	/// <exception cref="BusinessException">Thrown when validation fails</exception>
	public async Task<HomeworkType> CreateAsync(Guid academicMediumId, string name)
	{
		// Validate academic medium exists
		await ValidateAcademicMediumExistsAsync(academicMediumId);

		// Check for duplicate name within the same academic medium
		await ValidateHomeworkTypeAsync(name, academicMediumId);

		// Create new entity
		return new HomeworkType(
			GuidGenerator.Create(),
			name,
			academicMediumId,
			CurrentTenant.Id
		);
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
			throw new BusinessException("ShikshaMitra:DuplicateHomeworkType")
				.WithData("Name", name)
				.WithData("AcademicMediumId", academicMediumId);
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
			throw new BusinessException("ShikshaMitra:AcademicMediumNotFound").WithData(
				"AcademicMediumId",
				academicMediumId
			);
		}
	}
}
