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
		await CheckNameIsUniqueAsync(name);
		await ValidateSubjectIdsAsync(subjectIds);

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

	public async Task<ElectiveGroup> UpdateAsync(
		Guid id,
		[NotNull] string name,
		[NotNull] string color,
		[CanBeNull] IEnumerable<Guid> subjectIds
	)
	{
		var ElectiveGroup = await ElectiveGroupRepository.GetAsync(id, includeDetails: true);

		if (ElectiveGroup.Name != name)
		{
			await CheckNameIsUniqueAsync(name, id);
			ElectiveGroup.SetName(name);
		}

		await ValidateSubjectIdsAsync(subjectIds);

		ElectiveGroup.SetColor(color);
		ElectiveGroup.ReplaceSubjects(subjectIds);

		return ElectiveGroup;
	}

	private async Task CheckNameIsUniqueAsync(string name, Guid? expectedId = null)
	{
		var existing = await ElectiveGroupRepository.FindAsync(es => es.Name == name);
		if (existing != null && existing.Id != expectedId)
		{
			throw new UserFriendlyException(
				$"An elective subject with the name '{name}' already exists."
			);
		}
	}

	private async Task ValidateSubjectIdsAsync(IEnumerable<Guid> subjectIds)
	{
		if (subjectIds == null || !subjectIds.Any())
		{
			return;
		}

		var distinctIds = subjectIds.Distinct().ToList();
		var existingSubjectsCount = await subjectRepository.CountAsync(s =>
			distinctIds.Contains(s.Id)
		);

		if (existingSubjectsCount != distinctIds.Count)
		{
			throw new UserFriendlyException(
				"One or more provided subject IDs are invalid or do not belong to the specified education medium."
			);
		}
	}
}
