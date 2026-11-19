using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Backfills the default PF rule and Gujarat professional-tax slabs for demo
/// tenants whose HR seeder completion marker predates effective-dated rules.
/// </summary>
public class PayrollStatutorySettingsBackfillSeederContributor : BaseSeederContributor
{
	internal const string StepName = "PayrollStatutorySettingsBackfillSeeder";
	// v3 repairs missing default ESIC baselines without changing existing policy history.
	internal const string StepVersion = "PayrollStatutorySettingsBackfillSeeder:v3";
	internal const int StepOrder = 161;

	private readonly HRSeederContributor _hrSeederContributor;

	public PayrollStatutorySettingsBackfillSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork currentUnitOfWork,
		HRSeederContributor hrSeederContributor)
		: base(currentTenant, guidGenerator, currentUnitOfWork)
	{
		_hrSeederContributor = hrSeederContributor;
	}

	public override Task SeedAsync(DataSeedContext context) =>
		_hrSeederContributor.SeedPayrollSettingsAsync();
}
