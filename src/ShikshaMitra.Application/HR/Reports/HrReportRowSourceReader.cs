using ShikshaMitra.Security.UserScope;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.HR.Reports;

public class HrReportRowSourceReader(
	HrReportEmployeeRowReader employeeRowReader,
	HrReportEmployeePayrollRowReader employeePayrollRowReader,
	HrReportAttendanceRowReader attendanceRowReader,
	HrReportLeaveRowReader leaveRowReader,
	HrReportPerformanceRowReader performanceRowReader,
	HrReportPolicyRowReader policyRowReader) : ITransientDependency
{
	public async Task<HrReportRowsResult> BuildRowsResultAsync(
		string source,
		HrReportRequestDto input,
		UserScopeContext scope,
		bool exportAllRows)
	{
		if (source == HrReportDataSources.Employee && HrReportDatabaseBackedCapabilities.CanUseEmployeeGroup(input))
		{
			return await employeeRowReader.BuildGroupedRowsResultAsync(input, scope, exportAllRows);
		}

		if (source == HrReportDataSources.Payroll && HrReportDatabaseBackedCapabilities.CanUsePayrollGroup(input))
		{
			return await employeePayrollRowReader.BuildPayrollGroupedRowsResultAsync(input, scope, exportAllRows);
		}

		if (source == HrReportDataSources.Attendance && HrReportDatabaseBackedCapabilities.CanUseAttendanceGroup(input))
		{
			return await attendanceRowReader.BuildGroupedRowsResultAsync(input, scope, exportAllRows);
		}

		if (source == HrReportDataSources.Leave && HrReportDatabaseBackedCapabilities.CanUseLeaveGroup(input))
		{
			return await leaveRowReader.BuildGroupedRowsResultAsync(input, scope, exportAllRows);
		}

		if (source == HrReportDataSources.Performance && HrReportDatabaseBackedCapabilities.CanUsePerformanceGroup(input))
		{
			return await performanceRowReader.BuildGroupedRowsResultAsync(input, scope, exportAllRows);
		}

		if (source == HrReportDataSources.Policies && HrReportDatabaseBackedCapabilities.CanUsePolicyGroup(input))
		{
			return await policyRowReader.BuildGroupedRowsResultAsync(input, scope, exportAllRows);
		}

		if (HrReportDatabaseBackedCapabilities.CanUseTable(input))
		{
			return source switch
			{
				HrReportDataSources.Employee => await employeeRowReader.BuildRowsResultAsync(input, scope, exportAllRows),
				HrReportDataSources.Payroll => await employeePayrollRowReader.BuildPayrollRowsResultAsync(input, scope, exportAllRows),
				HrReportDataSources.Attendance => await attendanceRowReader.BuildRowsResultAsync(input, scope, exportAllRows),
				HrReportDataSources.Leave => await leaveRowReader.BuildRowsResultAsync(input, scope, exportAllRows),
				HrReportDataSources.Performance => await performanceRowReader.BuildRowsResultAsync(input, scope, exportAllRows),
				HrReportDataSources.Policies => await policyRowReader.BuildRowsResultAsync(input, scope, exportAllRows),
				_ => new HrReportRowsResult(await BuildRowsAsync(source, input, scope), 0, false)
			};
		}

		var rows = await BuildRowsAsync(source, input, scope);
		return new HrReportRowsResult(rows, rows.Count, false);
	}

	public Task<List<Dictionary<string, string?>>> BuildRowsAsync(
		string source,
		HrReportRequestDto input,
		UserScopeContext scope) =>
		source switch
		{
			HrReportDataSources.Payroll => employeePayrollRowReader.BuildPayrollRowsAsync(input, scope),
			HrReportDataSources.Attendance => attendanceRowReader.BuildRowsAsync(input, scope),
			HrReportDataSources.Leave => leaveRowReader.BuildRowsAsync(input, scope),
			HrReportDataSources.Policies => policyRowReader.BuildRowsAsync(input, scope),
			HrReportDataSources.Performance => performanceRowReader.BuildRowsAsync(input, scope),
			_ => employeeRowReader.BuildRowsAsync(input, scope)
		};
}

public record HrReportRowsResult(
	List<Dictionary<string, string?>> Rows,
	int TotalCount,
	bool IsDatabaseBacked);
