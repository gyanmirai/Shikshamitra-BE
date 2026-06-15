using ShikshaMitra.Academic.Curriculum.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.Curriculum;

public class StandardSubjectAssignmentManager(
	IRepository<AssignedSubject, Guid> assignmentRepo,
	IRepository<Subject, Guid> subjectRepository,
	IGuidGenerator guidGenerator
) : DomainService
{
	public async Task<AssignedSubject> AssignSubjectAsync(
		Guid standardId,
		Guid subjectId,
		string subjectCode,
		SubjectStatus status,
		SubjectType subjectType = SubjectType.Core,
		string? learningGoals = null,
		string? prerequisites = null,
		string? description = null,
		string? subjectColor = null
	)
	{
		var alreadyExists = await assignmentRepo.AnyAsync(a =>
			a.StandardId == standardId && a.SubjectId == subjectId
		);

		if (alreadyExists)
			throw new BusinessException(CurriculumErrorCodes.DuplicateSubjectAssignment)
				.WithData("StandardId", standardId)
				.WithData("SubjectId", subjectId)
				.WithData("message", "This subject is already assigned to this class. Duplicate assignments are not allowed.");

		return new AssignedSubject(
			id: guidGenerator.Create(),
			tenantId: CurrentTenant.Id,
			standardId: standardId,
			subjectId: subjectId,
			subjectCode: subjectCode,
			status: status,
			subjectType: subjectType,
			learningGoals: learningGoals,
			prerequisites: prerequisites,
			description: description
		);
	}

	public async Task UpdateTeachersAsync(
		Guid assignmentId,
		Guid primaryId,
		List<Guid> secondaryIds
	)
	{
		var assignment = await assignmentRepo.GetAsync(assignmentId);
		assignment.SetPrimaryTeacher(primaryId);
		assignment.SetSecondaryTeachers(secondaryIds);
	}

	/// <summary>
	/// Validates that subject codes are unique within a standard
	/// </summary>
	public async Task ValidateSubjectCodesAsync(Guid standardId, IEnumerable<string> subjectCodes, Guid? excludeGroupId = null)
	{
		var query = await assignmentRepo.GetQueryableAsync();
		var existing = query.Where(a =>
			a.StandardId == standardId &&
			subjectCodes.Contains(a.SubjectCode));

		var duplicates = existing.Select(a => a.SubjectCode).ToList();

		if (duplicates.Any())
		{
			throw new BusinessException("SubjectAssignment:DuplicateSubjectCode")
				.WithData("DuplicateCodes", string.Join(", ", duplicates))
				.WithData("message", $"Subject code(s) '{string.Join(", ", duplicates)}' are already in use for this class. Please use unique subject codes.");
		}
	}

	/// <summary>
	/// Validates that all subjects belong to the specified education medium
	/// </summary>
	public async Task ValidateSubjectsBelongToMediumAsync(IEnumerable<Guid> subjectIds, Guid educationMediumId)
	{
		var subjects = await subjectRepository.GetListAsync(s => subjectIds.Contains(s.Id));

		var invalidSubjects = subjects.Where(s => s.EducationMediumId != educationMediumId).ToList();

		if (invalidSubjects.Any())
		{
			throw new BusinessException("SubjectAssignment:SubjectsNotInMedium")
				.WithData("InvalidSubjectIds", string.Join(", ", invalidSubjects.Select(s => s.Id)));
		}

		// Check if all subject IDs were found
		var foundIds = subjects.Select(s => s.Id).ToList();
		var missingIds = subjectIds.Where(id => !foundIds.Contains(id)).ToList();

		if (missingIds.Any())
		{
			throw new BusinessException("SubjectAssignment:SubjectsNotFound")
				.WithData("MissingSubjectIds", string.Join(", ", missingIds));
		}
	}
}
