using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Setup;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Identity;
using P = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Academics;

namespace ShikshaMitra.Academic.Dashboard;

[Authorize, RemoteService(false)]
public class AcademicInsightsAppService(AcademicSetupAccess identity, IPermissionChecker permissions,
	IIdentityUserRepository users, AcademicInsightsContextReader contexts, AcademicInsightsTeachingReader teaching,
	AcademicInsightsTaskReader tasks, AcademicInsightsStudentReader students, AcademicInsightsOperationalReader operations,
	AcademicInsightsPersonalReader personal) : ApplicationService, IAcademicInsightsAppService
{
	public async Task<AcademicInsightsDto> GetAsync(AcademicInsightsInput input)
	{
		Validator.ValidateObject(input, new ValidationContext(input), true);
		await identity.RequireIdentityAsync();
		var persistedRoles = (await users.GetRolesAsync(CurrentUser.Id!.Value)).Select(x => x.Name).ToArray();
		return await AcademicInsightsEntryAccess.ReadAsync(persistedRoles, await permissions.IsGrantedAsync(P.Dashboard.View),
			() => personal.ReadAsync(input), () => ReadStaffAsync(input, persistedRoles));
	}

	private async Task<AcademicInsightsDto> ReadStaffAsync(AcademicInsightsInput input, string[] persistedRoles)
	{
		var context = await contexts.ReadAsync(input);
		var roles = persistedRoles.Select(x => x.Replace(" ", "").Replace("-", "").ToLowerInvariant()).ToArray();
		var response = new AcademicInsightsDto { GeneratedAt = Clock.Now.ToUniversalTime(),
			Role = ResolveRole(roles), ScopeLabel = context.WholeMedium ? "Selected academic medium" : "Assigned classes and divisions" };
		if (await operations.IsClassTeacherAsync(context)) response.Role = response.Role is "teacher" or "common" ? "classTeacher" : response.Role;
		async Task Add(string key, string permission, Func<Task<AcademicInsightSectionDto>> read, params string[] siblings)
		{
			var section = await AcademicInsightsSectionAccess.ReadFeatureAsync(permissions, key, permission, siblings, read, Logger);
			if (section != null) response.Sections.Add(section);
		}
		await Add("classrooms", ShikshaMitraSubmodulePermissions.ClassManagement.ClassTeacherAssignment.View, () => teaching.ClassroomsAsync(context),
			ShikshaMitraSubmodulePermissions.ClassManagement.StudentElectiveAssignment.View, ShikshaMitraSubmodulePermissions.ClassManagement.TeachersAssignment.View,
			ShikshaMitraSubmodulePermissions.ClassManagement.ChaptersProgress.View);
		await Add("student360", P.Dashboard.View, () => students.ReadAsync(context));
		await Add("calendar", P.AcademicCalendar_CalendarManagement.View, () => operations.CalendarAsync(context), P.AcademicCalendar_Dashboard.View,
			P.AcademicCalendar_EmergencyClosure.View, P.AcademicCalendar_ImpactLogs.View, P.AcademicCalendar_AuditLogs.View, P.AcademicCalendar_Settings.View);
		await Add("curriculum", P.CurriculumManagement_ChapterManagement.View, () => teaching.CurriculumAsync(context),
			P.CurriculumManagement_SubjectAssignment.View, P.CurriculumManagement_ElectiveAssignment.View, P.CurriculumManagement_Resources.View);
		await Add("lesson", P.CurriculumManagement_LessonPlan.View, () => teaching.LessonsAsync(context));
		await Add("timetable", P.Timetable_TimetableManagement.View, () => teaching.TimetableAsync(context));
		await Add("tasks", P.HomeworkAndAssignment_LearningTasks.View, () => tasks.TasksAsync(context));
		await Add("evaluation", P.HomeworkAndAssignment_AssessmentAndFeedback.View, () => tasks.EvaluationAsync(context));
		await Add("exams", P.Examination_ScheduleExam.View, () => teaching.ExamsAsync(context),
			P.Examination_SeatAllocation.View, P.Examination_RecordMarks.View, P.Examination_ResultCreation.View);
		var reportSection = await AcademicInsightsSectionAccess.SafeReadAsync("reports", () => operations.ReportsAsync(context), Logger);
		if (reportSection != null) response.Sections.Add(reportSection);
		await Add("notifications", P.Notification.View, () => Task.FromResult(new AcademicInsightSectionDto { Key = "notifications", Status = "ready",
			Summary = "Read-only template library. Saving and automatic sending are unavailable.",
			Items = [new("curriculum", "Curriculum", "Read-only template library; saving and automatic sending unavailable"),
				new("timetable", "Timetable", "Read-only template library; saving and automatic sending unavailable"),
				new("homework-assignment", "Homework and assignment", "Read-only template library; saving and automatic sending unavailable"),
				new("examination", "Examination", "Read-only template library; saving and automatic sending unavailable")] }));
		await Add("audits", P.Audits.View, () => operations.AuditsAsync(context));
		var settings = await AcademicInsightsSectionAccess.SafeReadAsync("settings", () => operations.SettingsAsync(context), Logger);
		if (settings != null) response.Sections.Add(settings);
		return response;
	}

	public static AcademicInsightSectionDto Unavailable(string key, string summary) => new() { Key = key, Status = "unavailable", Summary = summary };
	private static string ResolveRole(string[] roles)
	{
		(string Role, string[] Names)[] mapping = [("admin", ["admin", "administrator", "superadmin"]), ("director", ["director"]),
			("principal", ["principal"]), ("vice", ["viceprincipal"]), ("coordinator", ["academiccoordinator", "coordinator"]),
			("hod", ["hod", "headofdepartment"]), ("teacher", ["teacher", "classteacher"]),
			("exam", ["examcoordinator", "examinationcoordinator"]), ("timetable", ["timetablecoordinator"])];
		return mapping.FirstOrDefault(x => x.Names.Any(roles.Contains)).Role ?? "common";
	}
}
