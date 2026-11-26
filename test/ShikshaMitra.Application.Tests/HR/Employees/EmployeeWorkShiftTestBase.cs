using System;
using System.Threading.Tasks;
using ShikshaMitra.HR.WorkShifts;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.HR.Employees;

public abstract class EmployeeWorkShiftTestBase : ShikshaMitraApplicationTestBase<ShikshaMitraApplicationTestModule>
{
	private readonly ICurrentTenant _tenant;
	private readonly IDisposable _tenantScope;
	protected IRepository<EmployeeWorkShift, Guid> WorkShifts { get; }

	protected EmployeeWorkShiftTestBase()
	{
		_tenant = GetRequiredService<ICurrentTenant>();
		_tenantScope = _tenant.Change(Guid.Parse("11111111-1111-1111-1111-111111111111"));
		WorkShifts = GetRequiredService<IRepository<EmployeeWorkShift, Guid>>();
	}

	public override void Dispose()
	{
		_tenantScope.Dispose();
		base.Dispose();
	}

	protected Task<Guid> EnsureWorkShiftAsync(string name, TimeOnly start, TimeOnly end) =>
		WithUnitOfWorkAsync(async () =>
		{
			var shift = await WorkShifts.FindAsync(x => x.ShiftName == name);
			return shift?.Id ?? await CreateWorkShiftAsync(name, start, end);
		});

	protected async Task<Guid> CreateWorkShiftAsync(string name, TimeOnly start, TimeOnly end)
	{
		var shift = new EmployeeWorkShift(GetRequiredService<IGuidGenerator>().Create(), _tenant.Id, name, start, end);
		await WorkShifts.InsertAsync(shift, autoSave: true);
		return shift.Id;
	}

	protected async Task<InitialWorkShiftInput> InitialWorkShiftAsync(Guid shiftId, DateOnly? joiningDate = null)
	{
		var today = await GetRequiredService<EmployeeShiftResolver>().TodayAsync();
		return new InitialWorkShiftInput
		{
			WorkShiftId = shiftId,
			EffectiveFrom = joiningDate > today ? joiningDate.Value : today
		};
	}

	protected Task EnsureSchoolWideActorAsync() =>
		WithUnitOfWorkAsync(() => HrSchoolScopeTestHelper.EnsureCurrentActorAsync(ServiceProvider));

	protected Task SetHistoricalWorkShiftAsync(Guid employeeId, Guid shiftId, DateOnly effectiveFrom) =>
		WithUnitOfWorkAsync(async () =>
		{
			var shift = await WorkShifts.GetAsync(shiftId);
			var assignments = GetRequiredService<IRepository<EmployeeShiftAssignment, Guid>>();
			await assignments.DeleteAsync(x => x.EmployeeId == employeeId, autoSave: true);
			await assignments.InsertAsync(new EmployeeShiftAssignment(
				GetRequiredService<IGuidGenerator>().Create(), employeeId, shift, effectiveFrom), autoSave: true);
		});

	protected Task SetFixtureEmployeeActiveAsync(Guid employeeId) =>
		WithUnitOfWorkAsync(async () =>
		{
			var employee = await GetRequiredService<EmployeeManager>()
				.UpdateEmployeeStatusAsync(employeeId, Common.EmployeeStatusType.Active, "Active HR test fixture");
			await GetRequiredService<IEmployeeRepository>().UpdateAsync(employee, autoSave: true);
		});
}
