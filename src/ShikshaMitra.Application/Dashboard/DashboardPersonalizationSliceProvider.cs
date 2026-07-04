using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Academic.Examination.Results;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.Documents;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.Users;
using static ShikshaMitra.Dashboard.DashboardSummaryMutator;
using static ShikshaMitra.Dashboard.DashboardTemplates;
using static ShikshaMitra.Dashboard.DashboardWidgetCatalog;
using static ShikshaMitra.Dashboard.DashboardZones;
using ClassTeacherAssignment = ShikshaMitra.Academic.ClassTeacher.ClassTeacher;
using DocumentTypeEntity = ShikshaMitra.Students.StudentEnrollments.Settings.DocumentType;

namespace ShikshaMitra.Dashboard;

public sealed class DashboardTeacherScopeResolver(
	IRepository<Employee, Guid> employeeRepository,
	IAsyncQueryableExecuter asyncExecuter,
	ICurrentUser currentUser)
	: ITransientDependency
{
	public async Task<DashboardTeacherScope> ResolveAsync()
	{
		if (!currentUser.Id.HasValue)
		{
			return DashboardTeacherScope.Unresolved;
		}

		var employees = await employeeRepository.GetQueryableAsync();
		var employeeIds = await asyncExecuter.ToListAsync(employees
			.Where(employee =>
				employee.UserId == currentUser.Id.Value &&
				employee.EmployeeStatusType == EmployeeStatusType.Active)
			.Select(employee => employee.Id));

		return employeeIds.Count == 1
			? new DashboardTeacherScope(true, employeeIds[0])
			: DashboardTeacherScope.Unresolved;
	}
}

public sealed record DashboardTeacherScope(bool IsResolved, Guid? EmployeeId)
{
	public static DashboardTeacherScope Unresolved { get; } = new(false, null);
}

public sealed class DashboardStudentParentScopeResolver(
	IRepository<Student, Guid> studentRepository,
	IRepository<StudentContactDetail, Guid> contactDetailRepository,
	IRepository<StudentFamilyDetail, Guid> familyDetailRepository,
	IAsyncQueryableExecuter asyncExecuter,
	ICurrentUser currentUser)
	: ITransientDependency
{
	public async Task<DashboardStudentParentScope> ResolveAsync()
	{
		var email = NormalizeEmail(currentUser.Email);
		if (email == null)
		{
			return DashboardStudentParentScope.Unresolved;
		}

		var matchedStudentIds = await ResolveMatchedStudentIdsAsync(email);
		if (matchedStudentIds.Count == 0)
		{
			return DashboardStudentParentScope.Unresolved;
		}

		var students = await studentRepository.GetQueryableAsync();
		var enrolledStudentIds = await asyncExecuter.ToListAsync(students
			.Where(student =>
				matchedStudentIds.Contains(student.Id) &&
				student.Status == StudentLifeCycleStatusEnum.Enrolled)
			.Select(student => student.Id));

		var resolvedIds = enrolledStudentIds.Distinct().ToList();
		return resolvedIds.Count == 1
			? new DashboardStudentParentScope(true, resolvedIds[0])
			: DashboardStudentParentScope.Unresolved;
	}

	private async Task<List<Guid>> ResolveMatchedStudentIdsAsync(string normalizedEmail)
	{
		var contacts = await contactDetailRepository.GetQueryableAsync();
		var contactMatches = await asyncExecuter.ToListAsync(contacts
			.Where(contact =>
				contact.Email != null &&
				contact.Email.ToLower() == normalizedEmail)
			.Select(contact => contact.Id));

		var families = await familyDetailRepository.GetQueryableAsync();
		var familyMatches = await asyncExecuter.ToListAsync(families
			.Where(family =>
				(family.FatherDetails != null &&
				 family.FatherDetails.Email != null &&
				 family.FatherDetails.Email.ToLower() == normalizedEmail) ||
				(family.MotherDetails != null &&
				 family.MotherDetails.Email != null &&
				 family.MotherDetails.Email.ToLower() == normalizedEmail) ||
				(family.GuardianDetails != null &&
				 family.GuardianDetails.Email != null &&
				 family.GuardianDetails.Email.ToLower() == normalizedEmail))
			.Select(family => family.Id));

		return contactMatches
			.Concat(familyMatches)
			.Distinct()
			.ToList();
	}

	private static string? NormalizeEmail(string? email) =>
		string.IsNullOrWhiteSpace(email)
			? null
			: email.Trim().ToLowerInvariant();
}

public sealed record DashboardStudentParentScope(bool IsResolved, Guid? StudentId)
{
	public static DashboardStudentParentScope Unresolved { get; } = new(false, null);
}

public class DashboardPersonalizationSliceProvider(
	DashboardTeacherScopeResolver teacherScopeResolver,
	DashboardStudentParentScopeResolver studentParentScopeResolver,
	IRepository<ClassTeacherAssignment, Guid> classTeacherRepository,
	IAttendanceDayRepository attendanceDayRepository,
	IAttendanceEntryRepository attendanceEntryRepository,
	IDueFeeRepository dueFeeRepository,
	IRepository<StudentDocument, Guid> studentDocumentRepository,
	IRepository<DocumentTypeEntity, Guid> documentTypeRepository,
	IRepository<StudentSubmissionEntity, Guid> studentSubmissionRepository,
	IRepository<ExamResultHeader, Guid> resultHeaderRepository,
	IRepository<ExamResultDetail, Guid> resultDetailRepository,
	IUserScopeAccessService userScopeAccessService,
	IAsyncQueryableExecuter asyncExecuter,
	IPermissionChecker permissionChecker)
	: IDashboardSummarySliceProvider, ITransientDependency
{
	private static readonly SubmissionStatus[] StudentOpenLearningTaskStatuses =
	[
		SubmissionStatus.NotSubmitted,
		SubmissionStatus.Submitted,
		SubmissionStatus.Late,
		SubmissionStatus.Draft,
		SubmissionStatus.NeedsReview
	];

	public async Task ApplyAsync(DashboardSummarySliceContext context)
	{
		if (context.Summary.DashboardTemplate == Teacher)
		{
			await ApplyTeacherPersonalizationAsync(context.Summary, context.DateRange);
			return;
		}

		if (context.Summary.DashboardTemplate == StudentParent)
		{
			await ApplyStudentParentPersonalizationAsync(context);
		}
	}

	private async Task ApplyTeacherPersonalizationAsync(
		DashboardSummaryDto summary,
		DashboardDateRangeDto dateRange)
	{
		try
		{
			var scope = await teacherScopeResolver.ResolveAsync();
			if (!scope.IsResolved || !scope.EmployeeId.HasValue)
			{
				return;
			}

			var assignedClasses = await GetScopedClassTeacherAssignmentsAsync(scope.EmployeeId.Value);
			var classCount = assignedClasses.Count;
			await ApplyTeacherClassCountAsync(summary, classCount);
			await ApplyTeacherStudentRiskContextAsync(summary, classCount);
			await ApplyTeacherClassAttendanceActionAsync(summary, dateRange, assignedClasses);
			await ApplyTeacherClassPerformanceAsync(summary, assignedClasses);
		}
		catch
		{
			SetZoneError(summary, Kpis, "Teacher personalization could not be loaded.");
		}
	}

	private async Task<List<TeacherClassAssignmentRow>> GetScopedClassTeacherAssignmentsAsync(Guid teacherId)
	{
		var query = await classTeacherRepository.GetQueryableAsync();
		var scopedQuery = await userScopeAccessService.ApplyCurrentUserScopeAsync(query);
		var assignments = await asyncExecuter.ToListAsync(scopedQuery
			.Where(assignment => assignment.TeacherId == teacherId)
			.Select(assignment => new
			{
				assignment.StandardId,
				assignment.DivisionId
			}));

		return assignments
			.DistinctBy(assignment => new { assignment.StandardId, assignment.DivisionId })
			.Select(assignment => new TeacherClassAssignmentRow(assignment.StandardId, assignment.DivisionId))
			.ToList();
	}

	private async Task ApplyTeacherClassCountAsync(DashboardSummaryDto summary, int classCount)
	{
		if (!await HasAnyPermissionAsync(WidgetDefinitions["teacher-classes"].RequiredPermissions))
		{
			return;
		}

		UpdateWidget(
			summary.KpiCards,
			"teacher-classes",
			null,
			classCount.ToString(CultureInfo.InvariantCulture),
			classCount == 0
				? "No scoped class-teacher assignment found"
				: BuildCountSubtitle(classCount, "assigned class in scope", "assigned classes in scope"),
			classCount == 0 ? "info" : "success",
			title: "Assigned Classes");
	}

	private async Task ApplyTeacherStudentRiskContextAsync(DashboardSummaryDto summary, int classCount)
	{
		if (!await HasAnyPermissionAsync(WidgetDefinitions["teacher-student-risk"].RequiredPermissions))
		{
			return;
		}

		UpdateWidget(
			summary.RiskAlerts,
			"teacher-student-risk",
			null,
			null,
			classCount == 0
				? "No assigned class scope found for student follow-up."
				: $"Review scoped student records across {classCount} assigned classes.",
			classCount == 0 ? "info" : "warning");
	}

	private async Task ApplyTeacherClassAttendanceActionAsync(
		DashboardSummaryDto summary,
		DashboardDateRangeDto dateRange,
		IReadOnlyCollection<TeacherClassAssignmentRow> assignedClasses)
	{
		if (!await HasAnyPermissionAsync(WidgetDefinitions["teacher-attendance"].RequiredPermissions))
		{
			return;
		}

		if (assignedClasses.Count == 0)
		{
			UpdateWidget(
				summary.ActionItems,
				"teacher-attendance",
				0,
				null,
				"No assigned class scope found for attendance marking",
				"info");
			return;
		}

		var attendanceDate = ParseNormalizedDate(dateRange.To);
		var assignedDivisionIds = assignedClasses
			.Select(assignment => assignment.DivisionId)
			.Distinct()
			.ToList();
		var attendanceDays = await attendanceDayRepository.GetQueryableAsync();
		var markedDivisionIds = await asyncExecuter.ToListAsync(attendanceDays
			.Where(day =>
				day.Date == attendanceDate &&
				assignedDivisionIds.Contains(day.DivisionId))
			.Select(day => day.DivisionId));
		var missingCount = assignedDivisionIds
			.Except(markedDivisionIds.Distinct())
			.Count();

		UpdateWidget(
			summary.ActionItems,
			"teacher-attendance",
			missingCount,
			null,
			missingCount == 0
				? "Attendance marked for assigned classes"
				: BuildCountSubtitle(missingCount, "assigned class attendance not marked", "assigned classes attendance not marked"),
			missingCount == 0 ? "success" : "warning");
	}

	private async Task ApplyTeacherClassPerformanceAsync(
		DashboardSummaryDto summary,
		IReadOnlyCollection<TeacherClassAssignmentRow> assignedClasses)
	{
		if (!await HasAnyPermissionAsync(WidgetDefinitions["teacher-class-performance"].RequiredPermissions) ||
		    !await HasAnyPermissionAsync([ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Academics.Examination_ResultCreation.View]))
		{
			return;
		}

		if (assignedClasses.Count == 0)
		{
			UpdateWidget(
				summary.ModuleSnapshots,
				"teacher-class-performance",
				0,
				"Open",
				"No assigned class scope found for performance summary",
				"info",
				health: "attention");
			return;
		}

		var snapshot = await BuildTeacherClassPerformanceSnapshotAsync(assignedClasses);
		if (snapshot.PublishedResultCount == 0)
		{
			UpdateWidget(
				summary.ModuleSnapshots,
				"teacher-class-performance",
				0,
				"Open",
				"No published result found for assigned classes",
				"info",
				health: "attention");
			return;
		}

		var status = snapshot.PassPercentage >= 75 ? "success" : "warning";
		UpdateWidget(
			summary.ModuleSnapshots,
			"teacher-class-performance",
			snapshot.FailedCount,
			$"{snapshot.PassPercentage}%",
			$"Published result pass rate across {snapshot.PublishedResultCount} assigned class {Plural(snapshot.PublishedResultCount, "result", "results")}; {snapshot.FailedCount} {Plural(snapshot.FailedCount, "student needs", "students need")} follow-up.",
			status,
			health: status == "success" ? "good" : "attention");
	}

	private async Task<TeacherClassPerformanceSnapshot> BuildTeacherClassPerformanceSnapshotAsync(
		IReadOnlyCollection<TeacherClassAssignmentRow> assignedClasses)
	{
		var assignedDivisionIds = assignedClasses
			.Select(assignment => assignment.DivisionId)
			.Distinct()
			.ToList();
		if (assignedDivisionIds.Count == 0)
		{
			return TeacherClassPerformanceSnapshot.Empty;
		}

		var headers = await resultHeaderRepository.GetQueryableAsync();
		var headerRows = await asyncExecuter.ToListAsync(headers
			.Where(header =>
				assignedDivisionIds.Contains(header.DivisionId) &&
				(header.Status == ExamResultStatus.Published || header.Status == ExamResultStatus.Frozen))
			.Select(header => new
			{
				header.Id,
				header.DivisionId
			}));
		if (headerRows.Count == 0)
		{
			return TeacherClassPerformanceSnapshot.Empty;
		}

		var headerIds = headerRows.Select(header => header.Id).Distinct().ToList();
		var detailRows = await asyncExecuter.ToListAsync((await resultDetailRepository.GetQueryableAsync())
			.Where(detail => headerIds.Contains(detail.ResultHeaderId))
			.Select(detail => detail.ResultStatus));
		var appeared = detailRows.Count(status => status != ExamStudentResultStatus.Absent);
		if (appeared == 0)
		{
			return new TeacherClassPerformanceSnapshot(headerIds.Count, 0, 0);
		}

		var passed = detailRows.Count(status =>
			status is ExamStudentResultStatus.Pass or ExamStudentResultStatus.Compartment);
		var failed = detailRows.Count(status => status == ExamStudentResultStatus.Fail);
		var passPercentage = (int)Math.Round(passed / (double)appeared * 100, MidpointRounding.AwayFromZero);
		return new TeacherClassPerformanceSnapshot(headerIds.Count, failed, passPercentage);
	}

	private async Task ApplyStudentParentPersonalizationAsync(DashboardSummarySliceContext context)
	{
		try
		{
			var scope = await studentParentScopeResolver.ResolveAsync();
			if (!scope.IsResolved || !scope.StudentId.HasValue)
			{
				return;
			}

			var studentId = scope.StudentId.Value;
			await ApplyStudentParentAttendanceAsync(context.Summary, context.DateRange, studentId);
			await ApplyStudentParentAssignmentsAsync(context.Summary, studentId);
			await ApplyStudentParentFeeStatusAsync(context.Summary, studentId);
			await ApplyStudentParentDocumentStatusAsync(context.Summary, studentId);
			await ApplyStudentParentAcademicSummaryAsync(context.Summary, studentId);
		}
		catch
		{
			SetZoneError(context.Summary, Kpis, "Student/Parent personalization could not be loaded.");
		}
	}

	private async Task ApplyStudentParentAttendanceAsync(
		DashboardSummaryDto summary,
		DashboardDateRangeDto dateRange,
		Guid studentId)
	{
		if (!await HasAnyPermissionAsync(WidgetDefinitions["student-attendance"].RequiredPermissions))
		{
			return;
		}

		var startDate = ParseNormalizedDate(dateRange.From);
		var endDate = ParseNormalizedDate(dateRange.To);
		var attendanceDays = await attendanceDayRepository.GetQueryableAsync();
		var attendanceEntries = await attendanceEntryRepository.GetQueryableAsync();
		var statuses = await asyncExecuter.ToListAsync(
			from entry in attendanceEntries
			join day in attendanceDays on entry.AttendanceDayId equals day.Id
			where entry.StudentId == studentId &&
			      day.Date >= startDate &&
			      day.Date <= endDate &&
			      (entry.Status == AttendanceStatus.Present || entry.Status == AttendanceStatus.Absent)
			select entry.Status);

		var markedDays = statuses.Count;
		var presentDays = statuses.Count(status => status == AttendanceStatus.Present);
		var percentage = markedDays == 0
			? 0d
			: Math.Round(presentDays * 100d / markedDays, 2);

		UpdateWidget(
			summary.KpiCards,
			"student-attendance",
			null,
			$"{percentage:0.##}%",
			markedDays == 0
				? "No personal attendance marked in selected range"
				: $"{presentDays}/{markedDays} marked attendance days present",
			percentage >= 90d ? "success" : percentage >= 75d ? "warning" : "error",
			title: "Attendance");
	}

	private async Task ApplyStudentParentAssignmentsAsync(DashboardSummaryDto summary, Guid studentId)
	{
		if (!await HasAnyPermissionAsync(WidgetDefinitions["student-assignments"].RequiredPermissions))
		{
			return;
		}

		var submissions = await studentSubmissionRepository.GetQueryableAsync();
		var openTaskRows = await asyncExecuter.ToListAsync(submissions
			.Where(submission =>
				submission.StudentId == studentId &&
				StudentOpenLearningTaskStatuses.Contains(submission.Status) &&
				submission.ReviewedBy == null &&
				submission.ReviewedAt == null &&
				submission.GradedBy == null &&
				submission.GradedDate == null)
			.Select(submission => submission.Status));

		var overdueCount = openTaskRows.Count(status => status == SubmissionStatus.Late);
		var count = openTaskRows.Count;

		UpdateWidget(
			summary.KpiCards,
			"student-assignments",
			count,
			count.ToString(CultureInfo.InvariantCulture),
			count == 0
				? "No open personal learning-task submissions found"
				: overdueCount == 0
					? BuildCountSubtitle(count, "personal learning task open or awaiting review", "personal learning tasks open or awaiting review")
					: $"{overdueCount} overdue of {count} personal learning tasks open or awaiting review",
			overdueCount > 0 ? "warning" : CountStatus(count),
			title: "Assignments Due");
	}

	private async Task ApplyStudentParentFeeStatusAsync(DashboardSummaryDto summary, Guid studentId)
	{
		if (!await HasAnyPermissionAsync(WidgetDefinitions["student-fee-status"].RequiredPermissions))
		{
			return;
		}

		var dueFees = await dueFeeRepository.GetQueryableAsync();
		var outstandingRows = await asyncExecuter.ToListAsync(dueFees
			.Where(dueFee =>
				dueFee.StudentId == studentId &&
				dueFee.OutstandingAmount > 0d)
			.Select(dueFee => dueFee.OutstandingAmount));
		var outstandingAmount = outstandingRows.Sum();

		UpdateWidget(
			summary.KpiCards,
			"student-fee-status",
			outstandingRows.Count,
			FormatIndianAmount(outstandingAmount),
			outstandingRows.Count == 0
				? "No outstanding dues found for linked student"
				: BuildCountSubtitle(outstandingRows.Count, "outstanding fee item", "outstanding fee items"),
			outstandingAmount <= 0d ? "success" : "warning",
			title: "Fee Status");
	}

	private async Task ApplyStudentParentDocumentStatusAsync(DashboardSummaryDto summary, Guid studentId)
	{
		if (!await HasAnyPermissionAsync(WidgetDefinitions["student-document-update"].RequiredPermissions))
		{
			return;
		}

		var documentTypes = await documentTypeRepository.GetQueryableAsync();
		var requiredTypeIds = await asyncExecuter.ToListAsync(documentTypes
			.Where(documentType => documentType.IsRequired)
			.Select(documentType => documentType.Id));
		if (requiredTypeIds.Count == 0)
		{
			UpdateWidget(
				summary.ActionItems,
				"student-document-update",
				0,
				null,
				"No required student documents configured",
				"success");
			return;
		}

		var documents = await studentDocumentRepository.GetQueryableAsync();
		var uploadedTypeIds = await asyncExecuter.ToListAsync(documents
			.Where(document =>
				document.StudentId == studentId &&
				requiredTypeIds.Contains(document.DocumentTypeId) &&
				document.StorageStatus == StudentDocumentStorageStatus.Stored)
			.Select(document => document.DocumentTypeId));
		var pendingCount = requiredTypeIds
			.Except(uploadedTypeIds.Distinct())
			.Count();

		UpdateWidget(
			summary.ActionItems,
			"student-document-update",
			pendingCount,
			null,
			pendingCount == 0
				? "All required student documents are uploaded"
				: BuildCountSubtitle(pendingCount, "required document pending", "required documents pending"),
			pendingCount == 0 ? "success" : "warning");
	}

	private async Task ApplyStudentParentAcademicSummaryAsync(DashboardSummaryDto summary, Guid studentId)
	{
		if (!await HasAnyPermissionAsync(WidgetDefinitions["student-academic-summary"].RequiredPermissions))
		{
			return;
		}

		var latestResult = await BuildLatestPublishedResultAsync(studentId);
		if (latestResult == null)
		{
			UpdateWidget(
				summary.ModuleSnapshots,
				"student-academic-summary",
				0,
				"View",
				"No published personal academic result found; open student dashboard for details",
				"info",
				health: "attention");
			return;
		}

		var value = latestResult.Status == ExamStudentResultStatus.Absent
			? "Absent"
			: $"{Math.Round(latestResult.Percentage, MidpointRounding.AwayFromZero):0}%";
		var status = latestResult.Status switch
		{
			ExamStudentResultStatus.Pass or ExamStudentResultStatus.Compartment => "success",
			ExamStudentResultStatus.Fail => "warning",
			ExamStudentResultStatus.Absent or ExamStudentResultStatus.Withheld => "warning",
			_ => "info"
		};

		UpdateWidget(
			summary.ModuleSnapshots,
			"student-academic-summary",
			1,
			value,
			BuildStudentAcademicSummarySubtitle(latestResult),
			status,
			health: status == "success" ? "good" : "attention");
	}

	private async Task<StudentResultSnapshot?> BuildLatestPublishedResultAsync(Guid studentId)
	{
		var headers = await resultHeaderRepository.GetQueryableAsync();
		var details = await resultDetailRepository.GetQueryableAsync();
		var rows = await asyncExecuter.ToListAsync(
			from detail in details
			join header in headers on detail.ResultHeaderId equals header.Id
			where detail.StudentId == studentId &&
			      (header.Status == ExamResultStatus.Published || header.Status == ExamResultStatus.Frozen)
			orderby header.FrozenOn ?? header.PublishedOn ?? header.GeneratedOn descending
			select new StudentResultSnapshot(
				detail.Percentage,
				detail.Grade,
				detail.ResultStatus,
				detail.ClassRank,
				detail.SectionRank));

		return rows.FirstOrDefault();
	}

	private static string BuildStudentAcademicSummarySubtitle(StudentResultSnapshot result)
	{
		var parts = new List<string>
		{
			$"Latest published result: {result.Status}"
		};

		if (!string.IsNullOrWhiteSpace(result.Grade))
		{
			parts.Add($"grade {result.Grade}");
		}

		if (result.SectionRank.HasValue)
		{
			parts.Add($"section rank {result.SectionRank.Value}");
		}
		else if (result.ClassRank.HasValue)
		{
			parts.Add($"class rank {result.ClassRank.Value}");
		}

		return string.Join(", ", parts);
	}

	private async Task<bool> HasAnyPermissionAsync(string[] permissionNames)
	{
		foreach (var permissionName in permissionNames)
		{
			if (await permissionChecker.IsGrantedAsync(permissionName))
			{
				return true;
			}
		}

		return false;
	}

	private sealed record StudentResultSnapshot(
		double Percentage,
		string Grade,
		ExamStudentResultStatus Status,
		int? ClassRank,
		int? SectionRank);

	private sealed record TeacherClassAssignmentRow(Guid StandardId, Guid DivisionId);

	private sealed record TeacherClassPerformanceSnapshot(
		int PublishedResultCount,
		int FailedCount,
		int PassPercentage)
	{
		public static TeacherClassPerformanceSnapshot Empty { get; } = new(0, 0, 0);
	}

	private static string Plural(int count, string singular, string plural) =>
		count == 1 ? singular : plural;
}
