using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Students;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.ClassManagement.StudentElectiveAssignment;

[RemoteService(false)]
public class StudentElectiveCorrectionReader(
	IRepository<Student, Guid> students, IRepository<ElectiveGroup, Guid> groups,
	IRepository<Subject, Guid> subjects, IRepository<StudentElectiveSelection, Guid> selections,
	StudentElectiveCorrectionImpactReader impacts, IUserScopeService scopes) : ApplicationService
{
	internal async Task<PreparedElectiveCorrection> PrepareAsync(StudentElectiveCorrectionInput input)
	{
		if (input.StudentId == Guid.Empty || input.ClassId == Guid.Empty || input.DivisionId == Guid.Empty ||
			input.AcademicMediumId == Guid.Empty || input.ElectiveGroupId == Guid.Empty || input.SelectedSubjectId == Guid.Empty ||
			input.PreferredSubjectId == Guid.Empty || input.Reason?.Length > 1000) throw Invalid();
		var student = await students.FindAsync(x => x.TenantId == CurrentTenant.Id && x.Id == input.StudentId &&
			x.StandardId == input.ClassId && x.DivisionId == input.DivisionId && x.AcademicMediumId == input.AcademicMediumId);
		var group = await groups.FindAsync(x => x.TenantId == CurrentTenant.Id && x.Id == input.ElectiveGroupId && x.AcademicMediumId == input.AcademicMediumId, includeDetails: true);
		var selection = await selections.FindAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == input.StudentId &&
			x.ElectiveGroupId == input.ElectiveGroupId && x.AcademicMediumId == input.AcademicMediumId);
		if (student == null || group == null || selection == null) throw Invalid();
		if (!CurrentUser.Id.HasValue || !UserScopeQueryExtensions.HasAccessToEntity(student, await scopes.GetUserScopeAsync(CurrentUser.Id.Value)))
			throw new AbpAuthorizationException();
		if (!selection.IsLocked) throw new BusinessException("ClassManagement:CorrectionRequiresLockedSelection");
		var ids = new[] { selection.SelectedSubjectId, input.SelectedSubjectId, input.PreferredSubjectId ?? input.SelectedSubjectId };
		var options = await subjects.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(ids, x.Id));
		Subject? ValidSubject(Guid id) => options.SingleOrDefault(x => x.Id == id && x.EducationMediumId == input.AcademicMediumId &&
			x.Status == SubjectMasterStatus.Active && group.Subjects.Any(member => member.SubjectId == id));
		if (group.Status != SubjectMasterStatus.Active || ValidSubject(input.SelectedSubjectId) == null ||
			input.PreferredSubjectId.HasValue && ValidSubject(input.PreferredSubjectId.Value) == null) throw Invalid();
		var preferenceName = input.PreferredSubjectId.HasValue ? ValidSubject(input.PreferredSubjectId.Value)!.Name : selection.StudentPreference;
		if (selection.SelectedSubjectId == input.SelectedSubjectId &&
			(!input.PreferredSubjectId.HasValue || preferenceName == selection.StudentPreference)) throw new BusinessException("ClassManagement:ElectiveCorrectionNoChanges");
		var review = new StudentElectiveCorrectionReviewDto
		{
			StudentName = string.Join(" ", new[] { student.FirstName, student.MiddleName, student.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))),
			PreviousSubject = options.SingleOrDefault(x => x.Id == selection.SelectedSubjectId)?.Name ?? "Unavailable subject",
			SelectedSubject = ValidSubject(input.SelectedSubjectId)!.Name,
			PreviousPreference = selection.StudentPreference, PreferredSubject = preferenceName
		};
		var evidence = await impacts.ReadAsync(input, selection, review.Impacts);
		return new(selection, review, JsonSerializer.Serialize(new { group.ConcurrencyStamp,
			Subjects = options.OrderBy(x => x.Id).Select(x => new { x.Id, x.Name, x.Status, ModifiedTicks = x.LastModificationTime?.Ticks }), evidence }));
	}
	private static BusinessException Invalid() => new("ClassManagement:InvalidElectiveCorrection");
}

internal sealed record PreparedElectiveCorrection(StudentElectiveSelection Selection, StudentElectiveCorrectionReviewDto Review, string StateFingerprint);
