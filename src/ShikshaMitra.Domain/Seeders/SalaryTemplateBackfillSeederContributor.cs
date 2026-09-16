using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Backfills the default salary template for demo tenants whose HR seeder
/// completion marker predates salary-template support.
/// </summary>
public class SalaryTemplateBackfillSeederContributor : BaseSeederContributor
{
	internal const string StepName = "SalaryTemplateBackfillSeeder";
	internal const string StepVersion = "SalaryTemplateBackfillSeeder:v3";
	internal const int StepOrder = 162;

	private readonly HRSeederContributor _hrSeederContributor;

	public SalaryTemplateBackfillSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork currentUnitOfWork,
		HRSeederContributor hrSeederContributor)
		: base(currentTenant, guidGenerator, currentUnitOfWork)
	{
		_hrSeederContributor = hrSeederContributor;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		await _hrSeederContributor.SeedSalaryStructureTemplatesAsync();
		await _hrSeederContributor.BackfillLegacyCompensationRevisionsAsync();
	}
}
