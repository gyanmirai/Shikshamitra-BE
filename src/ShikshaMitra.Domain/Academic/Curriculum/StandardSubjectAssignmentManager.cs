using ShikshaMitra.TenantSettings.AcademicYears;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Guids;

namespace ShikshaMitra.Academic.Curriculum;

public class StandardSubjectAssignmentManager(IRepository<StandardSubjectAssignment, Guid> assignmentRepo, IGuidGenerator guidGenerator) : DomainService
{
	public StandardSubjectAssignment AssignSubject(
		Guid subjectId,
		Guid standardId,
		string subjectCode,
		string? learningGoals,
		string? prerequisites,
		string? description,
		SubjectStatus status) => new StandardSubjectAssignment(
			guidGenerator.Create(),
			CurrentTenant.Id,
			subjectId,
			standardId,
			subjectCode,
			status,
			learningGoals,
			prerequisites,
			description
		);

	public async Task UpdateTeachersAsync(Guid assignmentId, Guid primaryId, List<Guid> secondaryIds)
	{
		var assignment = await assignmentRepo.GetAsync(assignmentId);
		assignment.SetPrimaryTeacher(primaryId);
		assignment.SetSecondaryTeachers(secondaryIds);
	}

	public async Task ChangeStatusAsync(Guid assignmentId, SubjectStatus status)
	{
		var assignment = await assignmentRepo.GetAsync(assignmentId);
		assignment.ChangeStatus(status);
	}
}
