using JetBrains.Annotations;
using ShikshaMitra.Academic.Curriculum.Setting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.Curriculum;

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

	public async Task<ElectiveGroup> CreateAsync(
		[NotNull] string name,
		[NotNull] string color,
		[NotNull] Guid educationMediumId,
		[CanBeNull] IEnumerable<Guid> subjectIds
	)
	{
		// Ensure the elective group name is unique before creation.
		await CheckNameIsUniqueAsync(name);

		// Validate that all provided subject IDs exist.
		await ValidateSubjectIdsAsync(subjectIds); 

		var ElectiveGroup = new ElectiveGroup(
			guidGenerator.Create(),
			CurrentTenant.Id,
			name,
			color,
			educationMediumId
		);

		// Associate the elective group with the provided subjects.
		ElectiveGroup.ReplaceSubjects(subjectIds); 

        return ElectiveGroup;
    }
	

	public async Task<ElectiveGroup> UpdateAsync(
		Guid id,
		[NotNull] string name,
		[NotNull] string color,
		[CanBeNull] IEnumerable<Guid> subjectIds
	)
	{
        var ElectiveGroup = await ElectiveGroupRepository.GetAsync(id, includeDetails: true);

        // Check if the name is being changed, and ensure uniqueness if so.
        if (ElectiveGroup.Name != name)
        {
            await CheckNameIsUniqueAsync(name, id);
            ElectiveGroup.SetName(name);
        }

        // Validate that all provided subject IDs exist.
        await ValidateSubjectIdsAsync(subjectIds);

        // Update the color of the elective group.
        ElectiveGroup.SetColor(color);

        // Replace the subjects associated with the elective group.
        ElectiveGroup.ReplaceSubjects(subjectIds);

        return ElectiveGroup;
	}

    // Checks if the elective group name is unique, excluding the expectedId if provided.
    private async Task CheckNameIsUniqueAsync(string name, Guid? expectedId = null)
    {
        var existing = await ElectiveGroupRepository.FindAsync(es => es.Name == name);
        if (existing != null && existing.Id != expectedId)
        {
            // Throws an exception if a duplicate name is found.
            throw new UserFriendlyException(
            $"An elective subject with the name '{name}' already exists."
            );
        }
    }

  
    private async Task ValidateSubjectIdsAsync(IEnumerable<Guid> subjectIds)
    {
        // If no subject IDs are provided, nothing to validate.
        if (subjectIds == null || !subjectIds.Any())
        {
            return;
        }

        // Remove duplicate IDs for efficient validation.
        var distinctIds = subjectIds.Distinct().ToList();

        // Count how many of the provided IDs exist in the repository.
        var existingSubjectsCount = await subjectRepository.CountAsync(s =>
        distinctIds.Contains(s.Id)
        );

        // If the count does not match, at least one ID is invalid.
        if (existingSubjectsCount != distinctIds.Count)
        {
            throw new UserFriendlyException(
            "One or more provided subject IDs are invalid or do not belong to the specified education medium."
            );
        }
    }
}