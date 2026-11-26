using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.HR;

public class HrUserScopeCertificationGuardrail_Tests
{
	private static readonly Regex RawScopeMemberAccess = new(
		@"\bscope\.(AcademicYearIds|BoardIds|MediumIds|MediumStageIds|StandardIds|DivisionIds|WildcardBoardAcademicYearIds|WildcardMediumBoardIds|WildcardMediumStageMediumIds|WildcardStandardMediumStageIds|WildcardDivisionStandardIds|HasWildcard\w+|CanAccessMediumStage|HasTenantWideScope|HasAnyAccess|IsAdmin)",
		RegexOptions.Compiled);

	private static readonly HashSet<string> DirectUserScopeServiceAllowList = new(StringComparer.OrdinalIgnoreCase)
	{
		"AttendanceAndLeaves/AttendanceRegister/AttendanceRegisterReader.cs",
		"AttendanceAndLeaves/AttendanceTracking/AttendanceTrackingAccessService.cs",
		"AttendanceAndLeaves/Settings/LeavePolicyImpactDetailReader.cs",
		"AttendanceAndLeaves/TakeAttendance/TakeAttendanceAccessService.cs",
		"Audit/HrAuditTrailAccessService.cs",
		"Calendar/CalendarView/CalendarViewVisibilityService.cs",
		"Dashboard/HRDashboardScopeService.cs",
		"Employees/AcademicPermissions/EmployeeAcademicPermissionAppService.cs",
		"Employees/EmployeeAuthorizationService.cs",
		"Employees/ReportingManager/ReportingManagerAppService.cs",
		"Employees/Search/EmployeeSearchVisibilityService.cs",
		"Payroll/AttendanceImports/AttendanceImportLogReader.cs",
		"Payroll/RunPayroll/PayrollApprovalAccess.cs",
		"Payroll/RunPayroll/PayrollScopedEmployeeResolver.cs",
		"Payroll/RunPayroll/PayrollSectionScope.cs",
		"Payroll/SalarySlips/SalarySlipAccessService.cs",
		"Payroll/Statutory/EsiConfigurationAppService.cs",
		"Payroll/Statutory/EsiPolicyAppService.cs",
		"Payroll/Statutory/EsiStatutoryAppService.cs",
		"Payroll/Statutory/PfCoverageAppService.cs",
		"Policies/HrSetupReviewSampleReader.cs",
		"ProfessionalManagement/ProfessionalManagementReadScopeService.cs",
		"Reports/HrReportGenerator.cs",
		"Reports/HrReportingCapturedAccess.cs",
		"Reports/HrReportingWorkspaceAppService.cs",
		"Reports/HrTableExportAccess.cs",
		"Setup/HrSetupPayrollReadinessReader.cs"
	};

	private static readonly HashSet<string> RawScopeAccessAllowList = new(StringComparer.OrdinalIgnoreCase)
	{
		"AttendanceAndLeaves/TakeAttendance/TakeAttendanceAccessService.cs",
		"Dashboard/HRDashboardScopeService.cs",
		"Employees/AcademicPermissions/EmployeeAcademicPermissionAppService.cs",
		"Employees/HrEmployeeScopeQueryService.cs",
		"HrUserScopeAccess.cs",
		"HrUserScopeAccessService.cs",
		"Reports/HrReportingWorkspaceAppService.cs"
	};

	[Fact]
	public void Hr_Code_Should_Inject_IUserScopeService_Only_In_Approved_Gateways()
	{
		var violations = EnumerateHrApplicationFiles()
			.Where(file => !DirectUserScopeServiceAllowList.Contains(file.RelativePath))
			.SelectMany(file => File.ReadLines(file.FullPath)
				.Select((line, index) => new { file.RelativePath, LineNumber = index + 1, Line = line })
				.Where(row => row.Line.Contains("IUserScopeService", StringComparison.Ordinal)))
			.Select(row => $"{row.RelativePath}:{row.LineNumber}: {row.Line.Trim()}")
			.ToList();

		violations.ShouldBeEmpty();
	}

	[Fact]
	public void Hr_Code_Should_Not_Inspect_Raw_UserScopeContext_Collections_Outside_Gateways()
	{
		var violations = EnumerateHrApplicationFiles()
			.Where(file => !RawScopeAccessAllowList.Contains(file.RelativePath))
			.SelectMany(file => File.ReadLines(file.FullPath)
				.Select((line, index) => new { file.RelativePath, LineNumber = index + 1, Line = line })
				.Where(row => RawScopeMemberAccess.IsMatch(row.Line)))
			.Select(row => $"{row.RelativePath}:{row.LineNumber}: {row.Line.Trim()}")
			.ToList();

		violations.ShouldBeEmpty();
	}

	[Fact]
	public void Hr_Production_Code_Should_Not_Use_Static_Stage_Only_Access_Check()
	{
		var violations = EnumerateHrApplicationFiles()
			.SelectMany(file => File.ReadLines(file.FullPath)
				.Select((line, index) => new { file.RelativePath, LineNumber = index + 1, Line = line })
				.Where(row => row.Line.Contains("HrUserScopeAccess.CanAccessEmployeeStage(", StringComparison.Ordinal)))
			.Select(row => $"{row.RelativePath}:{row.LineNumber}: {row.Line.Trim()}")
			.ToList();

		violations.ShouldBeEmpty();
	}

	private static IReadOnlyList<(string FullPath, string RelativePath)> EnumerateHrApplicationFiles()
	{
		var hrRoot = Path.GetFullPath(Path.Combine(
			AppContext.BaseDirectory,
			"..",
			"..",
			"..",
			"..",
			"..",
			"src",
			"ShikshaMitra.Application",
			"HR"));

		return Directory
			.GetFiles(hrRoot, "*.cs", SearchOption.AllDirectories)
			.Select(file => (file, Path.GetRelativePath(hrRoot, file).Replace(Path.DirectorySeparatorChar, '/')))
			.ToList();
	}
}
