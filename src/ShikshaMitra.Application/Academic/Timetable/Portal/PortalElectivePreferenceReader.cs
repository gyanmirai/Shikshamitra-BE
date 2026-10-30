using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Timetable.Timetables;
using ShikshaMitra.Students;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Academic.Timetable.Portal;

/// <summary>One whole-week approval policy; callers must resolve persisted portal ownership first.</summary>
[RemoteService(false)]
public class PortalElectivePreferenceReader(
	PortalStudentContextReader contexts, ITimetableRepository timetables,
	IRepository<ElectiveGroup, Guid> groups, IRepository<Subject, Guid> subjects,
	IRepository<AssignedSubject, Guid> publishedSubjects,
	IRepository<StudentElectiveSelection, Guid> selections,
	IRepository<StudentElectivePreferenceSubmission, Guid> submissions, IDataFilter filters) : ApplicationService
{
	internal async Task<PortalElectivePreferencesDto> ReadAsync(Student student, Guid academicYearId)
	{
		var placement = (await contexts.ReadAsync([student.Id], academicYearId))[student.Id];
		if (placement.Error != null) throw placement.Error;
		var context = placement.Context!;
		var result = new PortalElectivePreferencesDto { StudentId = student.Id, AcademicYearId = academicYearId,
			AcademicYearName = context.CurrentAcademicYear?.Name ?? "", Availability = context.Availability };
		if (context.Availability != "available") return result;
		var timetable = await timetables.FindByStandardAndDivisionAsync(context.Student.StandardId!.Value, context.Student.DivisionId!.Value);
		if (timetable?.Status != TimetableStatus.Published || timetable.TenantId != CurrentTenant.Id)
		{ result.Availability = "noPublishedTimetable"; return result; }
		var requiredIds = timetable.Slots.Where(x => x.ElectiveGroupId.HasValue).Select(x => x.ElectiveGroupId!.Value).Distinct().ToArray();
		if (requiredIds.Length == 0) { result.CanViewTimetable = true; return result; }
		var groupRows = await AsyncExecuter.ToListAsync((await groups.WithDetailsAsync(x => x.Subjects))
			.Where(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(requiredIds, x.Id)));
		var assignmentRows = await publishedSubjects.GetListAsync(x => x.TenantId == CurrentTenant.Id &&
			x.StandardId == context.Student.StandardId && x.Status == SubjectStatus.Published &&
			x.SubjectType == SubjectType.Elective && x.ElectiveGroupId.HasValue && Enumerable.Contains(requiredIds, x.ElectiveGroupId.Value));
		List<StudentElectiveSelection> selectionRows;
		using (filters.Disable<ISoftDelete>())
			selectionRows = await selections.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == student.Id && Enumerable.Contains(requiredIds, x.ElectiveGroupId));
		var receipts = await submissions.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == student.Id && x.AcademicYearId == academicYearId);
		var subjectIds = timetable.Slots.SelectMany(x => x.Subjects).Select(x => x.SubjectId)
			.Concat(selectionRows.Select(x => x.SelectedSubjectId)).Distinct().ToArray();
		var subjectRows = (await subjects.GetListAsync(x => x.TenantId == CurrentTenant.Id && Enumerable.Contains(subjectIds, x.Id))).ToDictionary(x => x.Id);
		foreach (var groupId in requiredIds)
		{
			var group = groupRows.SingleOrDefault(x => x.Id == groupId);
			var receipt = receipts.SingleOrDefault(x => x.ElectiveGroupId == groupId);
			var candidates = selectionRows.Where(x => x.ElectiveGroupId == groupId).ToList();
			var selection = candidates.SingleOrDefault(x => !x.IsDeleted);
			var slots = timetable.Slots.Where(x => x.ElectiveGroupId == groupId).ToList();
			var dto = new PortalElectivePreferenceGroupDto { ElectiveGroupId = groupId, Name = group?.Name ?? "Elective group",
				PreferredSubjectId = receipt?.PreferredSubjectId, PreferredSubjectName = receipt?.PreferredSubjectName ?? selection?.StudentPreference,
				SubmittedAt = receipt?.SubmittedAt, AssignedSubjectId = selection?.SelectedSubjectId,
				AssignedSubjectName = selection != null ? subjectRows.GetValueOrDefault(selection.SelectedSubjectId)?.Name : null };
			if (group?.Status == SubjectMasterStatus.Active && group.AcademicMediumId == student.AcademicMediumId)
				dto.Options = group.Subjects.Where(member => subjectRows.TryGetValue(member.SubjectId, out var subject) &&
					subject.Status == SubjectMasterStatus.Active && subject.EducationMediumId == student.AcademicMediumId &&
					assignmentRows.Any(x => x.ElectiveGroupId == groupId && x.SubjectId == member.SubjectId) &&
					slots.All(slot => slot.Subjects.Count(x => x.SubjectId == member.SubjectId) == 1))
					.Select(x => new PortalElectivePreferenceOptionDto { SubjectId = x.SubjectId, Name = subjectRows[x.SubjectId].Name })
					.OrderBy(x => x.Name).ThenBy(x => x.SubjectId).ToList();
			if (dto.Options.Count == 0 || selection != null && (selection.AcademicMediumId != student.AcademicMediumId ||
				!dto.Options.Any(x => x.SubjectId == selection.SelectedSubjectId)) ||
				selection == null && (receipt != null || candidates.Count != 0)) dto.Status = "schoolActionRequired";
			else if (selection != null) dto.Status = selection.IsLocked ? "approved" : "pendingApproval";
			else { dto.Status = "selectionRequired"; dto.CanSubmit = true; }
			result.Groups.Add(dto);
		}
		result.Groups = result.Groups.OrderBy(x => x.Name).ThenBy(x => x.ElectiveGroupId).ToList();
		result.Availability = result.Groups.Any(x => x.Status == "schoolActionRequired") ? "electiveSetupRequired" :
			result.Groups.Any(x => x.Status == "selectionRequired") ? "electivePreferenceRequired" :
			result.Groups.Any(x => x.Status == "pendingApproval") ? "electiveApprovalPending" : "available";
		result.CanViewTimetable = result.Availability == "available";
		result.Message = result.Availability switch {
			"electiveSetupRequired" => "Please contact your school to review your elective assignments.",
			"electivePreferenceRequired" => "Choose your elective preferences and wait for school approval to view your timetable.",
			"electiveApprovalPending" => "Your elective preferences are awaiting school approval.", _ => null };
		return result;
	}
}
