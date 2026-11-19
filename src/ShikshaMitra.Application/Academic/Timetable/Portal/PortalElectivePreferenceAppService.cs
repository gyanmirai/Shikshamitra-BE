using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Students.Portal;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Academic.Timetable.Portal;

[Authorize]
[RemoteService(false)]
public class PortalElectivePreferenceAppService(StudentPortalAccessService access, PortalElectivePreferenceReader reader,
	IRepository<StudentElectivePreferenceSubmission, Guid> submissions, IPortalElectivePreferenceWriter writer,
	IUnitOfWorkManager units) : ApplicationService, IPortalElectivePreferenceAppService
{
	public async Task<PortalElectivePreferencesDto> GetAsync(Guid studentId, Guid academicYearId)
	{
		if (studentId == Guid.Empty || academicYearId == Guid.Empty) throw Invalid();
		var student = await access.ResolveStudentAsync(studentId);
		return await reader.ReadAsync(student, academicYearId);
	}

	public async Task<PortalElectivePreferencesDto> SubmitAsync(PortalElectivePreferenceSubmitInput input)
	{
		if (input.StudentId == Guid.Empty || input.AcademicYearId == Guid.Empty || input.ElectiveGroupId == Guid.Empty ||
			input.PreferredSubjectId == Guid.Empty || input.ClientRequestId == Guid.Empty) throw Invalid();
		// Own the transaction even for direct service callers; both mirrors commit together.
		using var unit = units.Begin(requiresNew: true, isTransactional: true);
		var student = await access.ResolveStudentAsync(input.StudentId);
		await writer.AcquireAsync(CurrentTenant.Id!.Value, student.Id, input.ClientRequestId);
		var state = await reader.ReadAsync(student, input.AcademicYearId);
		var receipts = await submissions.GetListAsync(x => x.TenantId == CurrentTenant.Id &&
			(x.ClientRequestId == input.ClientRequestId || x.StudentId == student.Id && x.AcademicYearId == input.AcademicYearId && x.ElectiveGroupId == input.ElectiveGroupId));
		if (receipts.Count != 0)
		{
			if (receipts.Count != 1 || receipts[0].StudentId != student.Id || receipts[0].AcademicYearId != input.AcademicYearId ||
				receipts[0].ElectiveGroupId != input.ElectiveGroupId || receipts[0].ClientRequestId != input.ClientRequestId ||
				receipts[0].PreferredSubjectId != input.PreferredSubjectId) throw new BusinessException(ElectivePreferenceErrorCodes.Conflict);
			await unit.CompleteAsync();
			return state;
		}
		var group = state.Groups.SingleOrDefault(x => x.ElectiveGroupId == input.ElectiveGroupId);
		if (group == null) throw Invalid();
		if (group.Status == "schoolActionRequired") { await unit.CompleteAsync(); return state; }
		if (!group.CanSubmit) throw new BusinessException(ElectivePreferenceErrorCodes.Conflict);
		var choice = group.Options.SingleOrDefault(x => x.SubjectId == input.PreferredSubjectId);
		if (choice == null) throw Invalid();
		await writer.InsertAsync(new StudentElectivePreferenceSubmission(GuidGenerator.Create(), CurrentTenant.Id!.Value,
			student.Id, input.AcademicYearId, group.ElectiveGroupId, choice.SubjectId, choice.Name, CurrentUser.Id!.Value, Clock.Now, input.ClientRequestId),
			new StudentElectiveSelection(GuidGenerator.Create(), CurrentTenant.Id, student.Id, group.ElectiveGroupId,
				choice.SubjectId, student.AcademicMediumId, choice.Name));
		state = await reader.ReadAsync(student, input.AcademicYearId);
		await unit.CompleteAsync();
		return state;
	}
	private static BusinessException Invalid() => new(ElectivePreferenceErrorCodes.InvalidRequest);
}
