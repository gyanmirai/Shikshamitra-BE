using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ShikshaMitra.Common;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Templates;

namespace ShikshaMitra.Seeders;

public partial class HRSeederContributor
{
	internal async Task BackfillLegacyCompensationRevisionsAsync()
	{
		var employees = await _employeeRepository.GetListAsync();
		if (employees.Count == 0)
		{
			return;
		}

		var employeeIds = employees.Select(x => x.Id).ToList();
		var existingRevisions = await _compensationRevisionRepository.GetListAsync();
		var effectiveRevisionsByEmployee = existingRevisions
			.Where(x => x.Status == CompensationRevisionStatus.Approved)
			.ToLookup(x => x.EmployeeId);
		var nextRevisionNumberByEmployee = existingRevisions
			.GroupBy(x => x.EmployeeId)
			.ToDictionary(group => group.Key, group => group.Max(x => x.RevisionNumber) + 1);
		var componentsByEmployee = await _employeePayrollComponentRepository.GetByEmployeeIdsAsync(employeeIds);
		var detailsByEmployee = await _employeePayrollDetailsRepository.GetByEmployeeIdsAsync(employeeIds);
		var migratedCount = 0;
		var activeGapCount = 0;

		foreach (var employee in employees.Where(employee =>
		         !effectiveRevisionsByEmployee[employee.Id]
			         .Any(revision => revision.EffectiveMonth <= ResolveRequiredCoverageMonth(employee))))
		{
			var recurring = componentsByEmployee[employee.Id]
				.Where(x => !x.IsTemp && x.Amount >= 0m)
				.OrderByDescending(x => x.IsEarning)
				.ThenBy(x => x.Name)
				.ToList();
			var earnings = recurring.Where(x => x.IsEarning && x.Amount > 0m).ToList();
			if (earnings.Count == 0)
			{
				if (employee.EmployeeStatusType == EmployeeStatusType.Active)
				{
					activeGapCount++;
					_logger.LogWarning(
						"[EmployeeCompensationMigrationGap] Active employee {EmployeeId} has no effective compensation revision or recurring earning rows.",
						employee.Id);
				}
				continue;
			}

			detailsByEmployee.TryGetValue(employee.Id, out var details);
			var grossSalary = earnings.Sum(x => x.Amount);
			var deductionTotal = recurring.Where(x => !x.IsEarning).Sum(x => x.Amount);
			var netSalary = Math.Max(0m, grossSalary - deductionTotal);
			var salaryBasis = ParseLegacyBasis(details?.QuotedSalaryBasis);
			var protectionPolicy = salaryBasis switch
			{
				SalaryInputBasis.TakeHome => CompensationProtectionPolicy.ProtectTakeHome,
				SalaryInputBasis.EmployerCost => CompensationProtectionPolicy.ProtectEmployerCost,
				_ => CompensationProtectionPolicy.ProtectGross
			};
			var fallbackQuote = salaryBasis == SalaryInputBasis.TakeHome ? netSalary : grossSalary;
			var quotedAmount = details?.QuotedMonthlyAmount is > 0m ? details.QuotedMonthlyAmount.Value : fallbackQuote;
			var employerCost = salaryBasis == SalaryInputBasis.EmployerCost
				? Math.Max(grossSalary, quotedAmount)
				: grossSalary;
			var effectiveMonth = ResolveLegacyEffectiveMonth(employee, recurring);
			var migratedAt = details?.SalaryGeneratedAt ?? _clock.Now;
			var revisionNumber = nextRevisionNumberByEmployee.GetValueOrDefault(employee.Id, 1);
			var revision = new EmployeeCompensationRevision(
				GuidGenerator.Create(), CurrentTenant.Id, employee.Id, revisionNumber, effectiveMonth,
				$"legacy-components:{employee.Id:N}");
			var snapshot = JsonSerializer.Serialize(new
			{
				MigrationSource = "LEGACY_PAYROLL_COMPONENTS",
				MigratedAt = migratedAt,
				Warnings = new[] { "Migrated from recurring employee payroll rows. Statutory amounts are preserved as historical values." },
				Lines = recurring.Select(x => new
				{
					x.ComponentId,
					x.Name,
					x.Amount,
					x.IsEarning,
					SemanticType = x.SemanticType.ToString()
				})
			});
			var hasTemplateProvenance =
				details?.SourceSalaryTemplateId is { } templateId && templateId != Guid.Empty &&
				details.SourceSalaryTemplateVersionId is { } versionId && versionId != Guid.Empty &&
				details.SourceSalaryTemplateVersion is > 0;
			revision.ReplaceDraft(
				effectiveMonth,
				salaryBasis,
				protectionPolicy,
				quotedAmount,
				quotedAmount,
				grossSalary,
				netSalary,
				employerCost,
				hasTemplateProvenance ? details!.SourceSalaryTemplateId : null,
				hasTemplateProvenance ? details!.SourceSalaryTemplateVersionId : null,
				hasTemplateProvenance ? details!.SourceSalaryTemplateVersion : null,
				Guid.TryParse(details?.PfRule, out var pfRuleId) ? pfRuleId : null,
				"IN-GJ",
				snapshot);
			revision.MakeEffectiveFromLegacy(migratedAt);
			await _compensationRevisionRepository.InsertAsync(revision, autoSave: false);

			var lines = recurring.Select((component, index) => new EmployeeCompensationRevisionLine(
				GuidGenerator.Create(), CurrentTenant.Id, revision.Id, employee.Id,
				component.ComponentId, component.Name, component.Description, component.Amount,
				component.IsEarning, component.SemanticType, CompensationLineSource.Legacy, index)).ToList();
			await _compensationRevisionLineRepository.InsertManyAsync(lines, autoSave: false);
			migratedCount++;
		}

		if (migratedCount > 0 || activeGapCount > 0)
		{
			await CurrentUnitOfWork.SaveChangesAsync();
			_logger.LogInformation(
				"Legacy compensation migration created {MigratedCount} effective revisions and found {ActiveGapCount} unresolved active-employee payroll gaps.",
				migratedCount,
				activeGapCount);
		}
	}

	private DateTime ResolveRequiredCoverageMonth(Employee employee)
	{
		var now = _clock.Now;
		var currentMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		if (!employee.DateOfJoining.HasValue)
		{
			return currentMonth;
		}

		var joining = employee.DateOfJoining.Value;
		var joiningMonth = new DateTime(joining.Year, joining.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		return joiningMonth > currentMonth ? joiningMonth : currentMonth;
	}

	private DateTime ResolveLegacyEffectiveMonth(
		Employee employee,
		IReadOnlyCollection<EmployeePayrollComponent> components)
	{
		var componentMonth = components
			.Where(x => x.ApplicableMonth.HasValue)
			.Select(x => x.ApplicableMonth!.Value)
			.OrderBy(x => x)
			.FirstOrDefault();
		var source = componentMonth != default
			? componentMonth
			: employee.DateOfJoining.HasValue
				? employee.DateOfJoining.Value.ToDateTime(TimeOnly.MinValue)
				: _clock.Now;
		return new DateTime(source.Year, source.Month, 1, 0, 0, 0, DateTimeKind.Utc);
	}

	private static SalaryInputBasis ParseLegacyBasis(string? value) => value?.Trim().ToUpperInvariant() switch
	{
		"TAKE_HOME" => SalaryInputBasis.TakeHome,
		"EMPLOYER_COST" => SalaryInputBasis.EmployerCost,
		_ => SalaryInputBasis.Gross
	};
}
