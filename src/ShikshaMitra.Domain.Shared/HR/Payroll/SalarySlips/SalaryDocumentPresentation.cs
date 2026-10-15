using System;
using System.Collections.Generic;
using System.Linq;
using Volo.Abp;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

public sealed record SalaryDocumentPresentation
{
	public string Layout { get; init; } = "Detailed";
	public string? LogoBlobName { get; init; }
	public bool ShowStaffDetails { get; init; } = true;
	public bool ShowBankDetails { get; init; } = true;
	public bool ShowAttendance { get; init; } = true;
	public bool ShowEarningsBreakdown { get; init; } = true;
	public bool ShowZeroDeductions { get; init; }
	public List<string> StatementColumns { get; init; } = [];
	public List<string> RegisterColumns { get; init; } = [];

	public static readonly HashSet<string> Columns = new(StringComparer.Ordinal)
	{
		"month", "employeeCode", "employeeName", "designation", "dateOfJoining", "bankAccount",
		"salary", "grossSalary", "basicSalary", "pfSalary", "pfNumber", "esicNumber", "pf", "pt", "tds", "esic",
		"otherDeductions", "totalEarnings", "totalDeduction", "netPay", "casualLeaveDays", "paidLeaveDays", "unpaidLeaveDays", "payableDays", "status"
	};
	public void Validate(Guid? tenantId)
	{
		if (Layout is not ("Compact" or "Detailed") || StatementColumns == null || RegisterColumns == null || StatementColumns.Count > Columns.Count || RegisterColumns.Count > Columns.Count ||
			StatementColumns.Concat(RegisterColumns).Any(x => !Columns.Contains(x)))
			throw new BusinessException("HR:Payslip:PresentationInvalid");
		if (LogoBlobName != null && (!tenantId.HasValue || !LogoBlobName.StartsWith($"payroll-logos/{tenantId}/", StringComparison.Ordinal) ||
			LogoBlobName.Split('/').Length != 3 || !Guid.TryParseExact(LogoBlobName.Split('/').Last(), "N", out _)))
			throw new BusinessException("HR:Payslip:LogoInvalid");
	}
}

public sealed record FrozenSalaryDocumentPresentation(Guid VersionId, string SchoolName, string SchoolAddress,
	SalaryDocumentPresentation Options, string? LogoSha256);
