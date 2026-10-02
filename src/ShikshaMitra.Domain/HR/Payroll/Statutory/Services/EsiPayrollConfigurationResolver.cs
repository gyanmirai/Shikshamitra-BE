using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.HR.Payroll.Statutory;

public class EsiPayrollConfigurationResolver(
	IRepository<EsiPayrollConfiguration, Guid> configurations,
	IPayrollInputRevisionStore revisions, ICurrentTenant tenant,
	IGuidGenerator guids, IUnitOfWorkManager units) : ITransientDependency
{
	public const string BaselineKey = "ESIC_DEFAULT_ON_V1";
	// Calculation baseline, never a legal registration or coverage date.
	public static readonly DateTime BaselineMonth = new(1900, 1, 1);
	public async Task EnsureDefaultAsync()
	{
		if (tenant.Id == null) return;
		if (await configurations.AnyAsync()) return;
		if (units.Current?.Options.IsTransactional == true) { await InsertDefaultAsync(); return; }
		using var unit = units.Begin(requiresNew: true, isTransactional: true);
		await InsertDefaultAsync();
		await unit.CompleteAsync();
	}
	private async Task InsertDefaultAsync()
	{
		await revisions.LockAsync();
		if (!await configurations.AnyAsync())
			await configurations.InsertAsync(new EsiPayrollConfiguration(guids.Create(), tenant.Id,
				BaselineMonth, true, null, null, BaselineKey), autoSave: true);
	}
	public async Task<EsiPayrollConfiguration?> ResolveAsync(DateTime month)
	{
		await EnsureDefaultAsync();
		var monthStart = new DateTime(month.Year, month.Month, 1);
		return (await configurations.GetListAsync(x => !x.IsCancelled && x.EffectiveMonth <= monthStart))
			.OrderByDescending(x => x.EffectiveMonth).FirstOrDefault();
	}
}
