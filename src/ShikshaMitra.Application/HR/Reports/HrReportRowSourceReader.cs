using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.ProfessionalManagement;
using ShikshaMitra.Security.UserScope;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Reports;

using static HrReportRowFormatting;

public class HrReportRowSourceReader(
	IRepository<EmployeeAttendance, Guid> attendanceRepository,
	IRepository<EmployeeLeave, Guid> leaveRepository,
	IRepository<PerformanceReviewAssignment, Guid> performanceAssignmentRepository,
	IRepository<ProfessionalReviewCycle, Guid> reviewCycleRepository,
	HrReportEmployeeQueryService employeeQueryService,
	HrReportEmployeePayrollRowReader employeePayrollRowReader,
	HrReportPolicyRowReader policyRowReader,
	IAsyncQueryableExecuter asyncExecuter) : ITransientDependency
{
	public Task<List<Dictionary<string, string?>>> BuildRowsAsync(
		string source,
		HrReportRequestDto input,
		UserScopeContext scope) =>
		source switch
		{
			HrReportDataSources.Payroll => employeePayrollRowReader.BuildPayrollRowsAsync(input, scope),
			HrReportDataSources.Attendance => BuildAttendanceRowsAsync(input, scope),
			HrReportDataSources.Leave => BuildLeaveRowsAsync(input, scope),
			HrReportDataSources.Policies => policyRowReader.BuildRowsAsync(input),
			HrReportDataSources.Performance => BuildPerformanceRowsAsync(input, scope),
			_ => employeePayrollRowReader.BuildEmployeeRowsAsync(input, scope)
		};

	private async Task<List<Dictionary<string, string?>>> BuildAttendanceRowsAsync(HrReportRequestDto input, UserScopeContext scope)
	{
		var employeeQuery = HrReportEmployeeQueryService.ApplyEmployeeFilters(await employeeQueryService.BuildScopedEmployeeQueryAsync(scope), input.Filters);
		var attendanceQuery = HrReportDateFilter.ApplyDateOnlyRange(await attendanceRepository.GetQueryableAsync(), input.Filters, x => x.AttendanceDate);
		var rows = await asyncExecuter.ToListAsync((
			from attendance in attendanceQuery
			join employee in employeeQuery on attendance.EmployeeId equals employee.Id
			select new
			{
				attendance.AttendanceDate,
				employee.EmployeeCode,
				employee.EmployeeName,
				employee.FirstName,
				employee.LastName,
				employee.Department,
				Status = attendance.StatusType,
				attendance.TimeIn,
				attendance.TimeOut,
				attendance.TotalHoursWorked,
				attendance.Remarks
			}).OrderByDescending(x => x.AttendanceDate).ThenBy(x => x.EmployeeCode).Take(HrReportRequestProcessor.MaxExportRows));

		return rows.Select(r => Row(
			("attendanceDate", FormatDate(r.AttendanceDate)),
			("employeeCode", r.EmployeeCode),
			("employeeName", DisplayName(r.EmployeeName, r.FirstName, r.LastName)),
			("department", r.Department),
			("status", r.Status.ToString()),
			("timeIn", FormatDateTime(r.TimeIn)),
			("timeOut", FormatDateTime(r.TimeOut)),
			("hoursWorked", r.TotalHoursWorked?.ToString("0.##", CultureInfo.InvariantCulture)),
			("remarks", r.Remarks))).ToList();
	}

	private async Task<List<Dictionary<string, string?>>> BuildLeaveRowsAsync(HrReportRequestDto input, UserScopeContext scope)
	{
		var employeeQuery = HrReportEmployeeQueryService.ApplyEmployeeFilters(await employeeQueryService.BuildScopedEmployeeQueryAsync(scope), input.Filters);
		var leaveQuery = HrReportDateFilter.ApplyDateOnlyRange(await leaveRepository.GetQueryableAsync(), input.Filters, x => x.StartDateOnly);
		var rows = await asyncExecuter.ToListAsync((
			from leave in leaveQuery
			join employee in employeeQuery on leave.EmployeeId equals employee.Id
			select new
			{
				leave.PublicId,
				employee.EmployeeCode,
				employee.EmployeeName,
				employee.FirstName,
				employee.LastName,
				LeaveType = leave.LeavePolicy != null ? leave.LeavePolicy.PolicyName : leave.LeavePolicyId.ToString(),
				leave.StartDateOnly,
				leave.EndDateOnly,
				leave.Status,
				leave.ApprovedLeaveUnits,
				leave.Description
			}).OrderByDescending(x => x.StartDateOnly).ThenBy(x => x.EmployeeCode).Take(HrReportRequestProcessor.MaxExportRows));

		return rows.Select(r => Row(
			("requestNo", r.PublicId.ToString(CultureInfo.InvariantCulture)),
			("employeeCode", r.EmployeeCode),
			("employeeName", DisplayName(r.EmployeeName, r.FirstName, r.LastName)),
			("leaveType", r.LeaveType),
			("startDate", FormatDate(r.StartDateOnly)),
			("endDate", FormatDate(r.EndDateOnly)),
			("status", r.Status.ToString()),
			("approvedUnits", FormatDecimal(r.ApprovedLeaveUnits)),
			("description", r.Description))).ToList();
	}

	private async Task<List<Dictionary<string, string?>>> BuildPerformanceRowsAsync(HrReportRequestDto input, UserScopeContext scope)
	{
		var employeeQuery = HrReportEmployeeQueryService.ApplyEmployeeFilters(await employeeQueryService.BuildScopedEmployeeQueryAsync(scope), input.Filters);
		var assignmentQuery = await performanceAssignmentRepository.GetQueryableAsync();
		var cycleQuery = await reviewCycleRepository.GetQueryableAsync();

		var rows = await asyncExecuter.ToListAsync((
			from assignment in assignmentQuery
			join employee in employeeQuery on assignment.EmployeeId equals employee.Id
			join cycle in cycleQuery on assignment.CycleId equals cycle.Id
			select new
			{
				Cycle = cycle.Name,
				employee.EmployeeCode,
				employee.EmployeeName,
				employee.FirstName,
				employee.LastName,
				employee.Department,
				assignment.Status,
				assignment.FinalScore,
				assignment.FinalRating,
				assignment.FinalizedAt
			}).OrderBy(x => x.Cycle).ThenBy(x => x.EmployeeCode).Take(HrReportRequestProcessor.MaxExportRows));

		return rows.Select(r => Row(
			("cycle", r.Cycle),
			("employeeCode", r.EmployeeCode),
			("employeeName", DisplayName(r.EmployeeName, r.FirstName, r.LastName)),
			("department", r.Department),
			("status", r.Status.ToString()),
			("finalScore", FormatDecimal(r.FinalScore)),
			("finalRating", r.FinalRating),
			("finalizedAt", FormatDateTime(r.FinalizedAt)))).ToList();
	}

}
