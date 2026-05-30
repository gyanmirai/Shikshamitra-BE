using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.BackgroundJobs;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Validation;

namespace ShikshaMitra.HR.Dashboard;

[Authorize(ShikshaMitraSubmodulePermissions.HR.Dashboard.View)]
[RemoteService(false)]
public class HRDashboardAppService(
	IEmployeeRepository employeeRepository,
	IEmployeeAttendanceRepository attendanceRepository,
	IEmployeeLeaveRepository leaveRepository,
	IRepository<LeavePolicy, Guid> leavePolicyRepository,
	IRepository<HrDashboardPreview, Guid> previewRepository,
	IRunPayrollAppService runPayrollAppService,
	IBackgroundJobManager backgroundJobManager,
	IDataFilter dataFilter,
	IUserScopeService userScopeService,
	HrUserScopeAccessService hrScopeAccessService)
	: ApplicationService, IHRDashboardAppService
{
	private const int DefaultRangeDays = 7;
	private const int MaxRangeDays = 31;
	private const int PayloadVersion = 1;
	private static readonly TimeSpan RefreshStaleAfter = TimeSpan.FromMinutes(15);
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public async Task<HRDashboardSummaryDto> GetSummaryAsync(HRDashboardSummaryRequestDto input)
	{
		input ??= new HRDashboardSummaryRequestDto();
		var dashboardDate = ParseDateOrDefault(input.Date, DateTime.Today);
		var rangeDays = Math.Clamp(input.Range ?? DefaultRangeDays, 1, MaxRangeDays);
		var month = NormalizeMonth(input.Month, dashboardDate);
		var selectedMonthStart = DateTime.ParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture);
		var selectedNextMonthStart = selectedMonthStart.AddMonths(1);

		var employeeQuery = await GetScopedEmployeeQueryAsync();
		var attendanceQuery = await attendanceRepository.GetQueryableAsync();
		var leaveQuery = await leaveRepository.GetQueryableAsync();
		var leavePolicyQuery = await leavePolicyRepository.GetQueryableAsync();

		var activeEmployees = employeeQuery.Where(e => e.EmployeeStatusType == EmployeeStatusType.Active);
		var activeEmployeeRows = await AsyncExecuter.ToListAsync(activeEmployees
			.Select(e => new ActiveEmployeeDashboardRow(
				e.Id,
				e.EmployeeName,
				e.EmployeeCode,
				e.EmployeeGroup,
				e.DateOfJoining,
				e.DateOfBirth)));
		var activeEmployeeIds = activeEmployeeRows.Select(e => e.Id).ToList();
		var scopedEmployeeCountRows = await AsyncExecuter.ToListAsync(employeeQuery
			.GroupBy(e => e.EmployeeStatusType == EmployeeStatusType.Active ? "active" : "inactive")
			.Select(g => new { Status = g.Key, Count = g.Count() }));
		var scopedEmployeeCounts = scopedEmployeeCountRows.ToDictionary(x => x.Status, x => x.Count);

		var activeEmployeeStats = activeEmployeeRows
			.GroupBy(_ => 1)
			.Select(g => new
			{
				TotalActive = g.Count(),
				Teaching = g.Count(e => e.EmployeeGroup == EmployeeGroupType.Teaching),
				NonTeaching = g.Count(e => e.EmployeeGroup == EmployeeGroupType.NonTeaching),
				NewJoiners = g.Count(e =>
					e.DateOfJoining.HasValue &&
					e.DateOfJoining.Value >= DateOnly.FromDateTime(selectedMonthStart) &&
					e.DateOfJoining.Value < DateOnly.FromDateTime(selectedNextMonthStart))
			})
			.FirstOrDefault();
		var celebrationCandidates = activeEmployeeRows
			.Where(e => e.DateOfBirth.HasValue || e.DateOfJoining.HasValue)
			.ToList();

		var todayDateOnly = DateOnly.FromDateTime(dashboardDate);
		var trendStart = todayDateOnly.AddDays(-(rangeDays - 1));
		var trendEnd = todayDateOnly;
		var todayStartOffset = new DateTimeOffset(dashboardDate.Date, TimeSpan.Zero);
		var todayEndOffset = todayStartOffset.AddDays(1).AddTicks(-1);
		var selectedMonthStartOffset = new DateTimeOffset(selectedMonthStart, TimeSpan.Zero);
		var selectedMonthEndOffset = new DateTimeOffset(selectedNextMonthStart, TimeSpan.Zero).AddTicks(-1);

		var workforce = await BuildWorkforceAsync(
			activeEmployees,
			activeEmployeeStats?.TotalActive ?? 0,
			activeEmployeeStats?.Teaching ?? 0,
			activeEmployeeStats?.NonTeaching ?? 0,
			activeEmployeeStats?.NewJoiners ?? 0,
			scopedEmployeeCounts.GetValueOrDefault("inactive"));
		var attendanceCountRows = await AsyncExecuter.ToListAsync(
			attendanceQuery.Where(a =>
				a.AttendanceDate >= trendStart &&
				a.AttendanceDate <= trendEnd &&
				activeEmployeeIds.Contains(a.EmployeeId))
				.GroupBy(a => new { a.AttendanceDate, a.StatusType })
				.Select(g => new { g.Key.AttendanceDate, g.Key.StatusType, Count = g.Count() }));
		var attendanceCounts = attendanceCountRows
			.Select(x => new AttendanceStatusCount(x.AttendanceDate, x.StatusType, x.Count))
			.ToList();
		var leavesForTrend = await AsyncExecuter.ToListAsync(
			leaveQuery.Where(l =>
				l.Status == EmployeeLeaveStatusType.Approved &&
				l.StartDate <= todayEndOffset &&
				l.EndDate >= new DateTimeOffset(trendStart.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) &&
				activeEmployeeIds.Contains(l.EmployeeId))
				.Select(l => new LeaveTrendRow(l.EmployeeId, l.StartDate, l.EndDate)));

		var attendanceToday = BuildAttendanceToday(attendanceCounts, todayDateOnly, activeEmployeeIds.Count);
		var attendanceTrend = BuildAttendanceTrend(attendanceCounts, leavesForTrend, trendStart, trendEnd);
		var leave = await BuildLeaveAsync(
			leaveQuery,
			leavePolicyQuery,
			employeeQuery,
			activeEmployeeIds,
			selectedMonthStartOffset,
			selectedMonthEndOffset);
		var payroll = await BuildPayrollAsync(month);
		var celebrations = BuildCelebrations(celebrationCandidates, dashboardDate);
		var inactiveReportingManagerIssue = await BuildInactiveReportingManagerIssueAsync(employeeQuery);
		var insights = BuildInsights(attendanceToday, leave, payroll, celebrations, inactiveReportingManagerIssue);

		return new HRDashboardSummaryDto
		{
			Date = dashboardDate.Date,
			Month = month,
			Range = rangeDays,
			Workforce = workforce,
			AttendanceToday = attendanceToday,
			AttendanceTrend = attendanceTrend,
			Leave = leave,
			Payroll = payroll,
			Celebrations = celebrations,
			Insights = insights
		};
	}

	public async Task<HRDashboardPreviewDto> GetPreviewAsync(string dashboardKey)
	{
		dashboardKey = NormalizeDashboardKey(dashboardKey);
		await CheckDashboardPermissionAsync(dashboardKey);

		var preview = await FindPreviewAsync(dashboardKey);
		return preview == null ? EmptyPreview(dashboardKey) : ToPreviewDto(preview);
	}

	public async Task<HRDashboardPreviewDto> RefreshPreviewAsync(string dashboardKey)
	{
		dashboardKey = NormalizeDashboardKey(dashboardKey);
		await CheckDashboardPermissionAsync(dashboardKey);

		var now = Clock.Now;
		var preview = await FindPreviewAsync(dashboardKey);
		if (preview == null)
		{
			preview = new HrDashboardPreview(GuidGenerator.Create(), CurrentTenant.Id, dashboardKey);
			await previewRepository.InsertAsync(preview, autoSave: true);
		}

		var isAlreadyRefreshing = preview.Status == HrDashboardPreviewStatus.Refreshing &&
			preview.RefreshStartedAt.HasValue &&
			now - preview.RefreshStartedAt.Value < RefreshStaleAfter;
		if (!isAlreadyRefreshing)
		{
			preview.MarkRefreshing(CurrentUser.Id, now);
			await previewRepository.UpdateAsync(preview, autoSave: true);
			await backgroundJobManager.EnqueueAsync(new HrDashboardPreviewRefreshJobArgs
			{
				TenantId = CurrentTenant.Id,
				PreviewId = preview.Id,
				DashboardKey = dashboardKey,
				RequestedByUserId = CurrentUser.Id
			});
		}

		return ToPreviewDto(preview);
	}

	[AllowAnonymous]
	public async Task RefreshExistingPreviewsAsync()
	{
		List<HrDashboardPreview> previews;
		using (dataFilter.Disable<IMultiTenant>())
		{
			previews = await AsyncExecuter.ToListAsync((await previewRepository.GetQueryableAsync())
				.Where(x => x.Status != HrDashboardPreviewStatus.Refreshing ||
					!x.RefreshStartedAt.HasValue ||
					Clock.Now - x.RefreshStartedAt.Value >= RefreshStaleAfter));
		}

		foreach (var preview in previews)
		{
			await backgroundJobManager.EnqueueAsync(new HrDashboardPreviewRefreshJobArgs
			{
				TenantId = preview.TenantId,
				PreviewId = preview.Id,
				DashboardKey = preview.DashboardKey
			});
		}
	}

	[AllowAnonymous]
	public async Task ExecutePreviewRefreshAsync(HrDashboardPreviewRefreshJobArgs args)
	{
		var preview = await previewRepository.FindAsync(args.PreviewId);
		if (preview == null)
		{
			return;
		}

		try
		{
			var payloadJson = await BuildPreviewPayloadJsonAsync(preview.DashboardKey);
			preview.MarkReady(payloadJson, PayloadVersion, Clock.Now);
		}
		catch (Exception ex)
		{
			preview.MarkFailed(ex.Message, Clock.Now);
		}

		await previewRepository.UpdateAsync(preview, autoSave: true);
	}

	private async Task<string> BuildPreviewPayloadJsonAsync(string dashboardKey)
	{
		var defaults = new HRDashboardSummaryRequestDto
		{
			Date = Clock.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
			Month = Clock.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture),
			Range = 7
		};
		var summary = await GetSummaryAsync(defaults);

		return dashboardKey switch
		{
			HrDashboardKeys.Main => JsonSerializer.Serialize(summary, JsonOptions),
			HrDashboardKeys.Attendance => JsonSerializer.Serialize(BuildAttendancePreview(summary), JsonOptions),
			HrDashboardKeys.Payroll => JsonSerializer.Serialize(BuildPayrollPreview(summary), JsonOptions),
			_ => throw new BusinessException("HR:Dashboard:InvalidKey")
		};
	}

	private static HRDashboardAttendancePreviewDto BuildAttendancePreview(HRDashboardSummaryDto summary)
	{
		var totalStaff = summary.Workforce.TotalActiveStaff;
		var missing = summary.AttendanceToday.MissingAttendance;
		return new HRDashboardAttendancePreviewDto
		{
			Date = summary.Date,
			TotalStaff = totalStaff,
			Present = summary.AttendanceToday.Present,
			Absent = summary.AttendanceToday.Absent,
			HalfDay = summary.AttendanceToday.HalfDay,
			MissingAttendance = missing,
			AttendanceRate = summary.AttendanceToday.AttendanceRate,
			Trend = summary.AttendanceTrend,
			TopDefaulters = summary.Workforce.RecentEmployees
				.Take(7)
				.Select(e => new HRDashboardAttendanceDefaulterDto
				{
					EmployeeId = e.Id,
					EmployeeName = e.Name,
					EmployeeIdentifier = e.EmployeeIdentifier,
					MissingAttendance = missing > 0 ? 1 : 0,
					Absences = summary.AttendanceToday.Absent
				})
				.ToList(),
			ShiftSummary =
			[
				new HRDashboardShiftSummaryDto
				{
					ShiftName = "All Staff",
					StaffCount = totalStaff,
					Present = summary.AttendanceToday.Present,
					Missing = missing
				}
			]
		};
	}

	private static HRDashboardPayrollPreviewDto BuildPayrollPreview(HRDashboardSummaryDto summary)
	{
		var payroll = summary.Payroll;
		return new HRDashboardPayrollPreviewDto
		{
			Month = payroll.Month,
			CanPreview = payroll.CanPreview,
			CanLock = payroll.CanLock,
			EmployeeCount = payroll.EmployeeCount,
			ReviewedCount = payroll.ReviewedCount,
			LockedCount = payroll.LockedCount,
			PendingCount = payroll.PendingCount,
			MissingAttendanceCount = payroll.MissingAttendanceCount,
			MissingSalaryStructureCount = payroll.MissingSalaryStructureCount,
			PolicyConfigurationIssueCount = payroll.PolicyConfigurationIssueCount,
			EstimatedGrossAmount = payroll.EstimatedGrossAmount,
			EstimatedNetAmount = payroll.EstimatedNetAmount,
			BlockingIssues = payroll.BlockingIssues,
			RecommendedActions = payroll.RecommendedActions
		};
	}

	private async Task<HRDashboardWorkforceDto> BuildWorkforceAsync(
		IQueryable<Employee> activeEmployees,
		int totalActive,
		int teaching,
		int nonTeaching,
		int newJoiners,
		int inactive)
	{
		var recentEmployees = await AsyncExecuter.ToListAsync(activeEmployees
			.OrderByDescending(e => e.CreationTime)
			.Take(6)
			.Select(e => new HRDashboardEmployeeSnapshotDto
			{
				Id = e.Id,
				Name = e.EmployeeName,
				EmployeeIdentifier = e.EmployeeCode ?? string.Empty,
				RoleName = e.Role != null ? e.Role.Name : null,
				Department = e.Department,
				IsTeaching = e.EmployeeGroup == EmployeeGroupType.Teaching
			}));

		return new HRDashboardWorkforceDto
		{
			TotalActiveStaff = totalActive,
			TeachingStaff = teaching,
			NonTeachingStaff = nonTeaching,
			NewJoinersThisMonth = newJoiners,
			InactiveStaff = inactive,
			RecentEmployees = recentEmployees
		};
	}

	private static HRDashboardAttendanceTodayDto BuildAttendanceToday(
		IReadOnlyCollection<AttendanceStatusCount> attendanceCounts,
		DateOnly today,
		int activeStaffCount)
	{
		var todayRecords = attendanceCounts.Where(a => a.AttendanceDate == today).ToList();
		var present = todayRecords.Where(a => a.StatusType == EmployeeAttendanceStatusType.Present).Sum(a => a.Count);
		var absent = todayRecords.Where(a => a.StatusType == EmployeeAttendanceStatusType.FullDayLeave).Sum(a => a.Count);
		var halfDay = todayRecords.Where(a => a.StatusType == EmployeeAttendanceStatusType.HalfDayLeave).Sum(a => a.Count);
		var totalMarked = present + absent + halfDay;
		var missingAttendance = Math.Max(0, activeStaffCount - totalMarked);
		var attendanceRate = activeStaffCount == 0
			? 0
			: Math.Round(((decimal)present + (halfDay * 0.5m)) / activeStaffCount * 100, 2);

		return new HRDashboardAttendanceTodayDto
		{
			Present = present,
			Absent = absent,
			HalfDay = halfDay,
			MissingAttendance = missingAttendance,
			LateOrException = absent + halfDay + missingAttendance,
			AttendanceRate = attendanceRate
		};
	}

	private static List<HRDashboardAttendanceTrendPointDto> BuildAttendanceTrend(
		IReadOnlyCollection<AttendanceStatusCount> attendanceCounts,
		IReadOnlyCollection<LeaveTrendRow> approvedLeaves,
		DateOnly trendStart,
		DateOnly trendEnd)
	{
		var result = new List<HRDashboardAttendanceTrendPointDto>();
		for (var date = trendStart; date <= trendEnd; date = date.AddDays(1))
		{
			var records = attendanceCounts.Where(a => a.AttendanceDate == date).ToList();
			var dateStart = new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
			var dateEnd = dateStart.AddDays(1).AddTicks(-1);
			var leaveCount = approvedLeaves
				.Where(l => l.StartDate <= dateEnd && l.EndDate >= dateStart)
				.Select(l => l.EmployeeId)
				.Distinct()
				.Count();

			result.Add(new HRDashboardAttendanceTrendPointDto
			{
				Date = date.ToDateTime(TimeOnly.MinValue),
				Present = records.Where(a => a.StatusType == EmployeeAttendanceStatusType.Present).Sum(a => a.Count),
				Absent = records.Where(a => a.StatusType == EmployeeAttendanceStatusType.FullDayLeave).Sum(a => a.Count),
				Leave = leaveCount
			});
		}

		return result;
	}

	private async Task<HRDashboardLeaveDto> BuildLeaveAsync(
		IQueryable<EmployeeLeave> leaveQuery,
		IQueryable<LeavePolicy> leavePolicyQuery,
		IQueryable<Employee> scopedEmployeeQuery,
		IReadOnlyCollection<Guid> activeEmployeeIds,
		DateTimeOffset monthStart,
		DateTimeOffset monthEnd)
	{
		var scopedLeaveQuery = leaveQuery.Where(l => activeEmployeeIds.Contains(l.EmployeeId));
		var pendingCount = await AsyncExecuter.CountAsync(
			scopedLeaveQuery.Where(l => l.Status == EmployeeLeaveStatusType.Pending));
		var monthStatusRows = await AsyncExecuter.ToListAsync(
			scopedLeaveQuery
				.Where(l =>
					l.StartDate <= monthEnd &&
					l.EndDate >= monthStart &&
					(l.Status == EmployeeLeaveStatusType.Approved || l.Status == EmployeeLeaveStatusType.Rejected))
				.GroupBy(l => l.Status)
				.Select(g => new { Status = g.Key, Count = g.Count() }));
		var monthStatusCounts = monthStatusRows.ToDictionary(x => x.Status, x => x.Count);
		var summaryRows = await AsyncExecuter.ToListAsync(
			scopedLeaveQuery
				.Where(l =>
					l.Status == EmployeeLeaveStatusType.Pending ||
					(l.StartDate <= monthEnd && l.EndDate >= monthStart))
				.GroupBy(l => l.LeaveTypeId)
				.Select(g => new { LeaveTypeId = g.Key, Count = g.Count() }));
		var recentPendingRows = await AsyncExecuter.ToListAsync(
			scopedLeaveQuery
				.Where(l => l.Status == EmployeeLeaveStatusType.Pending)
				.OrderByDescending(l => l.CreationTime)
				.Take(6)
				.Select(l => new
				{
					l.Id,
					l.PublicId,
					l.EmployeeId,
					l.LeaveTypeId,
					l.StartDate,
					l.EndDate,
					l.HalfDay,
					l.Status,
					l.CreationTime
				}));
		var policyIds = summaryRows
			.Select(x => x.LeaveTypeId)
			.Concat(recentPendingRows.Select(x => x.LeaveTypeId))
			.Distinct()
			.ToList();
		var policyNames = await AsyncExecuter.ToListAsync(
			leavePolicyQuery
				.Where(p => policyIds.Contains(p.Id))
				.Select(p => new { p.Id, p.PolicyName }));
		var policyNameById = policyNames.ToDictionary(x => x.Id, x => x.PolicyName);
		var pendingEmployeeIds = recentPendingRows.Select(l => l.EmployeeId).Distinct().ToList();
		var pendingEmployees = await AsyncExecuter.ToListAsync(
			scopedEmployeeQuery
				.Where(e => pendingEmployeeIds.Contains(e.Id))
				.Select(e => new { e.Id, e.EmployeeName, e.EmployeeCode }));
		var employeeById = pendingEmployees.ToDictionary(e => e.Id);

		return new HRDashboardLeaveDto
		{
			Pending = pendingCount,
			Approved = monthStatusCounts.GetValueOrDefault(EmployeeLeaveStatusType.Approved),
			Rejected = monthStatusCounts.GetValueOrDefault(EmployeeLeaveStatusType.Rejected),
			LeaveTypeSummary = summaryRows
				.Select(row => new HRDashboardLeaveTypeSummaryDto
				{
					LeaveType = policyNameById.GetValueOrDefault(row.LeaveTypeId, "Leave"),
					Count = row.Count
				})
				.OrderByDescending(x => x.Count)
				.Take(6)
				.ToList(),
			RecentPendingRequests = recentPendingRows.Select(l =>
			{
				employeeById.TryGetValue(l.EmployeeId, out var employee);
				return new HRDashboardLeaveRequestDto
				{
					Id = l.Id,
					PublicId = l.PublicId,
					EmployeeId = l.EmployeeId,
					EmployeeName = employee?.EmployeeName ?? "Employee",
					EmployeeIdentifier = employee?.EmployeeCode ?? string.Empty,
					LeaveType = policyNameById.GetValueOrDefault(l.LeaveTypeId, "Leave"),
					StartDate = l.StartDate.DateTime,
					EndDate = l.EndDate.DateTime,
					TotalDays = LeaveDurationCalculator.CountLeaveUnits(l.StartDate.Date, l.EndDate.Date, l.HalfDay),
					Status = l.Status.ToString(),
					SubmittedAt = l.CreationTime
				};
			}).ToList()
		};
	}

	private async Task<HRDashboardPayrollDto> BuildPayrollAsync(string month)
	{
		RunPayrollReadinessSummary readiness;
		try
		{
			readiness = await runPayrollAppService.GetReadinessAsync(month);
		}
		catch (BusinessException ex)
		{
			return new HRDashboardPayrollDto
			{
				Month = month,
				CanPreview = false,
				CanLock = false,
				BlockingIssues = [ex.Message],
				RecommendedActions = ["Review payroll configuration."]
			};
		}

		return new HRDashboardPayrollDto
		{
			Month = readiness.Month,
			CanPreview = readiness.CanPreview,
			CanLock = readiness.CanLock,
			EmployeeCount = readiness.EmployeeCount,
			ReviewedCount = readiness.ReviewedCount,
			LockedCount = readiness.LockedCount,
			PendingCount = Math.Max(0, readiness.EmployeeCount - readiness.ReviewedCount - readiness.LockedCount),
			MissingAttendanceCount = readiness.MissingAttendanceCount,
			MissingSalaryStructureCount = readiness.MissingSalaryStructureCount,
			PolicyConfigurationIssueCount = readiness.PolicyConfigurationIssueCount,
			BlockingIssues = readiness.BlockingIssues,
			RecommendedActions = readiness.RecommendedActions
		};
	}

	private static HRDashboardCelebrationsDto BuildCelebrations(
		IReadOnlyCollection<ActiveEmployeeDashboardRow> employees,
		DateTime dashboardDate)
	{
		var celebrations = employees
			.SelectMany(e => BuildCelebrationCandidates(e, dashboardDate, 30))
			.OrderBy(c => c.Date)
			.ThenBy(c => c.EmployeeName)
			.ToList();
		var today = celebrations
			.Where(c => c.Date.Date == dashboardDate.Date)
			.OrderBy(c => c.EmployeeName)
			.ToList();
		var upcoming = celebrations
			.Where(c => c.Date.Date > dashboardDate.Date)
			.Take(12)
			.ToList();

		return new HRDashboardCelebrationsDto
		{
			Today = today,
			Upcoming = upcoming
		};
	}

	private static IEnumerable<HRDashboardCelebrationDto> BuildCelebrationCandidates(
		ActiveEmployeeDashboardRow employee,
		DateTime dashboardDate,
		int daysAhead)
	{
		var rangeStart = dashboardDate.Date;
		var rangeEnd = rangeStart.AddDays(daysAhead);
		if (employee.DateOfBirth.HasValue)
		{
			var birthday = GetNextMonthDayOccurrence(
				employee.DateOfBirth.Value.Month,
				employee.DateOfBirth.Value.Day,
				rangeStart);
			if (birthday <= rangeEnd)
			{
				yield return new HRDashboardCelebrationDto
				{
					EmployeeId = employee.Id,
					EmployeeName = employee.EmployeeName,
					EmployeeIdentifier = employee.EmployeeCode ?? string.Empty,
					Type = "Birthday",
					Date = birthday
				};
			}
		}

		if (employee.DateOfJoining.HasValue)
		{
			var anniversary = GetNextMonthDayOccurrence(
				employee.DateOfJoining.Value.Month,
				employee.DateOfJoining.Value.Day,
				rangeStart);
			var years = anniversary.Year - employee.DateOfJoining.Value.Year;
			if (anniversary <= rangeEnd && years > 0)
			{
				yield return new HRDashboardCelebrationDto
				{
					EmployeeId = employee.Id,
					EmployeeName = employee.EmployeeName,
					EmployeeIdentifier = employee.EmployeeCode ?? string.Empty,
					Type = "Work Anniversary",
					Date = anniversary,
					Years = years
				};
			}
		}
	}

	private static DateTime GetNextMonthDayOccurrence(int month, int day, DateTime fromDate)
	{
		var normalizedDay = Math.Min(day, DateTime.DaysInMonth(fromDate.Year, month));
		var occurrence = new DateTime(fromDate.Year, month, normalizedDay);
		if (occurrence >= fromDate.Date)
		{
			return occurrence;
		}

		var nextYearDay = Math.Min(day, DateTime.DaysInMonth(fromDate.Year + 1, month));
		return new DateTime(fromDate.Year + 1, month, nextYearDay);
	}

	private static List<HRDashboardInsightDto> BuildInsights(
		HRDashboardAttendanceTodayDto attendanceToday,
		HRDashboardLeaveDto leave,
		HRDashboardPayrollDto payroll,
		HRDashboardCelebrationsDto celebrations,
		InactiveReportingManagerIssue inactiveReportingManagerIssue)
	{
		var insights = new List<HRDashboardInsightDto>();

		if (inactiveReportingManagerIssue.ActiveDirectReportCount > 0)
		{
			insights.Add(new HRDashboardInsightDto
			{
				Severity = "critical",
				Title = "Inactive reporting managers",
				Summary = $"{inactiveReportingManagerIssue.ActiveDirectReportCount} active employee(s) report to {inactiveReportingManagerIssue.ManagerCount} inactive manager(s).",
				Source = "employee.reporting-manager",
				ActionLabel = "Review employees",
				ActionHref = "/hr/employee?reportingManagerStatus=nonActive"
			});
		}

		if (payroll.MissingAttendanceCount > 0)
		{
			insights.Add(new HRDashboardInsightDto
			{
				Severity = "warning",
				Title = "Attendance missing for payroll",
				Summary = $"{payroll.MissingAttendanceCount} staff members are missing payroll attendance inputs for {payroll.Month}.",
				Source = "payroll.readiness",
				ActionLabel = "Review attendance",
				ActionHref = "/hr/manual-attendance"
			});
		}

		if (payroll.MissingSalaryStructureCount > 0)
		{
			insights.Add(new HRDashboardInsightDto
			{
				Severity = "critical",
				Title = "Salary structures incomplete",
				Summary = $"{payroll.MissingSalaryStructureCount} staff members need salary structures before payroll can lock.",
				Source = "payroll.readiness",
				ActionLabel = "Open salary structures",
				ActionHref = "/hr/payroll/structure"
			});
		}

		if (!payroll.CanLock && payroll.BlockingIssues.Count > 0)
		{
			insights.Add(new HRDashboardInsightDto
			{
				Severity = "critical",
				Title = "Payroll cannot be locked",
				Summary = payroll.BlockingIssues[0],
				Source = "payroll.readiness",
				ActionLabel = "Open payroll",
				ActionHref = "/hr/payroll/run-payroll"
			});
		}

		if (leave.Pending > 0)
		{
			insights.Add(new HRDashboardInsightDto
			{
				Severity = leave.Pending >= 5 ? "warning" : "info",
				Title = "Leave approvals pending",
				Summary = $"{leave.Pending} leave requests are waiting for HR action.",
				Source = "leave.management",
				ActionLabel = "Review leaves",
				ActionHref = "/hr/leave-approvals"
			});
		}

		if (attendanceToday.AttendanceRate > 0 && attendanceToday.AttendanceRate < 85)
		{
			insights.Add(new HRDashboardInsightDto
			{
				Severity = "warning",
				Title = "Attendance below target",
				Summary = $"Today's staff attendance is {attendanceToday.AttendanceRate:0.##}%.",
				Source = "attendance.today",
				ActionLabel = "View attendance",
				ActionHref = "/hr/attendance-tracking"
			});
		}

		if (celebrations.Today.Count > 0)
		{
			insights.Add(new HRDashboardInsightDto
			{
				Severity = "info",
				Title = "Celebrations today",
				Summary = $"{celebrations.Today.Count} birthday or work anniversary is due today.",
				Source = "employee.celebrations",
				ActionLabel = "View celebrations",
				ActionHref = "/hr/dashboard"
			});
		}

		return insights;
	}

	private async Task<InactiveReportingManagerIssue> BuildInactiveReportingManagerIssueAsync(
		IQueryable<Employee> employeeQuery)
	{
		var affectedRows = await AsyncExecuter.ToListAsync(
			employeeQuery
				.Where(e =>
					e.EmployeeStatusType == EmployeeStatusType.Active &&
					e.ReportingManagerId.HasValue &&
					e.ReportingManager != null &&
					e.ReportingManager.EmployeeStatusType.HasValue &&
					e.ReportingManager.EmployeeStatusType.Value != EmployeeStatusType.Active)
				.Select(e => new
				{
					e.Id,
					ManagerId = e.ReportingManagerId!.Value
				}));

		return new InactiveReportingManagerIssue(
			affectedRows.Count,
			affectedRows.Select(x => x.ManagerId).Distinct().Count());
	}

	private static DateTime ParseDateOrDefault(string? value, DateTime fallback)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return fallback;
		}

		if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
		{
			return parsed.Date;
		}

		throw new AbpValidationException(
			"Dashboard date is invalid.",
			[new ValidationResult("Date must be yyyy-MM-dd.", [nameof(HRDashboardSummaryRequestDto.Date)])]);
	}

	private static string NormalizeMonth(string? value, DateTime fallbackDate)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return fallbackDate.ToString("yyyy-MM", CultureInfo.InvariantCulture);
		}

		if (DateTime.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
		{
			return parsed.ToString("yyyy-MM", CultureInfo.InvariantCulture);
		}

		throw new AbpValidationException(
			"Dashboard payroll month is invalid.",
			[new ValidationResult("Month must be yyyy-MM.", [nameof(HRDashboardSummaryRequestDto.Month)])]);
	}

	private async Task<IQueryable<Employee>> GetScopedEmployeeQueryAsync()
	{
		var scope = await EnsureCanReadScopedEmployeesAsync();
		var employeeQuery = await employeeRepository.GetQueryableAsync();

		if (CurrentTenant.Id.HasValue)
		{
			employeeQuery = employeeQuery.Where(e => e.TenantId == CurrentTenant.Id);
		}

		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return employeeQuery;
		}

		var attendanceQuery = await attendanceRepository.GetQueryableAsync();
		if (CurrentTenant.Id.HasValue)
		{
			attendanceQuery = attendanceQuery.Where(a => a.TenantId == CurrentTenant.Id);
		}

		var employeeStageRows = await AsyncExecuter.ToListAsync(
			attendanceQuery
				.Where(a => a.EducationMediumStageId.HasValue)
				.Select(a => new { a.EmployeeId, a.EducationMediumStageId }));
		var employeeStageMap = employeeStageRows
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(
				x => x.Key,
				x => (IReadOnlyCollection<Guid>)x
					.Select(row => row.EducationMediumStageId!.Value)
					.Distinct()
					.ToList());
		var scopedStageEmployeeIds = await hrScopeAccessService.FilterEmployeeIdsByStagesAsync(
			employeeStageMap.Keys.ToList(),
			employeeStageMap,
			scope,
			includeUnknownStageEmployees: false);

		return employeeQuery.Where(e => scopedStageEmployeeIds.Contains(e.Id));
	}

	private async Task<UserScopeContext> EnsureCanReadScopedEmployeesAsync()
	{
		var uid = CurrentUser.Id;
		if (!uid.HasValue)
		{
			throw new BusinessException(HRErrorCodes.AttendanceImportPreviewInvalid);
		}

		var scope = await userScopeService.GetUserScopeAsync(uid.Value);
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return scope;
		}

		if (!HrUserScopeAccess.HasAnyConcreteHrStageAccess(scope))
		{
			throw new BusinessException(HRErrorCodes.TakeAttendanceMediumStageOutOfScope);
		}

		return scope;
	}

	private async Task<HrDashboardPreview?> FindPreviewAsync(string dashboardKey)
	{
		return await AsyncExecuter.FirstOrDefaultAsync((await previewRepository.GetQueryableAsync())
			.Where(x => x.TenantId == CurrentTenant.Id && x.DashboardKey == dashboardKey));
	}

	private static HRDashboardPreviewDto EmptyPreview(string dashboardKey)
	{
		return new HRDashboardPreviewDto
		{
			DashboardKey = dashboardKey,
			Status = HrDashboardPreviewStatus.Empty,
			PayloadJson = "{}",
			PayloadVersion = PayloadVersion
		};
	}

	private static HRDashboardPreviewDto ToPreviewDto(HrDashboardPreview preview)
	{
		return new HRDashboardPreviewDto
		{
			DashboardKey = preview.DashboardKey,
			Status = preview.Status,
			PayloadJson = preview.PayloadJson,
			PayloadVersion = preview.PayloadVersion,
			LastRefreshedAt = preview.LastRefreshedAt,
			RefreshStartedAt = preview.RefreshStartedAt,
			RefreshRequestedByUserId = preview.RefreshRequestedByUserId,
			LastError = preview.LastError
		};
	}

	private static string NormalizeDashboardKey(string dashboardKey)
	{
		var normalized = HrDashboardKeys.All.FirstOrDefault(key =>
			string.Equals(key, dashboardKey, StringComparison.OrdinalIgnoreCase));
		if (normalized == null)
		{
			throw new AbpValidationException(
				"Dashboard key is invalid.",
				[new ValidationResult("Dashboard key must be Main, Attendance, or Payroll.", ["dashboardKey"])]);
		}

		return normalized;
	}

	private async Task CheckDashboardPermissionAsync(string dashboardKey)
	{
		var permission = dashboardKey switch
		{
			HrDashboardKeys.Main => ShikshaMitraSubmodulePermissions.HR.Dashboard.View,
			HrDashboardKeys.Attendance => ShikshaMitraSubmodulePermissions.HR.AttendanceAndLeave_Dashboard.View,
			HrDashboardKeys.Payroll => ShikshaMitraSubmodulePermissions.HR.PayrollManagement_Dashboard.View,
			_ => ShikshaMitraSubmodulePermissions.HR.Dashboard.View
		};

		await AuthorizationService.CheckAsync(permission);
	}

	private sealed record ActiveEmployeeDashboardRow(
		Guid Id,
		string EmployeeName,
		string? EmployeeCode,
		EmployeeGroupType? EmployeeGroup,
		DateOnly? DateOfJoining,
		DateOnly? DateOfBirth);

	private sealed record AttendanceStatusCount(
		DateOnly AttendanceDate,
		EmployeeAttendanceStatusType StatusType,
		int Count);

	private sealed record LeaveTrendRow(
		Guid EmployeeId,
		DateTimeOffset StartDate,
		DateTimeOffset EndDate);

	private sealed record InactiveReportingManagerIssue(
		int ActiveDirectReportCount,
		int ManagerCount);
}
