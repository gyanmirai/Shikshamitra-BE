using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.Documents;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using DocumentTypeEntity = ShikshaMitra.Students.StudentEnrollments.Settings.DocumentType;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
public class StudentDashboardPayloadBuilder(
	IStudentRepository studentRepository,
	IRepository<AttendanceEntry, Guid> attendanceEntryRepository,
	IAttendanceDayRepository attendanceDayRepository,
	IRepository<StudentDocument, Guid> studentDocumentRepository,
	IRepository<DocumentTypeEntity, Guid> documentTypeRepository)
	: ApplicationService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public async Task<string> BuildAsync(string dashboardKey, UserScopeContext scope)
	{
		if (!string.Equals(dashboardKey, StudentDashboardKeys.Reporting, StringComparison.OrdinalIgnoreCase))
		{
			throw new BusinessException(StudentDashboardErrorCodes.InvalidKey);
		}

		var now = Clock.Now;
		var payload = await BuildReportingDashboardAsync(scope, now);
		return JsonSerializer.Serialize(payload, JsonOptions);
	}

	private async Task<StudentReportingDashboardDto> BuildReportingDashboardAsync(UserScopeContext scope, DateTime now)
	{
		var rangeEnd = now.Date;
		var rangeStart = rangeEnd.AddDays(-29);
		var studentQuery = ApplyScope(await studentRepository.GetQueryableWithoutAutoIncludesAsync(), scope);

		var totalStudents = await AsyncExecuter.CountAsync(studentQuery);
		var enrolledStudents = await AsyncExecuter.CountAsync(studentQuery.Where(x => x.Status == StudentLifeCycleStatusEnum.Enrolled));

		var enrolledRows = await AsyncExecuter.ToListAsync(studentQuery
			.Where(x => x.Status == StudentLifeCycleStatusEnum.Enrolled)
			.Select(x => new StudentDashboardStudentRow(
				x.Id,
				x.AcademicYearId,
				x.StandardId,
				x.Standard != null ? x.Standard.Name : "Unassigned",
				x.DivisionId,
				x.Division != null ? x.Division.Name : "Unassigned",
				x.GrNo,
				x.PhotoBlobName)));
		var enrolledIds = enrolledRows.Select(x => x.Id).ToList();

		var attendanceRows = await GetAttendanceRowsAsync(studentQuery, rangeStart, rangeEnd);
		var attendancePercentage = CalculateAttendancePercentage(attendanceRows.Select(x => x.Status));
		var missingGr = enrolledRows.Count(x => !x.GrNo.HasValue);
		var missingPhotos = enrolledRows.Count(x => string.IsNullOrWhiteSpace(x.PhotoBlobName));
		var missingDocuments = await CountStudentsMissingRequiredDocumentsAsync(enrolledIds);
		var unmarkedAttendanceGroups = await CountUnmarkedAttendanceGroupsAsync(enrolledRows, rangeEnd);

		return new StudentReportingDashboardDto
		{
			GeneratedAt = now,
			RangeStart = rangeStart,
			RangeEnd = rangeEnd,
			Kpis =
			[
				Kpi("total-students", "Total students", totalStudents, "default", "All students visible in your scope.", "/student/profile"),
				Kpi("enrolled-students", "Enrolled", enrolledStudents, "success", "Active enrolled student records.", "/student/profile"),
				Kpi("attendance-rate", "30 day attendance", attendancePercentage, attendancePercentage >= 75 ? "success" : "warning", "Present percentage from marked attendance.", "/student/attendance/dashboard", $"{attendancePercentage}%")
			],
			Categories = BuildCategories(),
			EnrollmentTrend = await BuildEnrollmentTrendAsync(studentQuery, now),
			AttendanceTrend = BuildAttendanceTrend(attendanceRows, rangeEnd),
			ClassStrength = BuildClassStrength(enrolledRows),
			Alerts = BuildAlerts(missingGr, missingPhotos, missingDocuments, unmarkedAttendanceGroups),
			QuickLinks = BuildQuickLinks()
		};
	}

	private async Task<List<StudentDashboardAttendanceRow>> GetAttendanceRowsAsync(
		IQueryable<Student> studentQuery,
		DateTime rangeStart,
		DateTime rangeEnd)
	{
		var entries = await attendanceEntryRepository.GetQueryableAsync();
		var days = await attendanceDayRepository.GetQueryableAsync();

		return await AsyncExecuter.ToListAsync(
			from entry in entries
			join day in days on entry.AttendanceDayId equals day.Id
			join student in studentQuery on entry.StudentId equals student.Id
			where day.Date >= rangeStart &&
			      day.Date <= rangeEnd &&
			      (entry.Status == AttendanceStatus.Present || entry.Status == AttendanceStatus.Absent)
			select new StudentDashboardAttendanceRow(day.Date, entry.Status));
	}

	private async Task<int> CountStudentsMissingRequiredDocumentsAsync(List<Guid> enrolledIds)
	{
		if (enrolledIds.Count == 0)
		{
			return 0;
		}

		var requiredDocumentTypeIds = await AsyncExecuter.ToListAsync(
			(await documentTypeRepository.GetQueryableAsync())
				.Where(x => x.IsRequired)
				.Select(x => x.Id));
		if (requiredDocumentTypeIds.Count == 0)
		{
			return 0;
		}

		var uploadedRows = await AsyncExecuter.ToListAsync(
			(await studentDocumentRepository.GetQueryableAsync())
				.Where(x =>
					enrolledIds.Contains(x.StudentId) &&
					requiredDocumentTypeIds.Contains(x.DocumentTypeId) &&
					x.StorageStatus == StudentDocumentStorageStatus.Stored)
				.Select(x => new { x.StudentId, x.DocumentTypeId }));

		var uploadedCounts = uploadedRows
			.Distinct()
			.GroupBy(x => x.StudentId)
			.ToDictionary(x => x.Key, x => x.Count());

		return enrolledIds.Count(studentId => uploadedCounts.GetValueOrDefault(studentId) < requiredDocumentTypeIds.Count);
	}

	private async Task<int> CountUnmarkedAttendanceGroupsAsync(List<StudentDashboardStudentRow> enrolledRows, DateTime date)
	{
		var expectedGroups = enrolledRows
			.Where(x => x.AcademicYearId.HasValue && x.StandardId.HasValue && x.DivisionId.HasValue)
			.Select(x => new AttendanceGroupKey(x.AcademicYearId!.Value, x.StandardId!.Value, x.DivisionId!.Value))
			.Distinct()
			.ToList();
		if (expectedGroups.Count == 0)
		{
			return 0;
		}

		var days = await AsyncExecuter.ToListAsync(
			(await attendanceDayRepository.GetQueryableAsync())
				.Where(x => x.Date == date)
				.Select(x => new AttendanceGroupKey(x.AcademicYearId, x.StandardId, x.DivisionId)));
		var markedGroups = days.ToHashSet();

		return expectedGroups.Count(x => !markedGroups.Contains(x));
	}

	private async Task<List<StudentReportingTrendPointDto>> BuildEnrollmentTrendAsync(
		IQueryable<Student> studentQuery,
		DateTime now)
	{
		var firstMonth = new DateTime(now.Year, now.Month, 1).AddMonths(-5);
		var rows = await AsyncExecuter.ToListAsync(studentQuery
			.Where(x => x.CreationTime >= firstMonth)
			.Select(x => new { x.CreationTime, x.Status }));

		return Enumerable.Range(0, 6)
			.Select(index => firstMonth.AddMonths(index))
			.Select(month =>
			{
				var monthRows = rows.Where(x => x.CreationTime.Year == month.Year && x.CreationTime.Month == month.Month).ToList();
				return new StudentReportingTrendPointDto
				{
					Label = month.ToString("MMM yy", CultureInfo.InvariantCulture),
					Inquiries = monthRows.Count(x => x.Status == StudentLifeCycleStatusEnum.Inquiry),
					Registrations = monthRows.Count(x => x.Status is StudentLifeCycleStatusEnum.Registration or StudentLifeCycleStatusEnum.RegistrationFormIssued or StudentLifeCycleStatusEnum.EntranceExam),
					Admissions = monthRows.Count(x => x.Status == StudentLifeCycleStatusEnum.Admission),
					Enrolled = monthRows.Count(x => x.Status == StudentLifeCycleStatusEnum.Enrolled)
				};
			})
			.ToList();
	}

	private static List<StudentReportingAttendanceTrendPointDto> BuildAttendanceTrend(
		IReadOnlyCollection<StudentDashboardAttendanceRow> rows,
		DateTime rangeEnd)
	{
		return Enumerable.Range(0, 7)
			.Select(offset => rangeEnd.AddDays(-6 + offset))
			.Select(day =>
			{
				var dayRows = rows.Where(x => x.Date == day.Date).ToList();
				var present = dayRows.Count(x => x.Status == AttendanceStatus.Present);
				var absent = dayRows.Count(x => x.Status == AttendanceStatus.Absent);
				return new StudentReportingAttendanceTrendPointDto
				{
					Date = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
					Present = present,
					Absent = absent,
					Percentage = CalculateAttendancePercentage(dayRows.Select(x => x.Status))
				};
			})
			.ToList();
	}

	private static List<StudentReportingClassStrengthDto> BuildClassStrength(
		IReadOnlyCollection<StudentDashboardStudentRow> rows)
	{
		return rows
			.GroupBy(x => new { x.StandardId, x.StandardName })
			.OrderBy(x => x.Key.StandardName)
			.Select(group => new StudentReportingClassStrengthDto
			{
				StandardId = group.Key.StandardId,
				Standard = group.Key.StandardName,
				Total = group.Count(),
				Divisions = group
					.GroupBy(x => new { x.DivisionId, x.DivisionName })
					.OrderBy(x => x.Key.DivisionName)
					.Select(division => new StudentReportingDivisionStrengthDto
					{
						DivisionId = division.Key.DivisionId,
						Division = division.Key.DivisionName,
						Total = division.Count()
					})
					.ToList()
			})
			.Take(12)
			.ToList();
	}

	private static List<StudentReportingAlertDto> BuildAlerts(
		int missingGr,
		int missingPhotos,
		int missingDocuments,
		int unmarkedAttendanceGroups)
	{
		var alerts = new List<StudentReportingAlertDto>
		{
			Alert("missing-gr", "warning", "Missing GR numbers", missingGr, "Enrolled students without GR numbers.", "Open GR generation", "/student/management?tab=grGeneration"),
			Alert("missing-photos", "info", "Missing student photos", missingPhotos, "Student profiles without uploaded photos.", "Upload photos", "/student/upload-student-photo"),
			Alert("missing-documents", "warning", "Required documents pending", missingDocuments, "Students missing one or more required documents.", "Upload documents", "/student/upload-student-documents"),
			Alert("attendance-unmarked", "critical", "Attendance not marked today", unmarkedAttendanceGroups, "Class/division groups without a saved attendance day.", "Take attendance", "/student/attendance/take-attendance")
		};

		return alerts.Where(x => x.Count > 0).ToList();
	}

	private static List<StudentReportingCategoryDto> BuildCategories() =>
	[
		new() { Id = "records", Title = "Records reports", Description = "Strength, lists, gender, age, religion, birthday, and contact reports.", AvailableReports = 19, Href = "/student/reports", Icon = "solar:documents-bold-duotone" },
		new() { Id = "attendance", Title = "Attendance reports", Description = "Daily, monthly, gender-wise, sheet, register, and absent-day reports.", AvailableReports = 8, Href = "/student/attendance/reports", Icon = "solar:calendar-mark-bold-duotone" },
		new() { Id = "certificates", Title = "Certificates and cards", Description = "Bonafide, leaving, character, first trial, and student I-card workflows.", AvailableReports = 5, Href = "/student/certificates", Icon = "solar:diploma-verified-bold-duotone" }
	];

	private static List<StudentReportingQuickLinkDto> BuildQuickLinks() =>
	[
		QuickLink("student-list", "Student list", "Search and inspect student records.", "/student/profile", "solar:users-group-rounded-bold"),
		QuickLink("gr-generation", "GR generation", "Assign or fix student GR numbers.", "/student/management?tab=grGeneration", "solar:hashtag-circle-bold"),
		QuickLink("roll-generation", "Roll generation", "Manage roll numbers by division.", "/student/management?tab=rollGeneration", "solar:sort-by-time-bold"),
		QuickLink("change-division", "Change division", "Move students between classes or divisions.", "/student/management?tab=changeDivision", "solar:transfer-horizontal-bold"),
		QuickLink("attendance-dashboard", "Attendance dashboard", "Review class/division attendance health.", "/student/attendance/dashboard", "solar:chart-2-bold")
	];

	private static StudentReportingKpiDto Kpi(
		string id,
		string label,
		decimal value,
		string tone,
		string description,
		string href,
		string? displayValue = null) =>
		new()
		{
			Id = id,
			Label = label,
			Value = value,
			DisplayValue = displayValue ?? value.ToString("0", CultureInfo.InvariantCulture),
			Tone = tone,
			Description = description,
			Href = href
		};

	private static StudentReportingAlertDto Alert(
		string id,
		string severity,
		string title,
		int count,
		string summary,
		string actionLabel,
		string actionHref) =>
		new()
		{
			Id = id,
			Severity = severity,
			Title = title,
			Count = count,
			Summary = summary,
			ActionLabel = actionLabel,
			ActionHref = actionHref
		};

	private static StudentReportingQuickLinkDto QuickLink(
		string id,
		string label,
		string description,
		string href,
		string icon) =>
		new()
		{
			Id = id,
			Label = label,
			Description = description,
			Href = href,
			Icon = icon
		};

	private static decimal CalculateAttendancePercentage(IEnumerable<AttendanceStatus> statuses)
	{
		var rows = statuses.ToList();
		var marked = rows.Count(status => status is AttendanceStatus.Present or AttendanceStatus.Absent);
		if (marked == 0)
		{
			return 0;
		}

		return Math.Round(rows.Count(status => status == AttendanceStatus.Present) * 100m / marked, 2);
	}

	private static IQueryable<Student> ApplyScope(IQueryable<Student> query, UserScopeContext scope)
	{
		if (scope.IsAdmin)
		{
			return query;
		}

		if (!scope.HasAnyAccess)
		{
			return query.Where(_ => false);
		}

		return query.Where(s =>
			scope.HasWildcardAcademicYear ||
			(s.AcademicYearId.HasValue &&
			 (scope.AcademicYearIds.Contains(s.AcademicYearId.Value) ||
			  scope.WildcardBoardAcademicYearIds.Contains(s.AcademicYearId.Value))) ||
			(s.DivisionId.HasValue &&
			 (scope.DivisionIds.Contains(s.DivisionId.Value) ||
			  (scope.HasWildcardDivision &&
			   s.StandardId.HasValue &&
			   scope.WildcardDivisionStandardIds.Contains(s.StandardId.Value)))) ||
			(s.StandardId.HasValue &&
			 (scope.StandardIds.Contains(s.StandardId.Value) ||
			  (scope.HasWildcardStandard &&
			   s.AcademicMediumStageId.HasValue &&
			   scope.WildcardStandardMediumStageIds.Contains(s.AcademicMediumStageId.Value)))) ||
			(s.AcademicMediumStageId.HasValue &&
			 (scope.MediumStageIds.Contains(s.AcademicMediumStageId.Value) ||
			  (scope.HasWildcardMediumStage &&
			   scope.WildcardMediumStageMediumIds.Contains(s.AcademicMediumId)))) ||
			scope.MediumIds.Contains(s.AcademicMediumId) ||
			(scope.HasWildcardMedium &&
			 s.AcademicBoardId.HasValue &&
			 scope.WildcardMediumBoardIds.Contains(s.AcademicBoardId.Value)) ||
			(s.AcademicBoardId.HasValue && scope.BoardIds.Contains(s.AcademicBoardId.Value)));
	}

	private sealed record StudentDashboardStudentRow(
		Guid Id,
		Guid? AcademicYearId,
		Guid? StandardId,
		string StandardName,
		Guid? DivisionId,
		string DivisionName,
		int? GrNo,
		string? PhotoBlobName);

	private sealed record StudentDashboardAttendanceRow(DateTime Date, AttendanceStatus Status);

	private sealed record AttendanceGroupKey(Guid AcademicYearId, Guid StandardId, Guid DivisionId);
}
