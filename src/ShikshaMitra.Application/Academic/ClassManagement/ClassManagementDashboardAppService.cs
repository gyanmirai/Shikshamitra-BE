using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.ClassManagement.Dashboard;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using ChapterProgressEntity = ShikshaMitra.Academic.Syllabus.ChapterProgress;

namespace ShikshaMitra.Academic.ClassManagement;

[Authorize(ShikshaMitraSubmodulePermissions.ClassManagement.Dashboard.View)]
[RemoteService(IsEnabled = false)]
public class ClassManagementDashboardAppService(
	IRepository<Student, Guid> studentRepository,
	IRepository<Employee, Guid> employeeRepository,
	IRepository<Standard, Guid> standardRepository,
	IRepository<Division, Guid> divisionRepository,
	IRepository<ClassTeacher.ClassTeacher, Guid> classTeacherRepository,
	IRepository<TeacherSubjectAllocation, Guid> teacherAllocationRepository,
	IRepository<AssignedSubject, Guid> assignedSubjectRepository,
	IRepository<StudentElectiveSelection, Guid> electiveSelectionRepository,
	IRepository<ChapterProgressEntity, Guid> chapterProgressRepository,
	IRepository<EducationMediumStage, Guid> educationMediumStageRepository
) : ApplicationService, IClassManagementDashboardAppService
{
	public async Task<ClassManagementDashboardDto> GetDashboardAsync(Guid academicMediumId, string? role = null)
	{
		if (academicMediumId == Guid.Empty)
		{
			throw new UserFriendlyException("Academic medium is required to load class management dashboard.");
		}

		var normalizedRole = NormalizeRole(role);

		var mediumStages = await educationMediumStageRepository.GetListAsync(s => s.EducationMediumId == academicMediumId);
		var mediumStageIds = mediumStages.Select(s => s.Id).ToHashSet();

		var standards = (await standardRepository.GetListAsync())
			.Where(s => mediumStageIds.Contains(s.EducationMediumStageId))
			.ToList();
		var standardIds = standards.Select(s => s.Id).ToHashSet();

		var divisions = (await divisionRepository.GetListAsync())
			.Where(d => standardIds.Contains(d.StandardId))
			.ToList();
		var divisionIds = divisions.Select(d => d.Id).ToHashSet();

		var students = await studentRepository.GetListAsync(s => s.AcademicMediumId == academicMediumId);
		var employees = await employeeRepository.GetListAsync();
		var classTeachers = (await classTeacherRepository.GetListAsync(ct =>
			ct.AcademicMediumId == academicMediumId &&
			ct.Status == ClassTeacherAssignmentStatus.Active))
			.Where(ct => standardIds.Contains(ct.StandardId) && divisionIds.Contains(ct.DivisionId))
			.ToList();
		var teacherAllocations = (await teacherAllocationRepository.GetListAsync(includeDetails: true))
			.Where(a => standardIds.Contains(a.StandardId) && divisionIds.Contains(a.DivisionId))
			.ToList();
		var assignedSubjects = (await assignedSubjectRepository.GetListAsync(includeDetails: true))
			.Where(a => standardIds.Contains(a.StandardId))
			.ToList();
		var electiveSelections = await electiveSelectionRepository.GetListAsync(s => s.AcademicMediumId == academicMediumId);
		var chapterProgress = (await chapterProgressRepository.GetListAsync())
			.Where(p => divisionIds.Contains(p.DivisionId))
			.ToList();

		var activeStudents = students.Where(s => s.DivisionId.HasValue && divisionIds.Contains(s.DivisionId.Value)).ToList();
		var scopedTeacherIds = classTeachers
			.Select(ct => ct.TeacherId)
			.Concat(teacherAllocations.SelectMany(a => a.Teachers.Select(t => t.TeacherId)))
			.ToHashSet();
		var teachers = employees
			.Where(e => e.EmployeeGroup == EmployeeGroupType.Teaching && scopedTeacherIds.Contains(e.Id))
			.ToList();
		var teacherNames = teachers.ToDictionary(e => e.Id, e => string.IsNullOrWhiteSpace(e.EmployeeName) ? e.EmployeeCode : e.EmployeeName);

		var classTeacherKeys = classTeachers.Select(ct => (ct.StandardId, ct.DivisionId)).ToHashSet();
		var missingClassTeachers = divisions.Count(d => !classTeacherKeys.Contains((d.StandardId, d.Id)));

		var subjectSlots = divisions.Sum(d => assignedSubjects.Count(a => a.StandardId == d.StandardId));
		var allocatedSubjectSlots = teacherAllocations.Count(a => a.Teachers.Any());
		var missingSubjectTeachers = Math.Max(0, subjectSlots - allocatedSubjectSlots);

		var averageProgress = chapterProgress.Count > 0
			? ClampPercent((int)Math.Round(chapterProgress.Average(p => p.Progress)))
			: 0;

		var electiveUnlocked = electiveSelections.Count(s => !s.IsLocked);
		var capacityRisk = divisions.Count(d => CountStudents(activeStudents, d.Id) > d.Strength);
		var healthScore = CalculateHealthScore(missingClassTeachers, missingSubjectTeachers, capacityRisk, averageProgress, divisions.Count, subjectSlots);

		var dto = new ClassManagementDashboardDto
		{
			Role = normalizedRole,
			HealthScore = healthScore,
			HealthLabel = healthScore >= 85 ? "Excellent" : healthScore >= 70 ? "Stable" : healthScore >= 55 ? "Needs attention" : "Critical",
			Metrics = BuildMetrics(normalizedRole, standards, divisions, activeStudents, teachers, missingClassTeachers, missingSubjectTeachers, averageProgress, electiveUnlocked, capacityRisk),
			Alerts = BuildAlerts(missingClassTeachers, missingSubjectTeachers, capacityRisk, averageProgress, electiveUnlocked),
			Actions = BuildActions(normalizedRole, missingClassTeachers, missingSubjectTeachers, electiveUnlocked, capacityRisk, averageProgress),
			ProgressItems = BuildProgressItems(normalizedRole, assignedSubjects, chapterProgress),
			ClassRisks = BuildClassRisks(divisions, standards, activeStudents, classTeachers, teacherNames, chapterProgress),
			TeacherWorkload = BuildTeacherWorkload(teacherAllocations, teacherNames),
			ProgressTrend = BuildProgressTrend(averageProgress)
		};

		return dto;
	}

	private static string NormalizeRole(string? role)
	{
		if (string.Equals(role, "Principal", StringComparison.OrdinalIgnoreCase)) return "Principal";
		if (string.Equals(role, "Teacher", StringComparison.OrdinalIgnoreCase)) return "Teacher";
		return "Admin";
	}

	private static List<ClassManagementDashboardMetricDto> BuildMetrics(
		string role,
		List<Standard> standards,
		List<Division> divisions,
		List<Student> students,
		List<Employee> teachers,
		int missingClassTeachers,
		int missingSubjectTeachers,
		int averageProgress,
		int electiveUnlocked,
		int capacityRisk)
	{
		var common = new List<ClassManagementDashboardMetricDto>
		{
			Metric("classes", "Classes", standards.Count.ToString(), $"{divisions.Count} divisions", "info"),
			Metric("students", "Students", students.Count.ToString(), "Active class placements", "success"),
			Metric("course-progress", "Course progress", $"{averageProgress}%", "Average chapter completion", averageProgress >= 75 ? "success" : averageProgress >= 50 ? "warning" : "error"),
			Metric("alerts", "Open risks", (missingClassTeachers + missingSubjectTeachers + capacityRisk).ToString(), "Teacher, capacity and progress risks", missingClassTeachers + missingSubjectTeachers + capacityRisk > 0 ? "warning" : "success")
		};

		if (role == "Teacher")
		{
			return
			[
				Metric("my-classes", "My classes", Math.Max(1, standards.Count).ToString(), "Assigned teaching scope", "info"),
				Metric("my-subjects", "My subjects", Math.Max(1, teachers.Count > 0 ? missingSubjectTeachers + 1 : 0).ToString(), "Subject responsibilities", "info"),
				Metric("chapters", "Chapter progress", $"{averageProgress}%", "Keep progress updated", averageProgress >= 75 ? "success" : "warning"),
				Metric("pending", "Pending actions", (missingSubjectTeachers + electiveUnlocked).ToString(), "Updates and reviews", missingSubjectTeachers + electiveUnlocked > 0 ? "warning" : "success")
			];
		}

		if (role == "Principal")
		{
			common.Add(Metric("teachers", "Teachers", teachers.Count.ToString(), "Teaching staff", "info"));
			common.Add(Metric("capacity-risk", "Capacity risk", capacityRisk.ToString(), "Over strength divisions", capacityRisk > 0 ? "warning" : "success"));
			return common;
		}

		common.Add(Metric("teachers", "Teachers", teachers.Count.ToString(), "Teaching staff", "info"));
		common.Add(Metric("missing-class-teachers", "Class teacher gaps", missingClassTeachers.ToString(), "Divisions without class teacher", missingClassTeachers > 0 ? "error" : "success"));
		common.Add(Metric("missing-subject-teachers", "Subject teacher gaps", missingSubjectTeachers.ToString(), "Subject slots without teachers", missingSubjectTeachers > 0 ? "error" : "success"));
		common.Add(Metric("electives-unlocked", "Electives unlocked", electiveUnlocked.ToString(), "Selections still editable", electiveUnlocked > 0 ? "warning" : "success"));
		return common;
	}

	private static List<ClassManagementDashboardAlertDto> BuildAlerts(int missingClassTeachers, int missingSubjectTeachers, int capacityRisk, int averageProgress, int electiveUnlocked)
	{
		var alerts = new List<ClassManagementDashboardAlertDto>();

		if (missingClassTeachers > 0)
		{
			alerts.Add(Alert("class-teacher", "Class teacher assignment pending", $"{missingClassTeachers} division(s) do not have a class teacher.", "critical", "Staffing", "Assign", "/classManagement/assignClassTeacher"));
		}
		if (missingSubjectTeachers > 0)
		{
			alerts.Add(Alert("subject-teacher", "Subject teacher coverage gap", $"{missingSubjectTeachers} subject slot(s) need teacher allocation.", "critical", "Staffing", "Assign", "/classManagement/teacher"));
		}
		if (capacityRisk > 0)
		{
			alerts.Add(Alert("capacity", "Class capacity pressure", $"{capacityRisk} division(s) are above configured strength.", "warning", "Capacity", "Review", "/classManagement/reports"));
		}
		if (averageProgress < 60)
		{
			alerts.Add(Alert("progress", "Course progress below target", $"Average chapter completion is {averageProgress}%.", "warning", "Course progress", "Update", "/classManagement/courseProgress"));
		}
		if (electiveUnlocked > 0)
		{
			alerts.Add(Alert("elective", "Elective selections still unlocked", $"{electiveUnlocked} saved elective selection(s) are not locked.", "info", "Electives", "Review", "/classManagement/assign-elective-subject"));
		}

		if (alerts.Count == 0)
		{
			alerts.Add(Alert("healthy", "No critical class-management risks", "Assignments, capacity and progress are currently stable.", "info", "Health", "View reports", "/classManagement/reports"));
		}

		return alerts;
	}

	private static List<ClassManagementDashboardActionDto> BuildActions(string role, int missingClassTeachers, int missingSubjectTeachers, int electiveUnlocked, int capacityRisk, int averageProgress)
	{
		var actions = new List<ClassManagementDashboardActionDto>();

		if (role != "Teacher")
		{
			actions.Add(Action("assign-class-teacher", "Assign class teachers", "Close class ownership gaps.", missingClassTeachers, "high", "Open", "/classManagement/assignClassTeacher"));
			actions.Add(Action("assign-subject-teacher", "Assign subject teachers", "Complete subject coverage by division.", missingSubjectTeachers, "high", "Open", "/classManagement/teacher"));
			actions.Add(Action("lock-electives", "Lock elective selections", "Freeze finalized elective choices.", electiveUnlocked, "medium", "Review", "/classManagement/assign-elective-subject"));
		}

		actions.Add(Action("course-progress", "Update course progress", "Review chapters that are behind plan.", averageProgress < 75 ? 1 : 0, averageProgress < 60 ? "high" : "medium", "Update", "/classManagement/courseProgress"));
		actions.Add(Action("reports", "Generate dashboard report", "Export class health, capacity and teacher coverage.", Math.Max(0, capacityRisk), "low", "Generate", "/classManagement/reports"));

		return actions.Where(a => a.Count > 0 || a.Id == "course-progress" || a.Id == "reports").ToList();
	}

	private static List<ClassManagementDashboardProgressDto> BuildProgressItems(string role, List<AssignedSubject> assignedSubjects, List<ChapterProgressEntity> chapterProgress)
	{
		if (chapterProgress.Count == 0)
		{
			return
			[
				Progress("setup", "Course tracking", "No chapter progress is recorded yet", 0, 75, "behind"),
				Progress("coverage", "Subject coverage", $"{assignedSubjects.Count} assigned subject(s)", assignedSubjects.Count > 0 ? 70 : 0, 90, assignedSubjects.Count > 0 ? "on-track" : "behind")
			];
		}

		return chapterProgress
			.OrderBy(p => p.Progress)
			.Take(role == "Teacher" ? 5 : 6)
			.Select((p, index) => Progress(p.ChapterId.ToString(), $"Chapter {index + 1}", p.Remarks ?? "Progress recorded", ClampPercent(p.Progress), 80, p.Progress >= 80 ? "on-track" : p.Progress >= 50 ? "watch" : "behind"))
			.ToList();
	}

	private static List<ClassManagementDashboardClassRiskDto> BuildClassRisks(
		List<Division> divisions,
		List<Standard> standards,
		List<Student> students,
		List<ClassTeacher.ClassTeacher> classTeachers,
		Dictionary<Guid, string> teacherNames,
		List<ChapterProgressEntity> chapterProgress)
	{
		return divisions
			.Select(d =>
			{
				var standard = standards.FirstOrDefault(s => s.Id == d.StandardId);
				var studentCount = CountStudents(students, d.Id);
				var classTeacher = classTeachers.FirstOrDefault(ct => ct.DivisionId == d.Id);
				var progressItems = chapterProgress.Where(p => p.DivisionId == d.Id).ToList();
				var progress = progressItems.Count > 0 ? ClampPercent((int)Math.Round(progressItems.Average(p => p.Progress))) : 0;
				var isCapacityRisk = studentCount > d.Strength;
				var isOwnerRisk = classTeacher == null;
				var isProgressRisk = progressItems.Count > 0 && progress < 60;
				var level = isCapacityRisk || isOwnerRisk ? "high" : isProgressRisk ? "medium" : "low";
				var reason = isOwnerRisk ? "Class teacher missing" : isCapacityRisk ? "Above configured strength" : isProgressRisk ? "Course progress behind target" : "Stable";

				return new ClassManagementDashboardClassRiskDto
				{
					Id = d.Id.ToString(),
					ClassName = standard?.Name ?? "Class",
					DivisionName = d.Name,
					Students = studentCount,
					Capacity = d.Strength,
					CourseProgress = progress,
					ClassTeacher = classTeacher != null && teacherNames.TryGetValue(classTeacher.TeacherId, out var teacherName) ? teacherName : "Unassigned",
					RiskLevel = level,
					RiskReason = reason
				};
			})
			.OrderByDescending(r => r.RiskLevel == "high")
			.ThenByDescending(r => r.RiskLevel == "medium")
			.ThenBy(r => r.ClassName)
			.Take(8)
			.ToList();
	}

	private static List<ClassManagementDashboardWorkloadDto> BuildTeacherWorkload(List<TeacherSubjectAllocation> allocations, Dictionary<Guid, string> teacherNames)
	{
		var rows = allocations
			.SelectMany(a => a.Teachers.Select(t => new { t.TeacherId, a.StandardId, a.SubjectId }))
			.GroupBy(x => x.TeacherId)
			.Select(g =>
			{
				var classes = g.Select(x => x.StandardId).Distinct().Count();
				var subjects = g.Select(x => x.SubjectId).Distinct().Count();
				var score = Math.Min(100, classes * 15 + subjects * 10);
				return new ClassManagementDashboardWorkloadDto
				{
					Id = g.Key.ToString(),
					TeacherName = teacherNames.TryGetValue(g.Key, out var name) ? name : "Teacher",
					Classes = classes,
					Subjects = subjects,
					LoadScore = score,
					Status = score >= 80 ? "overloaded" : score <= 25 ? "underloaded" : "balanced"
				};
			})
			.OrderByDescending(x => x.LoadScore)
			.Take(6)
			.ToList();

		if (rows.Count == 0)
		{
			rows.Add(new ClassManagementDashboardWorkloadDto
			{
				Id = "empty",
				TeacherName = "No teacher allocations yet",
				Classes = 0,
				Subjects = 0,
				LoadScore = 0,
				Status = "underloaded"
			});
		}

		return rows;
	}

	private static ClassManagementDashboardTrendDto BuildProgressTrend(int averageProgress)
	{
		var actual = new List<int>
		{
			ClampPercent((int)Math.Round(averageProgress * 0.25)),
			ClampPercent((int)Math.Round(averageProgress * 0.45)),
			ClampPercent((int)Math.Round(averageProgress * 0.65)),
			ClampPercent((int)Math.Round(averageProgress * 0.82)),
			ClampPercent(averageProgress)
		};

		return new ClassManagementDashboardTrendDto
		{
			Labels = ["Week 1", "Week 2", "Week 3", "Week 4", "Now"],
			Planned = [20, 40, 60, 80, 100],
			Actual = actual
		};
	}

	private static int CalculateHealthScore(int missingClassTeachers, int missingSubjectTeachers, int capacityRisk, int averageProgress, int divisionCount, int subjectSlots)
	{
		var score = 100;
		if (divisionCount > 0)
		{
			score -= (int)Math.Round((double)missingClassTeachers / divisionCount * 30);
			score -= (int)Math.Round((double)capacityRisk / divisionCount * 20);
		}
		if (subjectSlots > 0)
		{
			score -= (int)Math.Round((double)missingSubjectTeachers / subjectSlots * 25);
		}
		score -= Math.Max(0, 75 - averageProgress) / 2;
		return Math.Clamp(score, 0, 100);
	}

	private static int CountStudents(List<Student> students, Guid divisionId) =>
		students.Count(s => s.DivisionId == divisionId);

	private static int ClampPercent(int value) => Math.Clamp(value, 0, 100);

	private static ClassManagementDashboardMetricDto Metric(string id, string label, string value, string helper, string severity) =>
		new() { Id = id, Label = label, Value = value, Helper = helper, Severity = severity };

	private static ClassManagementDashboardAlertDto Alert(string id, string title, string description, string severity, string category, string actionLabel, string? actionHref) =>
		new() { Id = id, Title = title, Description = description, Severity = severity, Category = category, ActionLabel = actionLabel, ActionHref = actionHref };

	private static ClassManagementDashboardActionDto Action(string id, string title, string description, int count, string priority, string actionLabel, string actionHref) =>
		new() { Id = id, Title = title, Description = description, Count = count, Priority = priority, ActionLabel = actionLabel, ActionHref = actionHref };

	private static ClassManagementDashboardProgressDto Progress(string id, string label, string subLabel, int value, int target, string status) =>
		new() { Id = id, Label = label, SubLabel = subLabel, Value = value, Target = target, Status = status };
}
