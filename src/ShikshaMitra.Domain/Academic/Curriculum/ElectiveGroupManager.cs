using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.Curriculum;

/// <summary>
/// Domain service for managing elective group business logic
/// </summary>
public class ElectiveGroupManager : DomainService
{
	private readonly IRepository<ElectiveGroup, Guid> ElectiveGroupRepository;
	private readonly IReadOnlyRepository<Subject, Guid> subjectRepository;
	private readonly IGuidGenerator guidGenerator;

	public ElectiveGroupManager(
		IRepository<ElectiveGroup, Guid> ElectiveGroupRepository,
		IReadOnlyRepository<Subject, Guid> subjectRepository,
		IGuidGenerator guidGenerator
	)
	{
		this.ElectiveGroupRepository = ElectiveGroupRepository;
		this.subjectRepository = subjectRepository;
		this.guidGenerator = guidGenerator;
	}

	/// <summary>
	/// Creates a new elective group with business rule validation
	/// </summary>
	/// <param name="name">Elective group name (must be unique within education medium)</param>
	/// <param name="color">Hex color code for UI display</param>
	/// <param name="educationMediumId">Education medium ID the group belongs to</param>
	/// <param name="subjectIds">List of subject IDs to include (minimum 2 required)</param>
	/// <returns>Created elective group entity</returns>
	public async Task<ElectiveGroup> CreateAsync(
		[NotNull] string name,
		[NotNull] string color,
		[NotNull] Guid educationMediumId,
		[CanBeNull] IEnumerable<Guid> subjectIds
	)
	{
		// Validate minimum subjects requirement
		ValidateMinimumSubjects(subjectIds);

		// Validate name uniqueness within the education medium
		await CheckNameIsUniqueAsync(name, educationMediumId);

		// Validate all subjects exist and belong to the specified education medium
		await ValidateSubjectsBelongToMediumAsync(subjectIds, educationMediumId);

		var ElectiveGroup = new ElectiveGroup(
			guidGenerator.Create(),
			CurrentTenant.Id,
			name,
			color,
			educationMediumId
		);

		ElectiveGroup.ReplaceSubjects(subjectIds);

		return ElectiveGroup;
	}

	/// <summary>
	/// Updates an existing elective group with business rule validation
	/// </summary>
	/// <param name="id">Elective group ID to update</param>
	/// <param name="name">Updated group name</param>
	/// <param name="color">Updated color</param>
	/// <param name="educationMediumId">Education medium ID (for validation)</param>
	/// <param name="subjectIds">Updated list of subject IDs (minimum 2 required)</param>
	/// <returns>Updated elective group entity</returns>
	public async Task<ElectiveGroup> UpdateAsync(
		Guid id,
		[NotNull] string name,
		[NotNull] string color,
		[NotNull] Guid educationMediumId,
		[CanBeNull] IEnumerable<Guid> subjectIds
	)
	{
		var ElectiveGroup = await ElectiveGroupRepository.GetAsync(id, includeDetails: true);

		// Verify the group belongs to the specified education medium
		if (ElectiveGroup.EducationMediumId != educationMediumId)
		{
			throw new BusinessException("ShikshaMitra:ElectiveGroup:CrossMediumUpdateNotAllowed")
				.WithData("ElectiveGroupId", id)
				.WithData("ExpectedMediumId", educationMediumId)
				.WithData("ActualMediumId", ElectiveGroup.EducationMediumId);
		}

		// Validate minimum subjects requirement
		ValidateMinimumSubjects(subjectIds);

		// Validate name uniqueness if name changed
		if (ElectiveGroup.Name != name)
		{
			await CheckNameIsUniqueAsync(name, educationMediumId, id);
			ElectiveGroup.SetName(name);
		}

		// Validate all subjects exist and belong to the specified education medium
		await ValidateSubjectsBelongToMediumAsync(subjectIds, educationMediumId);

		ElectiveGroup.SetColor(color);
		ElectiveGroup.ReplaceSubjects(subjectIds);

		return ElectiveGroup;
	}

	/// <summary>
	/// Validates that the elective group name is unique within the education medium
	/// </summary>
	private async Task CheckNameIsUniqueAsync(
		string name,
		Guid educationMediumId,
		Guid? expectedId = null
	)
	{
		var existing = await ElectiveGroupRepository.FindAsync(
			eg => eg.Name == name && eg.EducationMediumId == educationMediumId
		);

		if (existing != null && existing.Id != expectedId)
		{
			throw new BusinessException("ShikshaMitra:ElectiveGroup:DuplicateName")
				.WithData("Name", name)
				.WithData("EducationMediumId", educationMediumId);
		}
	}

	/// <summary>
	/// Validates that at least the minimum number of subjects are provided
	/// </summary>
	private void ValidateMinimumSubjects(IEnumerable<Guid> subjectIds)
	{
		if (subjectIds == null || subjectIds.Count() < ElectiveGroupConsts.MinSubjectsRequired)
		{
			throw new BusinessException("ShikshaMitra:ElectiveGroup:InsufficientSubjects")
				.WithData("MinimumRequired", ElectiveGroupConsts.MinSubjectsRequired)
				.WithData("Provided", subjectIds?.Count() ?? 0);
		}
	}

	/// <summary>
	/// Validates that all provided subject IDs exist and belong to the specified education medium
	/// </summary>
	private async Task ValidateSubjectsBelongToMediumAsync(
		IEnumerable<Guid> subjectIds,
		Guid educationMediumId
	)
	{
		if (subjectIds == null || !subjectIds.Any())
		{
			return;
		}

		var distinctIds = subjectIds.Distinct().ToList();

		// Fetch all subjects with the provided IDs
		var subjects = await subjectRepository.GetListAsync(s => distinctIds.Contains(s.Id));

		// Check if all subject IDs were found
		if (subjects.Count != distinctIds.Count)
		{
			var foundIds = subjects.Select(s => s.Id).ToList();
			var missingIds = distinctIds.Except(foundIds).ToList();

			throw new BusinessException("ShikshaMitra:ElectiveGroup:SubjectsNotFound")
				.WithData("MissingSubjectIds", string.Join(", ", missingIds));
		}

		// Check if all subjects belong to the specified education medium
		var invalidSubjects = subjects
			.Where(s => s.EducationMediumId != educationMediumId)
			.ToList();

		if (invalidSubjects.Any())
		{
			throw new BusinessException("ShikshaMitra:ElectiveGroup:SubjectsNotInMedium")
				.WithData("EducationMediumId", educationMediumId)
				.WithData(
					"InvalidSubjectIds",
					string.Join(", ", invalidSubjects.Select(s => s.Id))
				);
		}
	}
}
