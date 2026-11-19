using System.Threading.Tasks;
using ShikshaMitra.HR.Payroll.Statutory;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Seeds the minimum tenant payroll catalog required by salary templates.
/// This contributor also runs when a tenant is created after the initial database seed.
/// </summary>
public sealed class PayrollCatalogDataSeedContributor : IDataSeedContributor, ITransientDependency
{
	private readonly ICurrentTenant _currentTenant;
	private readonly HRSeederContributor _hrSeederContributor;
	private readonly EsiPayrollConfigurationResolver _esiConfiguration;

	public PayrollCatalogDataSeedContributor(
		ICurrentTenant currentTenant,
		HRSeederContributor hrSeederContributor, EsiPayrollConfigurationResolver esiConfiguration)
	{
		_currentTenant = currentTenant;
		_hrSeederContributor = hrSeederContributor;
		_esiConfiguration = esiConfiguration;
	}

	public async Task SeedAsync(DataSeedContext context)
	{
		if (_currentTenant.Id is null)
		{
			return;
		}

		await _hrSeederContributor.SeedPayrollCatalogDefaultsAsync();
		await _esiConfiguration.EnsureDefaultAsync();
	}
}
