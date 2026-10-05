using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Compensation;
using ShikshaMitra.HR.Payroll.Statutory;
using ShikshaMitra.HR.Payroll.Templates;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;
using Volo.Abp.Timing;

namespace ShikshaMitra.HR.Employees;

public class EmployeeActivationReadinessService(
	IEmployeeRepository employeeRepository,
	IRepository<EmployeeCompensationRevision, Guid> compensationRepository,
	IRepository<EmployeeCompensationRevisionLine, Guid> lineRepository,
	SalaryTemplateEsiContextResolver esiContextResolver,
	IAsyncQueryableExecuter asyncExecuter,
	IClock clock) : ITransientDependency
{
	public async Task<EmployeeActivationReadinessDto> GetAsync(Guid employeeId)
	{
		var employee = await employeeRepository.GetAsync(employeeId)
			?? throw new EntityNotFoundException(typeof(Employee), employeeId);
		var localNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(clock.Now, DateTimeKind.Utc),
			TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"));
		var currentMonth = new DateTime(localNow.Year, localNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var joiningMonth = employee.DateOfJoining.HasValue
			? new DateTime(employee.DateOfJoining.Value.Year, employee.DateOfJoining.Value.Month, 1, 0, 0, 0, DateTimeKind.Utc)
			: currentMonth;
		var requiredMonth = joiningMonth > currentMonth ? joiningMonth : currentMonth;
		var revision = await asyncExecuter.FirstOrDefaultAsync(
			(await compensationRepository.GetQueryableAsync())
				.Where(x => x.EmployeeId == employeeId && x.Status == CompensationRevisionStatus.Approved && x.EffectiveMonth <= requiredMonth)
				.OrderByDescending(x => x.EffectiveMonth)
				.ThenByDescending(x => x.RevisionNumber));
		var blockers = revision == null
			? new List<string> { "PAYROLL_SETUP_REQUIRED" }
			: await GetEsiBlockersAsync(employeeId, requiredMonth, revision);
		return new EmployeeActivationReadinessDto
		{
			CanActivate = blockers.Count == 0,
			RequiredMonth = requiredMonth,
			CompensationRevisionId = revision?.Id,
			BlockerCodes = blockers
		};
	}

	private async Task<List<string>> GetEsiBlockersAsync(Guid employeeId, DateTime month, EmployeeCompensationRevision revision)
	{
		var resolution = await esiContextResolver.ResolveAsync(month, revision.StatutoryProfile, employeeId);
		if (resolution.Context is not { } context)
			return resolution.Warnings.ToList();

		// Re-evaluate coverage from agreed earnings and current registration facts.
		// The salary snapshot may have been saved while registration was pending or ESIC was off.
		var earnings = await lineRepository.GetListAsync(x => x.RevisionId == revision.Id && x.IsEarning);
		var wages = earnings.Select(line => new StatutoryWageLine(line.ComponentId ?? line.Id, line.Name, line.Amount,
			context.ComponentTreatments.GetValueOrDefault(line.ComponentId ?? line.Id, StatutoryWageTreatment.Unclassified))).ToList();
		var calculation = EsiStatutoryCalculator.Calculate(month, context.Rule, context.Establishment,
			context.Enrollment, wages, context.Overrides);
		var blockers = calculation.IssueCodes.ToList();
		if (calculation.IsCovered && string.IsNullOrWhiteSpace(context.Enrollment?.InsuranceNumber))
			blockers.Add(EsiStatutoryCalculator.InsuranceNumberIssue);
		return blockers.Distinct().ToList();
	}
}
