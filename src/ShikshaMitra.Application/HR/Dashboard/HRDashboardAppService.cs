using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
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
	private static readonly TimeSpan PreviewStaleAfter = TimeSpan.FromHours(24);
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

	public async Task<HRDashboardSummaryDto> GetSummaryAsync(HRDashboardSummaryRequestDto input)
	{
		var scope = await EnsureCanReadScopedEmployeesAsync();
		return await GetSummaryAsync(input, scope);
	}

	private async Task<HRDashboardSummaryDto> GetSummaryAsync(
		HRDashboardSummaryRequestDto input,
		UserScopeContext scope)
	{
		input ??= new HRDashboardSummaryRequestDto();
		var dashboardDate = ParseDateOrDefault(input.Date, DateTime.Today);
		var rangeDays = Math.Clamp(input.Range ?? DefaultRangeDays, 1, MaxRangeDays);
		var month = NormalizeMonth(input.Month, dashboardDate);
		var selectedMonthStart = DateTime.ParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture);
		var selectedNextMonthStart = selectedMonthStart.AddMonths(1);
		var selectedMonthStartDate = DateOnly.FromDateTime(selectedMonthStart);
		var selectedMonthEndDate = DateOnly.FromDateTime(selectedNextMonthStart.AddDays(-1));

		var employeeQuery = await GetScopedEmployeeQueryAsync(scope);
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
		var leaveTrendRows = await AsyncExecuter.ToListAsync(
			leaveQuery.Where(l =>
				l.Status == EmployeeLeaveStatusType.Approved &&
				l.StartDateOnly <= trendEnd &&
				l.EndDateOnly >= trendStart &&
				activeEmployeeIds.Contains(l.EmployeeId))
				.Select(l => new LeaveTrendRow(l.EmployeeId, l.StartDateOnly, l.EndDateOnly)));

		var attendanceToday = BuildAttendanceToday(attendanceCounts, todayDateOnly, activeEmployeeIds.Count);
		var attendanceTrend = BuildAttendanceTrend(attendanceCounts, leaveTrendRows, trendStart, trendEnd);
		var leave = await BuildLeaveAsync(
			leaveQuery,
			leavePolicyQuery,
			employeeQuery,
			activeEmployeeIds,
			selectedMonthStartDate,
			selectedMonthEndDate);
		var inactiveReportingManagerIssue = await BuildInactiveReportingManagerIssueAsync(employeeQuery);
		var payroll = await BuildPayrollAsync(month);
		var celebrations = BuildCelebrations(celebrationCandidates, dashboardDate);
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
		var scope = await EnsureCanReadScopedEmployeesAsync();
		var scopeKey = BuildScopeKey(scope);

		var preview = await FindPreviewAsync(dashboardKey, scopeKey);
		return preview == null ? EmptyPreview(dashboardKey, scopeKey) : ToPreviewDto(preview);
	}

	public async Task<HRDashboardPreviewDto> RefreshPreviewAsync(string dashboardKey)
	{
		dashboardKey = NormalizeDashboardKey(dashboardKey);
		await CheckDashboardPermissionAsync(dashboardKey);
		var scope = await EnsureCanReadScopedEmployeesAsync();
		var scopeKey = BuildScopeKey(scope);

		var now = Clock.Now;
		var preview = await FindPreviewAsync(dashboardKey, scopeKey);
		if (preview == null)
		{
			preview = new HrDashboardPreview(GuidGenerator.Create(), CurrentTenant.Id, dashboardKey, scopeKey);
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
				ScopeKey = scopeKey,
				RequestedByUserId = CurrentUser.Id
			});
		}

		return ToPreviewDto(preview);
	}

	[AllowAnonymous]
	public async Task RefreshExistingPreviewsAsync()
	{
		await EnsureScheduledPreviewsAsync();

		List<HrDashboardPreview> previews;
		var refreshStaleBefore = Clock.Now - RefreshStaleAfter;
		using (dataFilter.Disable<IMultiTenant>())
		{
			previews = await AsyncExecuter.ToListAsync((await previewRepository.GetQueryableAsync())
				.Where(x => x.TenantId == CurrentTenant.Id &&
					x.ScopeKey == HrDashboardScopeKeys.HrWide &&
					(x.Status != HrDashboardPreviewStatus.Refreshing ||
					!x.RefreshStartedAt.HasValue ||
					x.RefreshStartedAt.Value <= refreshStaleBefore)));
		}

		foreach (var preview in previews)
		{
			preview.MarkRefreshing(null, Clock.Now);
			await previewRepository.UpdateAsync(preview, autoSave: true);
			await backgroundJobManager.EnqueueAsync(new HrDashboardPreviewRefreshJobArgs
			{
				TenantId = preview.TenantId,
				PreviewId = preview.Id,
				DashboardKey = preview.DashboardKey,
				ScopeKey = preview.ScopeKey
			});
		}
	}

	private async Task EnsureScheduledPreviewsAsync()
	{
		foreach (var dashboardKey in HrDashboardKeys.All)
		{
			var preview = await FindPreviewAsync(dashboardKey, HrDashboardScopeKeys.HrWide);
			if (preview != null)
			{
				continue;
			}

			await previewRepository.InsertAsync(
				new HrDashboardPreview(
					GuidGenerator.Create(),
					CurrentTenant.Id,
					dashboardKey,
					HrDashboardScopeKeys.HrWide),
				autoSave: true);
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
			var refreshScope = await ResolveRefreshScopeAsync(args);
			if (!string.Equals(preview.ScopeKey, BuildScopeKey(refreshScope), StringComparison.Ordinal))
			{
				throw new BusinessException("HR:Dashboard:ScopeChanged");
			}

			var payloadJson = await BuildPreviewPayloadJsonAsync(preview.DashboardKey, refreshScope);
			preview.MarkReady(payloadJson, PayloadVersion, Clock.Now);
		}
		catch (Exception ex)
		{
			Logger.LogWarning(
				ex,
				"HR dashboard preview refresh failed. PreviewId={PreviewId}, DashboardKey={DashboardKey}, ScopeKey={ScopeKey}",
				preview.Id,
				preview.DashboardKey,
				preview.ScopeKey);
			preview.MarkFailed(FormatRefreshError(ex), Clock.Now);
		}

		await previewRepository.UpdateAsync(preview, autoSave: true);
	}

	private async Task<string> BuildPreviewPayloadJsonAsync(string dashboardKey, UserScopeContext scope)
	{
		var defaults = new HRDashboardSummaryRequestDto
		{
			Date = Clock.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
			Month = Clock.Now.ToString("yyyy-MM", CultureInfo.InvariantCulture),
			Range = 7
		};
		var summary = await GetSummaryAsync(defaults, scope);

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
			var leaveCount = approvedLeaves
				.Where(l => l.StartDate <= date && l.EndDate >= date)
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
		DateOnly monthStart,
		DateOnly monthEnd)
	{
		var pendingQuery = leaveQuery.Where(l =>
			l.Status == EmployeeLeaveStatusType.Pending &&
			activeEmployeeIds.Contains(l.EmployeeId));
		var pendingCount = await AsyncExecuter.CountAsync(pendingQuery);
		var pendingSummaryRows = await AsyncExecuter.ToListAsync(pendingQuery
			.GroupBy(l => l.LeaveTypeId)
			.Select(g => new LeaveTypeCountRow(g.Key, g.Count())));
		var recentPendingRows = await AsyncExecuter.ToListAsync(pendingQuery
			.OrderByDescending(l => l.CreationTime)
			.Take(6)
			.Select(l => new LeaveDashboardRow(
				l.Id,
				l.PublicId,
				l.EmployeeId,
				l.LeaveTypeId,
				l.StartDate,
				l.EndDate,
				l.HalfDay,
				l.Status,
				l.CreationTime)));
		var monthRows = await AsyncExecuter.ToListAsync(
			leaveQuery
				.Where(l =>
					(l.Status == EmployeeLeaveStatusType.Approved || l.Status == EmployeeLeaveStatusType.Rejected) &&
					l.StartDateOnly <= monthEnd &&
					l.EndDateOnly >= monthStart &&
					activeEmployeeIds.Contains(l.EmployeeId))
				.Select(l => new LeaveDashboardRow(
					l.Id,
					l.PublicId,
					l.EmployeeId,
					l.LeaveTypeId,
					l.StartDate,
					l.EndDate,
					l.HalfDay,
					l.Status,
					l.CreationTime)));
		var monthStatusCounts = monthRows
			.GroupBy(l => l.Status)
			.ToDictionary(g => g.Key, g => g.Count());
		var summaryRows = pendingSummaryRows
			.Concat(monthRows
				.GroupBy(l => l.LeaveTypeId)
				.Select(g => new LeaveTypeCountRow(g.Key, g.Count())))
			.GroupBy(l => l.LeaveTypeId)
			.Select(g => new LeaveTypeCountRow(g.Key, g.Sum(x => x.Count)))
			.ToList();
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

	private async Task<IQueryable<Employee>> GetScopedEmployeeQueryAsync(UserScopeContext scope)
	{
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

	private Task<UserScopeContext> ResolveRefreshScopeAsync(HrDashboardPreviewRefreshJobArgs args)
	{
		if (args.RequestedByUserId.HasValue)
		{
			return userScopeService.GetUserScopeAsync(args.RequestedByUserId.Value);
		}

		return Task.FromResult(CreateHrWideScope());
	}

	private UserScopeContext CreateHrWideScope()
	{
		return new UserScopeContext
		{
			UserId = Guid.Empty,
			TenantId = CurrentTenant.Id,
			IsAdmin = true,
			CalculatedAt = Clock.Now
		};
	}

	private async Task<HrDashboardPreview?> FindPreviewAsync(string dashboardKey, string scopeKey)
	{
		return await AsyncExecuter.FirstOrDefaultAsync((await previewRepository.GetQueryableAsync())
			.Where(x =>
				x.TenantId == CurrentTenant.Id &&
				x.DashboardKey == dashboardKey &&
				x.ScopeKey == scopeKey));
	}

	private HRDashboardPreviewDto EmptyPreview(string dashboardKey, string scopeKey)
	{
		return new HRDashboardPreviewDto
		{
			DashboardKey = dashboardKey,
			ScopeKey = scopeKey,
			Status = HrDashboardPreviewStatus.Empty,
			PayloadJson = "{}",
			PayloadVersion = PayloadVersion,
			IsStale = true,
			NextScheduledRefreshAt = GetNextScheduledRefreshAt(Clock.Now)
		};
	}

	private HRDashboardPreviewDto ToPreviewDto(HrDashboardPreview preview)
	{
		return new HRDashboardPreviewDto
		{
			DashboardKey = preview.DashboardKey,
			ScopeKey = preview.ScopeKey,
			Status = preview.Status,
			PayloadJson = preview.PayloadJson,
			PayloadVersion = preview.PayloadVersion,
			IsStale = !preview.LastRefreshedAt.HasValue || Clock.Now - preview.LastRefreshedAt.Value > PreviewStaleAfter,
			LastRefreshedAt = preview.LastRefreshedAt,
			LastSuccessfulRefreshAt = preview.LastRefreshedAt,
			NextScheduledRefreshAt = GetNextScheduledRefreshAt(Clock.Now),
			RefreshStartedAt = preview.RefreshStartedAt,
			RefreshRequestedByUserId = preview.RefreshRequestedByUserId,
			LastError = preview.LastError
		};
	}

	private static string BuildScopeKey(UserScopeContext scope)
	{
		if (HrUserScopeAccess.HasHrWideAccess(scope))
		{
			return HrDashboardScopeKeys.HrWide;
		}

		var raw = string.Join("|",
			$"ay:{JoinIds(scope.AcademicYearIds)}",
			$"b:{JoinIds(scope.BoardIds)}",
			$"m:{JoinIds(scope.MediumIds)}",
			$"ms:{JoinIds(scope.MediumStageIds)}",
			$"s:{JoinIds(scope.StandardIds)}",
			$"d:{JoinIds(scope.DivisionIds)}",
			$"way:{JoinIds(scope.WildcardBoardAcademicYearIds)}",
			$"wb:{JoinIds(scope.WildcardMediumBoardIds)}",
			$"wm:{JoinIds(scope.WildcardMediumStageMediumIds)}",
			$"wms:{JoinIds(scope.WildcardStandardMediumStageIds)}",
			$"ws:{JoinIds(scope.WildcardDivisionStandardIds)}");
		var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
		return $"scoped-{Convert.ToHexString(hash)[..32].ToLowerInvariant()}";
	}

	private static string JoinIds(IEnumerable<Guid> ids) =>
		string.Join(",", ids.OrderBy(x => x).Select(x => x.ToString("N")));

	private static DateTime GetNextScheduledRefreshAt(DateTime now)
	{
		var nowUtc = now.Kind switch
		{
			DateTimeKind.Utc => now,
			DateTimeKind.Local => now.ToUniversalTime(),
			_ => DateTime.SpecifyKind(now, DateTimeKind.Utc)
		};
		var nowIst = ConvertUtcToIndiaTime(nowUtc);
		var nextIst = nowIst.Date.AddHours(6);
		if (nowIst >= nextIst)
		{
			nextIst = nextIst.AddDays(1);
		}

		var nextUtc = DateTime.SpecifyKind(nextIst.AddHours(-5.5), DateTimeKind.Utc);
		return now.Kind == DateTimeKind.Local ? nextUtc.ToLocalTime() : DateTime.SpecifyKind(nextUtc, now.Kind);
	}

	private static DateTime ConvertUtcToIndiaTime(DateTime utcNow)
	{
		try
		{
			return TimeZoneInfo.ConvertTimeFromUtc(
				DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
				TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));
		}
		catch (Exception) when (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
		{
			return TimeZoneInfo.ConvertTimeFromUtc(
				DateTime.SpecifyKind(utcNow, DateTimeKind.Utc),
				TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"));
		}
		catch
		{
			return DateTime.SpecifyKind(utcNow, DateTimeKind.Utc).AddHours(5.5);
		}
	}

	private static string FormatRefreshError(Exception exception)
	{
		if (exception is BusinessException businessException)
		{
			var code = string.IsNullOrWhiteSpace(businessException.Code)
				? null
				: businessException.Code;
			var message = string.IsNullOrWhiteSpace(businessException.Message) ||
				businessException.Message.StartsWith("Exception of type", StringComparison.Ordinal)
					? null
					: businessException.Message;

			if (code != null && message != null)
			{
				return $"{code}: {message}";
			}

			if (code != null)
			{
				return code;
			}

			if (businessException.InnerException?.Message is { Length: > 0 } innerMessage)
			{
				return innerMessage;
			}
		}

		return string.IsNullOrWhiteSpace(exception.Message)
			? "Dashboard refresh failed."
			: exception.Message;
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
		DateOnly StartDate,
		DateOnly EndDate);

	private sealed record LeaveDashboardRow(
		Guid Id,
		int PublicId,
		Guid EmployeeId,
		Guid LeaveTypeId,
		DateTimeOffset StartDate,
		DateTimeOffset EndDate,
		bool HalfDay,
		EmployeeLeaveStatusType Status,
		DateTime CreationTime);

	private sealed record LeaveTypeCountRow(
		Guid LeaveTypeId,
		int Count);

	private sealed record InactiveReportingManagerIssue(
		int ActiveDirectReportCount,
		int ManagerCount);
}
