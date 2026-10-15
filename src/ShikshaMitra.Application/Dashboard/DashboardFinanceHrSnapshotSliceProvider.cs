using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Common;
using ShikshaMitra.Finance;
using ShikshaMitra.Finance.Reports;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Dashboard;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Records;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using static ShikshaMitra.Dashboard.DashboardSummaryMutator;
using static ShikshaMitra.Dashboard.DashboardTemplates;
using static ShikshaMitra.Dashboard.DashboardWidgetCatalog;
using static ShikshaMitra.Dashboard.DashboardZones;

namespace ShikshaMitra.Dashboard;

public class DashboardFinanceHrSnapshotSliceProvider(
	IRepository<Student, Guid> studentRepository,
	StudentScopeAccessService studentScopeAccessService,
	FinanceScopeAccessService financeScopeAccessService,
	IEmployeeAttendanceRepository employeeAttendanceRepository,
	HRDashboardEmployeeScopeReader hrEmployeeScopeReader,
	HRDashboardPayrollReader hrDashboardPayrollReader,
	IUserScopeAccessService userScopeAccessService,
	IFeePaymentReceiptRepository feePaymentReceiptRepository,
	IDueFeeRepository dueFeeRepository,
	IRepository<FeeHead, Guid> feeHeadRepository,
	IConcessionRequestRepository concessionRequestRepository,
	IAsyncQueryableExecuter asyncExecuter,
	IPermissionChecker permissionChecker)
	: IDashboardSummarySliceProvider, ITransientDependency
{
	public async Task ApplyAsync(DashboardSummarySliceContext context)
	{
		await AddFinanceSnapshotAsync(context.Summary, context.DateRange);
		await AddHrSnapshotAsync(context.Summary, context.DateRange);
	}

	private async Task AddFinanceSnapshotAsync(DashboardSummaryDto summary, DashboardDateRangeDto range)
	{
		if (summary.DashboardTemplate is not (Leadership or FinanceHr))
		{
			return;
		}

		var needsCollection = summary.DashboardTemplate == Leadership
			? await HasAnyPermissionAsync(WidgetDefinitions["leadership-fee-collection"].RequiredPermissions)
			: await HasAnyPermissionAsync(WidgetDefinitions["finance-collection"].RequiredPermissions);
		var needsDues = summary.DashboardTemplate == FinanceHr &&
			await HasAnyPermissionAsync(WidgetDefinitions["finance-dues"].RequiredPermissions);
		var needsConcession = summary.DashboardTemplate == FinanceHr &&
			await HasAnyPermissionAsync(WidgetDefinitions["finance-concession-impact"].RequiredPermissions);
		var needsFinanceModule = summary.DashboardTemplate == FinanceHr &&
			await HasAnyPermissionAsync(WidgetDefinitions["finance-dashboard"].RequiredPermissions);

		if (!needsCollection && !needsDues && !needsConcession && !needsFinanceModule)
		{
			return;
		}

		try
		{
			var scopedStudentIdsQuery = await GetScopedStudentIdsQueryAsync();
			var scopedAccountIdsQuery = await financeScopeAccessService.GetScopedFeeAccountIdsQueryAsync();
			var fromDate = DateOnly.FromDateTime(ParseNormalizedDate(range.From));
			var toDate = DateOnly.FromDateTime(ParseNormalizedDate(range.To));
			var academicYearId = Guid.TryParse(summary.AcademicYearId, out var parsedAcademicYearId)
				? parsedAcademicYearId
				: (Guid?)null;

			IReadOnlyList<FinanceAmountRow>? todayRows = null;
			IReadOnlyList<FinanceAmountRow>? dueRows = null;
			int? pendingConcessions = null;

			if (needsCollection && summary.DashboardTemplate == Leadership && academicYearId.HasValue)
			{
				var receiptQuery = await feePaymentReceiptRepository.GetQueryableAsync();
				var scopedFeeReceipts = receiptQuery.Where(r =>
					r.AcademicYearId == academicYearId.Value &&
					r.ReceiptScope == FeeReceiptScope.Fee &&
					r.VerificationStatus != FeeReceiptVerificationStatus.Rejected &&
					r.LinkedFinalReceiptId == null &&
					((!r.FinanceFeeAccountId.HasValue && r.StudentId.HasValue && scopedStudentIdsQuery.Contains(r.StudentId.Value)) ||
					 (r.FinanceFeeAccountId.HasValue && scopedAccountIdsQuery.Contains(r.FinanceFeeAccountId.Value))));

				var periodRows = await asyncExecuter.ToListAsync(scopedFeeReceipts
					.Where(r => r.ReceiptDate >= fromDate && r.ReceiptDate <= toDate)
					.Select(r => new FinanceAmountRow(r.TotalPaidAmount, r.StudentId, r.FinanceFeeAccountId)));
				var periodTotal = periodRows.Sum(x => x.Amount);
				var subtitle = periodRows.Count == 0
					? "No scoped fee receipts in selected range"
					: $"{periodRows.Count} receipts in selected range";
				UpdateWidget(
					summary.KpiCards,
					"leadership-fee-collection",
					null,
					FormatIndianAmount(periodTotal),
					subtitle,
					periodRows.Count == 0 ? "info" : "success");
			}

			if (summary.DashboardTemplate == FinanceHr && (needsCollection || needsFinanceModule) && academicYearId.HasValue)
			{
				var receiptQuery = await feePaymentReceiptRepository.GetQueryableAsync();
				var scopedFeeReceipts = receiptQuery.Where(r =>
					r.AcademicYearId == academicYearId.Value &&
					r.ReceiptScope == FeeReceiptScope.Fee &&
					r.VerificationStatus != FeeReceiptVerificationStatus.Rejected &&
					r.LinkedFinalReceiptId == null &&
					((!r.FinanceFeeAccountId.HasValue && r.StudentId.HasValue && scopedStudentIdsQuery.Contains(r.StudentId.Value)) ||
					 (r.FinanceFeeAccountId.HasValue && scopedAccountIdsQuery.Contains(r.FinanceFeeAccountId.Value))));

				todayRows = await asyncExecuter.ToListAsync(scopedFeeReceipts
					.Where(r => r.ReceiptDate == toDate)
					.Select(r => new FinanceAmountRow(r.TotalPaidAmount, r.StudentId, r.FinanceFeeAccountId)));

				if (needsCollection)
				{
					var todayTotal = todayRows.Sum(x => x.Amount);
					var subtitle = BuildCountSubtitle(todayRows.Count, "receipt posted today", "receipts posted today");
					UpdateWidget(
						summary.KpiCards,
						"finance-collection",
						null,
						FormatIndianAmount(todayTotal),
						subtitle,
						todayRows.Count == 0 ? "info" : "success");
				}
			}

			if ((needsDues || needsFinanceModule) && academicYearId.HasValue)
			{
				var dueQuery = await dueFeeRepository.GetQueryableAsync();
				var feeHeadQuery = await feeHeadRepository.GetQueryableAsync();
				var academicYearFeeHeadIds = feeHeadQuery
					.Where(fh => fh.AcademicYearId == academicYearId.Value)
					.Select(fh => fh.Id);
				dueRows = await asyncExecuter.ToListAsync(dueQuery
					.Where(d =>
						academicYearFeeHeadIds.Contains(d.FeeHeadId) &&
						d.OutstandingAmount > 0 &&
						((!d.FinanceFeeAccountId.HasValue && d.StudentId.HasValue && scopedStudentIdsQuery.Contains(d.StudentId.Value)) ||
						 (d.FinanceFeeAccountId.HasValue && scopedAccountIdsQuery.Contains(d.FinanceFeeAccountId.Value))))
					.Select(d => new FinanceAmountRow((double)d.OutstandingAmount, d.StudentId, d.FinanceFeeAccountId)));

				if (needsDues)
				{
					var outstanding = dueRows.Sum(x => x.Amount);
					var studentCount = dueRows.Select(x => FinanceReportPayerKey.From(x.StudentId, x.FinanceFeeAccountId)).Distinct().Count();
					var subtitle = BuildCountSubtitle(studentCount, "payer pending dues", "payers pending dues");
					UpdateWidget(
						summary.KpiCards,
						"finance-dues",
						null,
						FormatIndianAmount(outstanding),
						subtitle,
						studentCount == 0 ? "success" : "warning");
				}
			}

			if ((needsConcession || needsFinanceModule) && academicYearId.HasValue)
			{
				var concessionQuery = await concessionRequestRepository.GetQueryableAsync();
				pendingConcessions = await asyncExecuter.CountAsync(concessionQuery
					.Where(c =>
						c.AcademicYearId == academicYearId.Value &&
						c.Status == ConcessionRequestStatus.Pending &&
						scopedStudentIdsQuery.Contains(c.StudentId)));

				if (needsConcession)
				{
					UpdateWidget(
						summary.RiskAlerts,
						"finance-concession-impact",
						pendingConcessions,
						null,
						BuildCountSubtitle(pendingConcessions.Value, "concession request pending review", "concession requests pending review"),
						CountStatus(pendingConcessions.Value));
				}
			}

			if (needsFinanceModule)
			{
				var todayTotal = todayRows?.Sum(x => x.Amount) ?? 0d;
				var dueStudentCount = dueRows?.Select(x => FinanceReportPayerKey.From(x.StudentId, x.FinanceFeeAccountId)).Distinct().Count() ?? 0;
				var concessionCount = pendingConcessions ?? 0;
				var followUpCount = dueStudentCount + concessionCount;
				var subtitle = $"{dueStudentCount} due payers, {concessionCount} concession reviews";
				UpdateWidget(
					summary.ModuleSnapshots,
					"finance-dashboard",
					followUpCount,
					FormatIndianAmount(todayTotal),
					subtitle,
					followUpCount == 0 ? "success" : "warning",
					health: followUpCount == 0 ? "good" : "attention");
			}
		}
		catch
		{
			SetZoneError(summary, Kpis, "Finance snapshot could not be loaded.");
		}
	}

	private async Task AddHrSnapshotAsync(DashboardSummaryDto summary, DashboardDateRangeDto range)
	{
		if (summary.DashboardTemplate is not (FinanceHr or PrincipalAdmin))
		{
			return;
		}

		var needsPrincipalTeacherAttendance = summary.DashboardTemplate == PrincipalAdmin &&
			await HasAnyPermissionAsync(WidgetDefinitions["principal-teacher-attendance"].RequiredPermissions);
		var needsPayroll = summary.DashboardTemplate == FinanceHr &&
			await HasAnyPermissionAsync(WidgetDefinitions["hr-payroll-readiness"].RequiredPermissions);
		var needsAttendance = summary.DashboardTemplate == FinanceHr &&
			await HasAnyPermissionAsync(WidgetDefinitions["hr-staff-attendance"].RequiredPermissions);
		var needsHrModule = summary.DashboardTemplate == FinanceHr &&
			await HasAnyPermissionAsync(WidgetDefinitions["hr-dashboard"].RequiredPermissions);

		if (!needsPrincipalTeacherAttendance && !needsPayroll && !needsAttendance && !needsHrModule)
		{
			return;
		}

		try
		{
			var toDate = ParseNormalizedDate(range.To);
			var month = toDate.ToString("yyyy-MM", CultureInfo.InvariantCulture);
			var scope = await userScopeAccessService.GetCurrentUserScopeAsync();
			decimal? payrollReadiness = null;
			int payrollPending = 0;
			StaffAttendanceSnapshot? attendanceSnapshot = null;

			if (needsPrincipalTeacherAttendance)
			{
				var teacherAttendanceSnapshot = await BuildStaffAttendanceSnapshotAsync(
					scope,
					DateOnly.FromDateTime(toDate),
					teachingOnly: true);
				UpdateWidget(
					summary.KpiCards,
					"principal-teacher-attendance",
					null,
					$"{teacherAttendanceSnapshot.AttendanceRate:0.#}%",
					teacherAttendanceSnapshot.MissingAttendance == 0
						? $"{teacherAttendanceSnapshot.Present} teaching staff present, {teacherAttendanceSnapshot.Absent} absent"
						: $"{teacherAttendanceSnapshot.MissingAttendance} teaching staff attendance records missing",
					teacherAttendanceSnapshot.AttendanceRate >= 95 ? "success" : teacherAttendanceSnapshot.AttendanceRate >= 85 ? "warning" : "error");

				if (summary.DashboardTemplate == PrincipalAdmin)
				{
					return;
				}
			}

			if (needsPayroll || needsHrModule)
			{
				var payroll = await hrDashboardPayrollReader.BuildPayrollAsync(month);
				payrollReadiness = payroll.EmployeeCount == 0
					? 0
					: Math.Round((decimal)(payroll.ReviewedCount + payroll.LockedCount) / payroll.EmployeeCount * 100m, 1);
				payrollPending = payroll.PendingCount + payroll.MissingAttendanceCount + payroll.MissingSalaryStructureCount + payroll.PolicyConfigurationIssueCount;

				if (needsPayroll)
				{
					var subtitle = payrollPending == 0
						? "Payroll is ready for scoped employees"
						: BuildCountSubtitle(payrollPending, "payroll blocker in scope", "payroll blockers in scope");
					UpdateWidget(
						summary.KpiCards,
						"hr-payroll-readiness",
						null,
						$"{payrollReadiness:0.#}%",
						subtitle,
						payrollPending == 0 ? "success" : "warning");
				}
			}

			if (needsAttendance || needsHrModule)
			{
				attendanceSnapshot = await BuildStaffAttendanceSnapshotAsync(scope, DateOnly.FromDateTime(toDate));

				if (needsAttendance)
				{
					UpdateWidget(
						summary.KpiCards,
						"hr-staff-attendance",
						null,
						$"{attendanceSnapshot.AttendanceRate:0.#}%",
						attendanceSnapshot.MissingAttendance == 0
							? $"{attendanceSnapshot.Present} present, {attendanceSnapshot.Absent} absent"
							: $"{attendanceSnapshot.MissingAttendance} attendance records missing",
						attendanceSnapshot.AttendanceRate >= 95 ? "success" : attendanceSnapshot.AttendanceRate >= 85 ? "warning" : "error");
				}
			}

			if (needsHrModule)
			{
				var missingAttendance = attendanceSnapshot?.MissingAttendance ?? 0;
				var issueCount = payrollPending + missingAttendance;
				var attendanceRate = attendanceSnapshot?.AttendanceRate ?? 0m;
				var readiness = payrollReadiness ?? 0m;
				var subtitle = $"Payroll {readiness:0.#}%, staff attendance {attendanceRate:0.#}%";
				UpdateWidget(
					summary.ModuleSnapshots,
					"hr-dashboard",
					issueCount,
					$"{readiness:0.#}%",
					subtitle,
					issueCount == 0 ? "success" : "warning",
					health: issueCount == 0 ? "good" : "attention");
			}
		}
		catch
		{
			SetZoneError(summary, Kpis, "HR snapshot could not be loaded.");
		}
	}

	private async Task<IQueryable<Guid>> GetScopedStudentIdsQueryAsync()
	{
		var students = await studentRepository.GetQueryableAsync();
		var scopedStudents = await studentScopeAccessService.ApplyCurrentUserScopeAsync(students);
		return scopedStudents.Select(s => s.Id);
	}

	private async Task<StaffAttendanceSnapshot> BuildStaffAttendanceSnapshotAsync(
		UserScopeContext scope,
		DateOnly date,
		bool teachingOnly = false)
	{
		var employeeQuery = await hrEmployeeScopeReader.GetScopedEmployeeQueryAsync(scope);
		var activeEmployeeIds = await asyncExecuter.ToListAsync(employeeQuery
			.Where(e =>
				e.EmployeeStatusType == EmployeeStatusType.Active &&
				(!teachingOnly || e.EmployeeGroup == EmployeeGroupType.Teaching))
			.Select(e => e.Id));

		if (activeEmployeeIds.Count == 0)
		{
			return new StaffAttendanceSnapshot(0, 0, 0, 0, 0m);
		}

		var attendanceQuery = await employeeAttendanceRepository.GetQueryableAsync();
		var attendanceCounts = await asyncExecuter.ToListAsync(attendanceQuery
			.Where(a => a.AttendanceDate == date && activeEmployeeIds.Contains(a.EmployeeId))
			.GroupBy(a => a.StatusType)
			.Select(g => new AttendanceStatusCountRow(g.Key, g.Count())));
		var present = attendanceCounts
			.Where(a => a.StatusType == EmployeeAttendanceStatusType.Present)
			.Sum(a => a.Count);
		var halfDay = attendanceCounts
			.Where(a => a.StatusType == EmployeeAttendanceStatusType.HalfDayLeave)
			.Sum(a => a.Count);
		var absent = attendanceCounts
			.Where(a => a.StatusType == EmployeeAttendanceStatusType.FullDayLeave)
			.Sum(a => a.Count);
		var recorded = present + halfDay + absent;
		var missing = Math.Max(0, activeEmployeeIds.Count - recorded);
		var attendanceRate = Math.Round(((decimal)present + (halfDay * 0.5m)) / activeEmployeeIds.Count * 100m, 1);

		return new StaffAttendanceSnapshot(present, absent, halfDay, missing, attendanceRate);
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

	private sealed record FinanceAmountRow(double Amount, Guid? StudentId, Guid? FinanceFeeAccountId);

	private sealed record AttendanceStatusCountRow(EmployeeAttendanceStatusType StatusType, int Count);

	private sealed record StaffAttendanceSnapshot(
		int Present,
		int Absent,
		int HalfDay,
		int MissingAttendance,
		decimal AttendanceRate);
}
