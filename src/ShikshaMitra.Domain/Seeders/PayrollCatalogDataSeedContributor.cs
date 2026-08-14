using System.Threading.Tasks;
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

	public PayrollCatalogDataSeedContributor(
		ICurrentTenant currentTenant,
		HRSeederContributor hrSeederContributor)
	{
		_currentTenant = currentTenant;
		_hrSeederContributor = hrSeederContributor;
	}

	public async Task SeedAsync(DataSeedContext context)
	{
		if (_currentTenant.Id is null)
		{
			return;
		}

		await _hrSeederContributor.SeedPayrollCatalogDefaultsAsync();
	}
}
