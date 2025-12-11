using System;
using System.Threading.Tasks;
using ShikshaMitra.Extensions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.EducationMediums;
using Volo.Abp;
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

	private string EntityName => this.GetType().ToEntityNameFromManager();

	public AssignmentTypeManager(
		IAssignmentTypeRepository repository,
		IEducationMediumRepository mediumRepository
	)
	{
		_repository = repository;
		_mediumRepository = mediumRepository;
	}

	/// <summary>
	/// Creates a new assignment type with validation
	/// </summary>
	/// <param name="academicMediumId">Academic medium ID</param>
	/// <param name="name">Assignment type name</param>
	/// <returns>Created AssignmentType entity</returns>
	/// <exception cref="BusinessException">Thrown when validation fails</exception>
	public async Task<AssignmentType> CreateAsync(Guid academicMediumId, string name)
	{
		// Validate academic medium exists
		await ValidateAcademicMediumExistsAsync(academicMediumId);

		// Check for duplicate name within the same academic medium
		await ValidateAssignmentTypeAsync(name, academicMediumId);

		// Create new entity
		return new AssignmentType(
			GuidGenerator.Create(),
			name,
			academicMediumId,
			CurrentTenant.Id
		);
	}

	/// <summary>
	/// Updates an existing assignment type with validation
	/// </summary>
	/// <param name="assignmentType">Assignment type to update</param>
	/// <param name="newName">New name for the assignment type</param>
	/// <returns>Updated AssignmentType entity</returns>
	/// <exception cref="BusinessException">Thrown when validation fails</exception>
	public async Task<AssignmentType> UpdateAsync(AssignmentType assignmentType, string newName)
	{
		Check.NotNull(assignmentType, nameof(assignmentType));

		// If name hasn't changed, return as-is
		if (assignmentType.Name == newName)
		{
			return assignmentType;
		}

		// Validate new name doesn't conflict with existing assignment types
		await ValidateAssignmentTypeAsync(
			newName,
			assignmentType.AcademicMediumId,
			assignmentType.Id
		);

		// Update name
		assignmentType.SetName(newName);

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
			throw new BusinessException("ShikshaMitra:DuplicateAssignmentType")
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
