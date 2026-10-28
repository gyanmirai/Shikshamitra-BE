using System.Collections.Generic;
using System.Linq;
using ShikshaMitra.HR.Reports;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Reports;

public class PayrollIssuedReportBoundary_Tests
{
	[Theory]
	[InlineData("salary-register")]
	[InlineData("salary-statement")]
	public void Retired_saved_definition_parameter_normalizes_without_enabling_draft_runs(string starter)
	{
		var definition = HrReportingCatalogue.BuiltIns().Single(x => x.Id == "builtin:" + starter).Definition;
		definition.Parameters.Add("includeDrafts");
		HrReportingEngine.Validate(definition);
		definition.Parameters.ShouldNotContain("includeDrafts");
		var parameters = HrReportingEngine.ResolveParameters(definition, new() { Period = "month", Month = "2026-10", EmployeeIds = [System.Guid.NewGuid()] },
			System.DateTimeOffset.UtcNow, "Asia/Kolkata");
		parameters.IncludeDrafts.ShouldBeFalse();
		Should.Throw<BusinessException>(() => HrReportingEngine.ResolveParameters(definition,
			new() { Period = "month", Month = "2026-10", EmployeeIds = [System.Guid.NewGuid()], IncludeDrafts = true },
			System.DateTimeOffset.UtcNow, "Asia/Kolkata"));
	}

	[Fact]
	public void Retired_parameter_compatibility_does_not_accept_duplicates_or_other_datasets()
	{
		var salary = HrReportingCatalogue.BuiltIns().Single(x => x.Id == "builtin:salary-register").Definition;
		salary.Parameters.AddRange(["includeDrafts", "includeDrafts"]);
		Should.Throw<BusinessException>(() => HrReportingEngine.Validate(salary));
		var attendance = HrReportingCatalogue.BuiltIns().Single(x => x.Id == "builtin:in-out").Definition;
		attendance.Parameters.Add("includeDrafts");
		Should.Throw<BusinessException>(() => HrReportingEngine.Validate(attendance));
		attendance.Parameters.ShouldContain("includeDrafts");
	}
	[Theory]
	[InlineData(HrReportTypes.EmployeePayslip, null)]
	[InlineData(HrReportTypes.EmployeePayslip, "payroll")]
	[InlineData(HrReportTypes.SalaryRegister, null)]
	[InlineData(HrReportTypes.SalaryRegister, "payroll")]
	[InlineData(" employee payslip ", " PAYROLL ")]
	public void Legacy_payslip_aliases_cannot_enter_the_unlocked_snapshot_reader(string name, string? source)
	{
		var request = new HrReportRequestDto { ReportType = name, DataSource = source, Filters = new() { Month = "2026-10" } };
		Should.Throw<BusinessException>(() => new HrReportRequestValidationService().Validate(request))
			.Code.ShouldBe(HRErrorCodes.PayslipReportRequiresIssued);
		Should.Throw<BusinessException>(() => PayrollIssuedReportBoundary.EnsureLegacyExport(request))
			.Code.ShouldBe(HRErrorCodes.PayslipReportRequiresIssued);
	}

	[Fact]
	public void Explicit_salary_datasets_and_operational_payroll_preview_are_distinct()
	{
		Should.NotThrow(() => PayrollIssuedReportBoundary.EnsureLegacyPayslipRequest(new() { ReportType = HrReportTypes.EmployeePayslip, DataSource = HrReportDataSources.PayrollStatement }));
		Should.NotThrow(() => PayrollIssuedReportBoundary.EnsureLegacyPayslipRequest(new() { ReportType = HrReportTypes.MonthlyPayrollSummary, DataSource = HrReportDataSources.Payroll }));
		foreach (var source in new[] { HrReportDataSources.PayrollRegister, HrReportDataSources.PayrollStatement })
			HrReportingCatalogue.Datasets().Single(x => x.Id == source).Parameters.ShouldNotContain("includeDrafts");
	}
	[Theory]
	[InlineData(HrReportDataSources.PayrollRegister, "Draft", true)]
	[InlineData(HrReportDataSources.PayrollStatement, "Preview", true)]
	[InlineData(HrReportDataSources.PayrollRegister, "Preview", true)]
	[InlineData(HrReportDataSources.PayrollRegister, "Issued", false)]
	[InlineData(HrReportDataSources.PayrollStatement, "Unavailable", false)]
	[InlineData("attendance", "Draft", false)]
	public void Captured_salary_outputs_hide_draft_rows_and_preserve_other_reports(string source, string status, bool blocked)
	{
		var result = new HrReportingResult { Rows = [new Dictionary<string, object?> { ["status"] = status }] };
		var action = () => PayrollIssuedReportBoundary.EnsureCaptured(source, result);
		if (blocked) Should.Throw<BusinessException>(action).Code.ShouldBe(HRErrorCodes.PayslipReportRequiresIssued);
		else Should.NotThrow(action);
	}

	[Theory]
	[InlineData(HrReportDataSources.PayrollRegister, true, true)]
	[InlineData(HrReportDataSources.PayrollStatement, false, false)]
	[InlineData("attendance", true, false)]
	public void Legacy_download_guard_is_limited_to_salary_exports_that_requested_drafts(string source, bool drafts, bool blocked)
	{
		var action = () => PayrollIssuedReportBoundary.EnsureLegacyExport(new() { DataSource = source, Filters = new() { IncludeDrafts = drafts } });
		if (blocked) Should.Throw<BusinessException>(action).Code.ShouldBe(HRErrorCodes.PayslipReportRequiresIssued);
		else Should.NotThrow(action);
	}

	[Theory]
	[InlineData(HrReportDataSources.PayrollRegister, true, false, true)]
	[InlineData(HrReportDataSources.PayrollRegister, true, true, false)]
	[InlineData(HrReportDataSources.PayrollStatement, false, false, false)]
	[InlineData("attendance", true, false, false)]
	public void Paged_legacy_captures_cannot_bypass_the_boundary_with_empty_metadata_rows(string source, bool drafts, bool issuedOnly, bool blocked)
	{
		var action = () => PayrollIssuedReportBoundary.EnsureLegacyCapture(source, drafts, issuedOnly);
		if (blocked) Should.Throw<BusinessException>(action).Code.ShouldBe(HRErrorCodes.PayslipReportRequiresIssued);
		else Should.NotThrow(action);
	}
}
