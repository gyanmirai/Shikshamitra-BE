using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Services;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.HR.Payroll.Compensation;

[RemoteService(false)]
[Authorize]
public class SalarySummaryAppService(
	IEmployeeAuthorizationService access,
	IEmployeeRepository employees,
	PayrollCompensationRevisionResolver compensation,
	PayrollProvidentFundComponentBuilder providentFund,
	PayrollEsiContextResolver esi,
	EsiPayrollConfigurationResolver esiConfiguration,
	EmployeeStatutoryProfileResolver profiles,
	PfEligibilityResolver pfEligibility,
	IProfessionalTaxRepository professionalTax, PayrollEsiPolicyCompensationRecalculator esiCompensation) : ApplicationService, ISalarySummaryAppService
{
	public async Task<SalarySummaryDto> GetAsync(Guid employeeId, string month)
	{
		var permission = await AuthorizationService.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.Manage)
			? ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.Manage
			: ShikshaMitraSubmodulePermissions.HR.PayrollManagement_CompensationRevisions.View;
		await AuthorizationService.CheckAsync(permission);
		await access.CheckEmployeeScopedPermissionAsync(employeeId, permission);
		if (!DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
			throw new BusinessException("HR:Payroll:StatutoryProfileInvalidMonth");
		date = DateTime.SpecifyKind(date, DateTimeKind.Utc);
		var summary = new SalarySummaryDto { Month = month };
		var resolved = await compensation.ResolveAsync([employeeId], date.Year, date.Month, Array.Empty<EmployeePayrollComponent>().ToLookup(x => x.EmployeeId));
		if (!resolved.TryGetValue(employeeId, out var salary)) return summary;
		summary.RevisionId = salary.Revision.Id;
		summary.SalaryEffectiveMonth = salary.Revision.EffectiveMonth;
		summary.GrossSalary = salary.Revision.GrossSalary;
		summary.QuotedEmployerCost = salary.Revision.SalaryBasis == SalaryInputBasis.EmployerCost ? salary.Revision.QuotedMonthlyAmount : null;
		var profile = await profiles.ResolveAsync(employeeId, date);
		if (salary.Revision.EsiManagementMode == EsiManagement.External)
			profile = profile with { ApplyEsic = false, EsicDisabledReason = salary.Revision.EsiExternalReason };
		var components = await providentFund.AddProvidentFundDeductionAsync(employeeId, salary.Revision.PfRuleId, salary.Components, date.Year, date.Month);
		var pfRule = await providentFund.ResolveRuleAsync(salary.Revision.PfRuleId, employeeId, date.Year, date.Month);
		var slabs = await professionalTax.GetListAsync(x => x.IsActive && x.JurisdictionCode == salary.Revision.StatutoryProfile && x.EffectiveFrom <= date && (!x.EffectiveTo.HasValue || x.EffectiveTo >= date));
		var configuration = await esiConfiguration.ResolveAsync(date);
		PayrollEsiStatutoryContext? esiContext = null;
		try { esiContext = (await esi.ResolveAsync(date.Year, date.Month, [employeeId], components.ToLookup(x => x.EmployeeId), resolved)).GetValueOrDefault(employeeId); }
		catch (BusinessException error) when (error.Code is HRErrorCodes.StatutoryRuleNotFound or HRErrorCodes.EsiPolicyNotEffective) { summary.BlockingIssues.Add("ESI_POLICY_REQUIRED"); }
		if (esiContext != null)
			await esiCompensation.RecalculateAsync(resolved, new Dictionary<Guid, PayrollEsiStatutoryContext> { [employeeId] = esiContext },
				new Dictionary<Guid, PayrollStatutoryProfile> { [employeeId] = profile }, date);
		// Full-month estimate: no attendance, adjustments, snapshots, reviews, or payroll-run writes.
		var cessationMonth = EmployeeLifecycleEligibility.IsEmploymentCessationMonth(await employees.GetAsync(employeeId), date.Year, date.Month);
		var result = PayrollComputation.Calculate(0, 0, 1m, 1m, components,
			new PayrollStatutoryRuleSet(pfRule, slabs, cessationMonth, EmployeeStateInsurance: esiContext, EsiConfigurationId: configuration?.Id) { StatutoryProfile = profile });
		summary.GrossSalary = result.FullGrossSalary;
		var pfWages = components.Where(x => x.IsEarning && x.SemanticType is PayrollComponentSemanticType.Basic or PayrollComponentSemanticType.DearnessAllowance).Sum(x => x.Amount);
		var decision = await pfEligibility.ResolveAsync(employeeId, date.AddMonths(1).AddDays(-1), result.GrossSalary, pfWages, PfEligibilityResolver.Declaration(salary.Revision), salary.Revision.PfRuleId.HasValue);
		var includesPt = components.Any(x => x.SemanticType == PayrollComponentSemanticType.ProfessionalTax);
		var pt = slabs.FirstOrDefault(x => x.Id == result.ProfessionalTaxRuleId);
		var cessationExemption = includesPt && cessationMonth && pt == null && slabs.Any(x =>
			!x.DeductInCessationMonth && result.TotalEarnings >= x.FromSalary &&
			(!x.ToSalary.HasValue || result.TotalEarnings <= x.ToSalary));
		var ptSummary = SalaryStatutorySummary.ProfessionalTax(includesPt, pt, cessationExemption);
		summary.StatutorySummary = [SalaryStatutorySummary.Pf(result.StatutoryApplication?.ProvidentFund, decision, pfRule),
			SalaryStatutorySummary.Esi(result.EsiCalculation, result.StatutoryApplication?.EmployeeStateInsurance, configuration?.Enabled == true),
			ptSummary];
		var esiSummary = summary.StatutorySummary[1];
		if (salary.Revision.EsiManagementMode == EsiManagement.External)
			summary.StatutorySummary[1] = SalaryStatutorySummary.ExternalEsi(salary.Revision.EsiExternalReason);
		esiSummary.RuleId = result.EmployeeStateInsuranceRuleId;
		esiSummary.RuleVersion = result.EmployeeStateInsuranceRuleVersion;
		summary.BlockingIssues.AddRange(result.StatutoryIssueCodes.Where(PayrollLockPreconditionGuard.IsBlockingLockIssue));
		summary.BlockingIssues.AddRange(summary.StatutorySummary.Where(x => x.Status == "NEEDS_SETUP").Select(x => x.Scheme + "_SETUP_REQUIRED"));
		summary.BlockingIssues = summary.BlockingIssues.Distinct().ToList();
		summary.IsComplete = summary.BlockingIssues.Count == 0;
		summary.TotalDeductions = summary.IsComplete ? result.TotalDeduction : null;
		summary.NetSalary = summary.IsComplete ? result.NetPay : null;
		summary.EmployerCost = summary.IsComplete ? result.TotalEarnings + result.EmployerContributionTotal : null;
		summary.Earnings = result.Earnings.Select(x => new SalarySummaryLineDto(x.Source.Id.ToString(), x.Source.Name, x.Amount)).ToList();
		summary.Deductions = result.Deductions.Select(x => new SalarySummaryLineDto(x.Source.SemanticType.ToString(), x.Source.Name, x.Amount)).ToList();
		summary.EmployerContributions = result.EmployerContributions.Select(x => new SalarySummaryLineDto(x.RuleId.ToString(), x.Name, x.Amount)).ToList();
		return summary;
	}
}
