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

public class ElectiveSubjectManager : DomainService
{
	private readonly IRepository<ElectiveSubject, Guid> electiveSubjectRepository;
	private readonly IReadOnlyRepository<Subject, Guid> subjectRepository;
	private readonly IGuidGenerator guidGenerator;

	public ElectiveSubjectManager(
		IRepository<ElectiveSubject, Guid> electiveSubjectRepository,
		IReadOnlyRepository<Subject, Guid> subjectRepository,
		IGuidGenerator guidGenerator
	)
	{
		this.electiveSubjectRepository = electiveSubjectRepository;
		this.subjectRepository = subjectRepository;
		this.guidGenerator = guidGenerator;
	}

	public async Task<ElectiveSubject> CreateAsync(
		[NotNull] string name,
		[NotNull] string color,
		Guid educationMediumId,
		[CanBeNull] IEnumerable<Guid> subjectIds
	)
	{
		await CheckNameIsUniqueAsync(name, educationMediumId);
		await ValidateSubjectIdsAsync(subjectIds, educationMediumId);

		var electiveSubject = new ElectiveSubject(
			guidGenerator.Create(),
			CurrentTenant.Id,
			name,
			color,
			educationMediumId
		);

		electiveSubject.ReplaceSubjects(subjectIds);

		return electiveSubject;
	}

	public async Task<ElectiveSubject> UpdateAsync(
		Guid id,
		[NotNull] string name,
		[NotNull] string color,
		Guid educationMediumId,
		[CanBeNull] IEnumerable<Guid> subjectIds
	)
	{
		var electiveSubject = await electiveSubjectRepository.GetAsync(id, includeDetails: true);

		if (electiveSubject.Name != name || electiveSubject.EducationMediumId != educationMediumId)
		{
			await CheckNameIsUniqueAsync(name, educationMediumId, id);
			electiveSubject.SetName(name);
		}

		await ValidateSubjectIdsAsync(subjectIds, educationMediumId);

		electiveSubject.SetColor(color);
		electiveSubject.SetEducationMedium(educationMediumId);
		electiveSubject.ReplaceSubjects(subjectIds);

		return electiveSubject;
	}

	private async Task CheckNameIsUniqueAsync(
		string name,
		Guid educationMediumId,
		Guid? expectedId = null
	)
	{
		var existing = await electiveSubjectRepository.FindAsync(es =>
			es.Name == name && es.EducationMediumId == educationMediumId
		);
		if (existing != null && existing.Id != expectedId)
		{
			throw new UserFriendlyException(
				$"An elective subject with the name '{name}' already exists for this education medium."
			);
		}
	}

	private async Task ValidateSubjectIdsAsync(IEnumerable<Guid> subjectIds, Guid educationMediumId)
	{
		if (subjectIds == null || !subjectIds.Any())
		{
			return;
		}

		var distinctIds = subjectIds.Distinct().ToList();
		var existingSubjectsCount = await subjectRepository.CountAsync(s =>
			distinctIds.Contains(s.Id) && s.EducationMediumId == educationMediumId
		);

		if (existingSubjectsCount != distinctIds.Count)
		{
			throw new UserFriendlyException(
				"One or more provided subject IDs are invalid or do not belong to the specified education medium."
			);
		}
	}
}
