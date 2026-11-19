using System;
using System.Linq;
using System.Text.Json;
using Volo.Abp;

namespace ShikshaMitra.HR.Reports;

internal static class PayrollIssuedReportBoundary
{
	internal static bool IsSalaryDocument(string? source) => source is HrReportDataSources.PayrollRegister or
		HrReportDataSources.PayrollStatement;

	internal static void EnsureCaptured(string source, HrReportingResult result)
	{
		if (IsSalaryDocument(source) && result.Rows.Any(row => row.TryGetValue("status", out var value) &&
			(value is JsonElement json ? json.ToString() : value?.ToString()) is "Draft" or "Preview"))
			throw new BusinessException(HRErrorCodes.PayslipReportRequiresIssued);
	}

	internal static void EnsureLegacyExport(HrReportRequestDto request)
	{
		EnsureLegacyPayslipRequest(request);
		if (IsSalaryDocument(request.DataSource) && request.Filters.IncludeDrafts)
			throw new BusinessException(HRErrorCodes.PayslipReportRequiresIssued);
	}

	internal static void EnsureLegacyPayslipRequest(HrReportRequestDto request)
	{
		var legacySource = string.IsNullOrWhiteSpace(request.DataSource) ||
			string.Equals(request.DataSource.Trim(), HrReportDataSources.Payroll, StringComparison.OrdinalIgnoreCase);
		var alias = string.Equals(request.ReportType?.Trim(), HrReportTypes.EmployeePayslip, StringComparison.OrdinalIgnoreCase) ||
			string.Equals(request.ReportType?.Trim(), HrReportTypes.SalaryRegister, StringComparison.OrdinalIgnoreCase);
		if (legacySource && alias) throw new BusinessException(HRErrorCodes.PayslipReportRequiresIssued);
	}

	internal static void EnsureLegacyCapture(string source, bool includesDrafts, bool issuedOnly)
	{
		if (IsSalaryDocument(source) && includesDrafts && !issuedOnly)
			throw new BusinessException(HRErrorCodes.PayslipReportRequiresIssued);
	}

	internal static bool IsIssuedOnly(HrReportRun run) => run.ExtraProperties.TryGetValue("issuedSalaryDocumentsOnly", out var marker) &&
		bool.TryParse(marker?.ToString(), out var value) && value;
}
