using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public sealed class AdmissionCrmDemoScenarioSeederContributor : BaseSeederContributor
{
	internal const string StepName = "AdmissionCrmDemoScenarioSeeder";
	internal const string StepVersion = "AdmissionCrmDemoScenarioSeeder:v2";
	internal const int StepOrder = 173;

	private readonly AdmissionCrmSeederContributor _configurationSeeder;

	public AdmissionCrmDemoScenarioSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork currentUnitOfWork,
		AdmissionCrmSeederContributor configurationSeeder)
		: base(currentTenant, guidGenerator, currentUnitOfWork)
	{
		_configurationSeeder = configurationSeeder;
	}

	public override Task SeedAsync(DataSeedContext context) =>
		_configurationSeeder.SeedDemoScenariosAsync();
}
