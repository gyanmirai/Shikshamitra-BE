using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace ShikshaMitra.HR.Reports;

public class PayrollSalaryDocumentRowReader(IRepository<PayslipDocument, Guid> documents,
	IRepository<SalarySlip, Guid> slips, IRepository<PayrollRunItemSnapshot, Guid> snapshots,
	HrReportEmployeeQueryService employees, IAsyncQueryableExecuter executer) : ITransientDependency
{
	public async Task<HrReportRowsResult> BuildAsync(HrReportRequestDto input, UserScopeContext scope, bool exportAllRows)
	{
		var statement = input.DataSource == HrReportDataSources.PayrollStatement;
		var from = statement ? input.Filters.FromDate : ParseMonth(input.Filters.Month);
		var to = statement ? input.Filters.ToDate : from;
		if (!from.HasValue || !to.HasValue || from > to || (statement && input.Filters.EmployeeIds?.Count != 1))
			throw new BusinessException("HR:Reports:PayrollDocumentPeriodRequired");
		var first = new DateOnly(from.Value.Year, from.Value.Month, 1);
		var last = new DateOnly(to.Value.Year, to.Value.Month, 1);
		if ((last.Year - first.Year) * 12 + last.Month - first.Month >= 120) throw new BusinessException("HR:Reports:PayrollDocumentRangeTooLong");
		var firstMonth = first.ToString("yyyy-MM");
		var lastMonth = last.ToString("yyyy-MM");
		var staff = HrReportEmployeeQueryService.ApplyEmployeeFilters(await employees.BuildScopedEmployeeQueryAsync(scope), input.Filters);
		var documentQuery = await documents.GetQueryableAsync();
		var currentDocuments = documentQuery.Where(x => x.TenantId == scope.TenantId &&
			!documentQuery.Any(later => later.SalarySlipId == x.SalarySlipId && later.Revision > x.Revision));
		var slipQuery = await slips.GetQueryableAsync();
		var official = from slip in slipQuery
					   join document in currentDocuments on slip.Id equals document.SalarySlipId
					   join employee in staff on slip.EmployeeId equals employee.Id
					   where slip.TenantId == scope.TenantId && (slip.Status == SalarySlipStatus.Finalized || slip.Status == SalarySlipStatus.Paid) &&
						   string.Compare(slip.MonthYear, firstMonth) >= 0 && string.Compare(slip.MonthYear, lastMonth) <= 0
					   select new DocumentRow
					   {
						   EmployeeId = slip.EmployeeId,
						   EmployeeCode = employee.EmployeeCode ?? "",
						   Month = slip.MonthYear,
						   FactsJson = document.FactsJson,
						   SnapshotJson = null,
						   TotalEarnings = slip.TotalEarning,
						   TotalDeduction = slip.TotalDeduction,
						   NetPay = slip.NetSalary,
						   EarningsJson = null,
						   DeductionsJson = null,
						   LeaveJson = null,
						   EmployeeName = null,
						   GrossSalary = null,
						   BasicSalary = null,
						   Salary = null,
						   PayableDays = null
					   };
		IQueryable<DocumentRow> query = official;
		if (input.Filters.IncludeDrafts)
		{
			var snapshotQuery = await snapshots.GetQueryableAsync();
			var drafts = from snapshot in snapshotQuery
						 join employee in staff on snapshot.EmployeeId equals employee.Id
						 where snapshot.TenantId == scope.TenantId && string.Compare(snapshot.MonthYear, firstMonth) >= 0 && string.Compare(snapshot.MonthYear, lastMonth) <= 0 &&
							 !slipQuery.Any(slip => slip.EmployeeId == snapshot.EmployeeId && slip.MonthYear == snapshot.MonthYear &&
								 (slip.Status == SalarySlipStatus.Finalized || slip.Status == SalarySlipStatus.Paid))
						 select new DocumentRow
						 {
							 EmployeeId = snapshot.EmployeeId,
							 EmployeeCode = snapshot.EmployeeIdentifier,
							 Month = snapshot.MonthYear,
							 FactsJson = null,
							 SnapshotJson = snapshot.StatutoryApplicationJson,
							 TotalEarnings = snapshot.NetPay + snapshot.TotalDeduction,
							 TotalDeduction = snapshot.TotalDeduction,
							 NetPay = snapshot.NetPay,
							 EarningsJson = snapshot.EarningComponentsJson,
							 DeductionsJson = snapshot.DeductionComponentsJson,
							 EmployeeName = snapshot.EmployeeName,
							 GrossSalary = snapshot.GrossSalary,
							 BasicSalary = snapshot.BasicSalary,
							 Salary = snapshot.FullGrossSalary,
							 PayableDays = snapshot.PayableDays,
							 LeaveJson = snapshot.LeaveUnitsByTypeJson
						 };
			query = official.Concat(drafts);
		}
		var total = await executer.CountAsync(query);
		var financialTotals = await executer.FirstOrDefaultAsync(query.GroupBy(_ => 1).Select(group => new
		{ Earnings = group.Sum(x => x.TotalEarnings), Deductions = group.Sum(x => x.TotalDeduction), Net = group.Sum(x => x.NetPay) }));
		var warnings = new List<string>();
		if (!statement)
		{
			var matching = await executer.CountAsync(staff);
			if (matching > total) warnings.Add($"{matching - total} matching staff have no saved payroll for this month and are unavailable. They are excluded from totals.");
		}
		var take = exportAllRows ? HrReportRequestProcessor.MaxExportRows : input.PageSize;
		var skip = exportAllRows ? 0 : (input.Page - 1) * input.PageSize;
		var sort = input.Sorts?.FirstOrDefault();
		var sorted = sort?.Field switch
		{
			"netPay" => sort.Direction == "desc" ? query.OrderByDescending(x => x.NetPay).ThenBy(x => x.EmployeeCode) : query.OrderBy(x => x.NetPay).ThenBy(x => x.EmployeeCode),
			"totalDeduction" => sort.Direction == "desc" ? query.OrderByDescending(x => x.TotalDeduction).ThenBy(x => x.EmployeeCode) : query.OrderBy(x => x.TotalDeduction).ThenBy(x => x.EmployeeCode),
			"totalEarnings" => sort.Direction == "desc" ? query.OrderByDescending(x => x.TotalEarnings).ThenBy(x => x.EmployeeCode) : query.OrderBy(x => x.TotalEarnings).ThenBy(x => x.EmployeeCode),
			"month" => sort.Direction == "desc" ? query.OrderByDescending(x => x.Month).ThenBy(x => x.EmployeeCode) : query.OrderBy(x => x.Month).ThenBy(x => x.EmployeeCode),
			"employeeCode" when sort.Direction == "desc" => query.OrderByDescending(x => x.EmployeeCode).ThenBy(x => x.Month),
			_ => query.OrderBy(x => x.EmployeeCode).ThenBy(x => x.Month)
		};
		var records = await executer.ToListAsync(statement ? sorted.Take(HrReportRequestProcessor.MaxExportRows) : sorted.Skip(skip).Take(take));
		var rows = records.Select(ToRow).ToList();
		if (statement)
		{
			var employee = await executer.FirstOrDefaultAsync(staff.Select(x => new { x.Id, x.EmployeeCode, x.FirstName, x.LastName }));
			if (employee == null) return new([], 0, true);
			var months = rows.Select(x => x["month"]).ToHashSet();
			for (var index = 0; index <= (last.Year - first.Year) * 12 + last.Month - first.Month; index++)
			{
				var month = first.AddMonths(index);
				if (!months.Contains(month.ToString("yyyy-MM"))) rows.Add(new()
				{
					["__employeeId"] = employee.Id.ToString(),
					["month"] = month.ToString("yyyy-MM"),
					["employeeCode"] = employee.EmployeeCode,
					["employeeName"] = $"{employee.FirstName} {employee.LastName}".Trim(),
					["status"] = "Unavailable"
				});
			}
			rows = rows.OrderBy(x => x["month"]).ToList();
			total = rows.Count;
			if (!exportAllRows) rows = rows.Skip(skip).Take(take).ToList();
		}
		return new(rows, total, true)
		{
			Warnings = warnings,
			Totals = financialTotals == null ? new() : new()
			{ ["totalEarnings"] = financialTotals.Earnings, ["totalDeduction"] = financialTotals.Deductions, ["netPay"] = financialTotals.Net }
		};
	}
	private static DateOnly? ParseMonth(string? month) => DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ? parsed : null;
	internal static Dictionary<string, string?> ToRow(DocumentRow row)
	{
		var facts = row.FactsJson == null ? null : JsonSerializer.Deserialize<SalarySlipDto>(row.FactsJson);
		var deductions = facts?.Deductions.Select(x => (x.Name, x.Amount)).ToList() ?? ReadLines(row.DeductionsJson);
		decimal Sum(params string[] names) => deductions.Where(x => names.Any(name => string.Equals(x.Name.Replace(" ", ""), name, StringComparison.OrdinalIgnoreCase))).Sum(x => x.Amount);
		bool Named(string name, params string[] aliases) => aliases.Any(alias => string.Equals(name.Replace(" ", ""), alias, StringComparison.OrdinalIgnoreCase));
		decimal? LegacyAmount(Guid? ruleId, Func<(string Name, decimal Amount), bool> matches)
		{
			var identified = deductions.Where(matches).ToList();
			return identified.Count > 0 ? identified.Sum(x => x.Amount) : ruleId.HasValue ? null : 0m;
		}
		var application = facts?.StatutoryApplication ?? (row.SnapshotJson == null ? null : JsonSerializer.Deserialize<ShikshaMitra.HR.Payroll.Statutory.PayrollStatutoryApplication>(row.SnapshotJson));
		var pf = application?.ProvidentFund.AppliedEmployeeAmount ?? LegacyAmount(facts?.ProvidentFundRuleId,
			x => x.Name.Contains("Provident", StringComparison.OrdinalIgnoreCase) || Named(x.Name, "PF", "EPF"));
		var esi = application?.EmployeeStateInsurance.AppliedEmployeeAmount ?? LegacyAmount(facts?.EmployeeStateInsuranceRuleId,
			x => x.Name.Contains("Insurance", StringComparison.OrdinalIgnoreCase) || Named(x.Name, "ESIC", "ESI"));
		var typedDeductions = facts?.StatutoryDeductionAmounts ?? (row.SnapshotJson == null || row.DeductionsJson == null ? null : ShikshaMitra.HR.Payroll.SalarySlips.SalarySlipProjection.ReadStatutoryDeductions(row.DeductionsJson));
		var pt = typedDeductions?.GetValueOrDefault("pt") ?? LegacyAmount(facts?.ProfessionalTaxRuleId,
			x => x.Name.Contains("Professional", StringComparison.OrdinalIgnoreCase) || Named(x.Name, "PT", "P.T."));
		var tds = typedDeductions?.GetValueOrDefault("tds") ?? Sum("TDS", "T.D.S.", "TaxDeductedAtSource");
		var leave = facts?.LeaveUnitsByType ?? (row.LeaveJson == null ? null : JsonSerializer.Deserialize<Dictionary<string, decimal>>(row.LeaveJson));
		string? Money(decimal? value) => value?.ToString("0.00", CultureInfo.InvariantCulture);
		return new()
		{
			["__components"] = HrReportingSavedComponents.Serialize(facts, row.EarningsJson, row.DeductionsJson),
			["__employeeId"] = row.EmployeeId.ToString(),
			["payrollDepartment"] = facts?.Department,
			["month"] = row.Month,
			["employeeCode"] = facts?.EmployeeCode ?? row.EmployeeCode,
			["employeeName"] = facts?.EmployeeName ?? row.EmployeeName,
			["designation"] = facts?.Designation,
			["dateOfJoining"] = facts?.DateOfJoining?.ToString("yyyy-MM-dd"),
			["bankAccount"] = facts?.MaskedAccountNumber,
			["salary"] = Money(facts?.FullGrossSalary ?? row.Salary),
			["grossSalary"] = Money(facts?.GrossSalary ?? row.GrossSalary),
			["basicSalary"] = Money(facts?.BasicSalary ?? row.BasicSalary),
			["totalEarnings"] = Money(row.TotalEarnings),
			["pfSalary"] = Money(application?.ProvidentFund is { Apply: true, Eligible: true } ? application.ProvidentFund.ContributionWages : application == null ? null : 0m),
			["pfNumber"] = facts?.EpfNumber ?? application?.Profile.PfNumber,
			["esicNumber"] = facts?.EmployeeStateInsuranceNumber ?? application?.Profile.EsicInsuranceNumber,
			["pf"] = Money(pf),
			["pt"] = Money(pt),
			["tds"] = Money(tds),
			["esic"] = Money(esi),
			["otherDeductions"] = Money(row.TotalDeduction - pf - pt - tds - esi),
			["totalDeduction"] = Money(row.TotalDeduction),
			["netPay"] = Money(row.NetPay),
			["casualLeaveDays"] = Money(leave?.GetValueOrDefault("CL")),
			["paidLeaveDays"] = Money(leave == null ? null : leave.GetValueOrDefault("CL") + leave.GetValueOrDefault("PAID")),
			["unpaidLeaveDays"] = Money(leave?.GetValueOrDefault("UNPAID")),
			["payableDays"] = Money(facts?.PayableDays ?? row.PayableDays),
			["status"] = facts == null ? "Preview" : "Issued"
		};
	}
	private static List<(string Name, decimal Amount)> ReadLines(string? json)
	{
		if (json == null) return [];
		using var document = JsonDocument.Parse(json);
		return document.RootElement.EnumerateArray().Select(line =>
		{
			var properties = line.EnumerateObject().ToDictionary(x => x.Name, x => x.Value, StringComparer.OrdinalIgnoreCase);
			return (properties["name"].GetString() ?? "", properties["amount"].GetDecimal());
		}).ToList();
	}
	internal class DocumentRow
	{
		public Guid EmployeeId { get; set; }
		public string EmployeeCode { get; set; } = "";
		public string Month { get; set; } = "";
		public string? EmployeeName { get; set; }
		public string? FactsJson { get; set; }
		public string? SnapshotJson { get; set; }
		public string? EarningsJson { get; set; }
		public string? DeductionsJson { get; set; }
		public string? LeaveJson { get; set; }
		public decimal? Salary { get; set; }
		public decimal? BasicSalary { get; set; }
		public decimal? GrossSalary { get; set; }
		public decimal? PayableDays { get; set; }
		public decimal TotalEarnings { get; set; }
		public decimal TotalDeduction { get; set; }
		public decimal NetPay { get; set; }
	}
}