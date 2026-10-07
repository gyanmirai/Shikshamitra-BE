using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.Statutory;

public class EsiEnrollmentResolver(IRepository<EmployeeStatutoryEnrollment, Guid> enrollments,
	IRepository<PayrollRunItemSnapshot, Guid> snapshots,
	IRepository<SalarySlip, Guid> slips, IRepository<PayslipCorrection, Guid> corrections) : ITransientDependency
{
	public async Task<Dictionary<Guid, EmployeeStatutoryEnrollment>> ResolveAsync(DateTime month, IReadOnlyCollection<Guid> employeeIds)
	{
		month = new DateTime(month.Year, month.Month, 1);
		var ids = employeeIds.ToList();
		var result = (await enrollments.GetListAsync(x => x.SchemeCode == "ESI" && ids.Contains(x.EmployeeId)))
			.ToDictionary(x => x.EmployeeId);
		var (start, end) = EsiStatutoryCalculator.GetContributionPeriod(month);
		var months = Enumerable.Range(0, 6).Select(x => start.AddMonths(x)).Where(x => x < month)
			.Select(x => x.ToString("yyyy-MM", CultureInfo.InvariantCulture)).ToList();
		var finalized = await slips.GetListAsync(x => Enumerable.Contains(ids, x.EmployeeId) &&
			Enumerable.Contains(months, x.MonthYear) &&
			(x.Status == SalarySlipStatus.Finalized || x.Status == SalarySlipStatus.Paid));
		var finalizedMonths = finalized.Select(x => (x.EmployeeId, x.MonthYear)).ToHashSet();
		var settled = (await corrections.GetListAsync(x => ids.Contains(x.EmployeeId) && months.Contains(x.SourceMonth) &&
			x.Status == "APPLIED" && x.SettlementMonth != null)).GroupBy(x => (x.EmployeeId, x.SourceMonth))
			.Select(x => x.OrderByDescending(y => y.CompletedAt).ThenByDescending(y => y.Id).First()).ToList();
		var settledMonths = settled.Select(x => (x.EmployeeId, x.SourceMonth)).ToHashSet();
		var locked = await snapshots.GetListAsync(x => ids.Contains(x.EmployeeId) && x.IsLocked &&
			months.Contains(x.MonthYear) && x.EmployeeStateInsuranceRuleId != null &&
			(x.EmployeeStateInsuranceCoverageDecision == "WITHIN_WAGE_CEILING" ||
			 x.EmployeeStateInsuranceCoverageDecision == "CONTRIBUTION_PERIOD_CONTINUATION" ||
			 x.EmployeeStateInsuranceCoverageDecision == "FORCE_COVERED_OVERRIDE"));
		// An immutable payslip owns that employee-month's evidence, even if its snapshot differs.
		var history = finalized.Where(x => !settledMonths.Contains((x.EmployeeId, x.MonthYear)) && x.EmployeeStateInsuranceRuleId != null &&
				x.EmployeeStateInsuranceCoverageDecision is "WITHIN_WAGE_CEILING" or
					"CONTRIBUTION_PERIOD_CONTINUATION" or "FORCE_COVERED_OVERRIDE")
			.Select(x => new { x.EmployeeId, x.TenantId, x.MonthYear, x.EmployeeStateInsuranceNumber, x.ConcurrencyStamp })
			.Concat(locked.Where(x => !finalizedMonths.Contains((x.EmployeeId, x.MonthYear)))
				.Select(x => new { x.EmployeeId, x.TenantId, x.MonthYear, x.EmployeeStateInsuranceNumber, x.ConcurrencyStamp }))
			.Concat(settled.Select(x => new { Correction = x, Candidate = JsonSerializer.Deserialize<RunPayrollItem>(x.CandidateJson!)! })
				.Where(x => x.Candidate.EmployeeStateInsuranceRuleId != null && x.Candidate.EmployeeStateInsuranceCoverageDecision is
					"WITHIN_WAGE_CEILING" or "CONTRIBUTION_PERIOD_CONTINUATION" or "FORCE_COVERED_OVERRIDE")
				.Select(x => new { x.Correction.EmployeeId, x.Correction.TenantId, MonthYear = x.Correction.SourceMonth,
					x.Candidate.EmployeeStateInsuranceNumber, x.Correction.ConcurrencyStamp }))
			.ToLookup(x => x.EmployeeId);
		foreach (var employeeId in ids)
		{
			var source = result.GetValueOrDefault(employeeId);
			var prior = history[employeeId].OrderBy(x =>
				DateTime.ParseExact(x.MonthYear, "yyyy-MM", CultureInfo.InvariantCulture)).FirstOrDefault();
			var enteredThisPeriod = source?.CoveredFrom >= start && source?.CoveredFrom <= month;
			if (prior == null && !enteredThisPeriod) continue;
			source ??= new EmployeeStatutoryEnrollment(Guid.Empty, prior!.TenantId, employeeId, "ESI")
			{ ConcurrencyStamp = prior.ConcurrencyStamp };
			var projected = new EmployeeStatutoryEnrollment(source.Id, source.TenantId, source.EmployeeId, source.SchemeCode);
			projected.Update(source.Status == EmployeeStatutoryEnrollmentStatus.RegistrationPending
				? source.Status : EmployeeStatutoryEnrollmentStatus.Covered,
				source.WageCeilingCategory, source.InsuranceNumber ?? prior?.EmployeeStateInsuranceNumber,
				source.CoveredFrom ?? start, source.CoveredTo, end, source.StatusReason,
				source.EmployerShareReliefEligible, source.EmployerShareReliefFrom, source.EmployerShareReliefTo,
				source.DisabilityEvidenceReference);
			projected.ConcurrencyStamp = source.ConcurrencyStamp;
			result[employeeId] = projected;
		}
		return result;
	}
}
