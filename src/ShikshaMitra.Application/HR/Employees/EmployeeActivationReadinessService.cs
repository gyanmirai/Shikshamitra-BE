using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Compensation;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;
using Volo.Abp.Timing;

namespace ShikshaMitra.HR.Employees;

public class EmployeeActivationReadinessService(
	IEmployeeRepository employeeRepository,
	IRepository<EmployeeCompensationRevision, Guid> compensationRepository,
	IAsyncQueryableExecuter asyncExecuter,
	IClock clock) : ITransientDependency
{
	public async Task<EmployeeActivationReadinessDto> GetAsync(Guid employeeId)
	{
		var employee = await employeeRepository.GetAsync(employeeId)
			?? throw new EntityNotFoundException(typeof(Employee), employeeId);
		var currentMonth = new DateTime(clock.Now.Year, clock.Now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
		var joiningMonth = employee.DateOfJoining.HasValue
			? new DateTime(employee.DateOfJoining.Value.Year, employee.DateOfJoining.Value.Month, 1, 0, 0, 0, DateTimeKind.Utc)
			: currentMonth;
		var requiredMonth = joiningMonth > currentMonth ? joiningMonth : currentMonth;
		var revision = await asyncExecuter.FirstOrDefaultAsync(
			(await compensationRepository.GetQueryableAsync())
				.Where(x => x.EmployeeId == employeeId && x.Status == CompensationRevisionStatus.Approved && x.EffectiveMonth <= requiredMonth)
				.OrderByDescending(x => x.EffectiveMonth)
				.ThenByDescending(x => x.RevisionNumber));
		return new EmployeeActivationReadinessDto
		{
			CanActivate = revision != null,
			RequiredMonth = requiredMonth,
			CompensationRevisionId = revision?.Id,
			BlockerCodes = revision == null ? ["PAYROLL_SETUP_REQUIRED"] : []
		};
	}
}
