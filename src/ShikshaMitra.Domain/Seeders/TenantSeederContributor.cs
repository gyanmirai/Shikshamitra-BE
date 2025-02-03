using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public class TenantSeederContributor : BaseSeederContributor
{
	private readonly ITenantManager _tenantManager;
	private readonly ITenantRepository _tenantRepository;

	public TenantSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		ITenantManager tenantManager,
		ITenantRepository tenantRepository)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_tenantManager = tenantManager;
		_tenantRepository = tenantRepository;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		if (await _tenantRepository.GetCountAsync() > 0)
		{
			return;
		}

		var tenant = await _tenantManager.CreateAsync("DDVS");
		tenant.ExtraProperties["InstituteName"] = "DDVS";
		tenant.ExtraProperties["Address"] = "Parvat Patiya";
		tenant.ExtraProperties["City"] = "Surat";
		tenant.ExtraProperties["State"] = "Gujarat";
		tenant.ExtraProperties["Country"] = "India";
		tenant.ExtraProperties["PostalCode"] = "395010";
		tenant.ExtraProperties["ContactNo"] = "+91-9876543210";

		await _tenantRepository.InsertAsync(tenant);
		await CurrentUnitOfWork.SaveChangesAsync();

		context.Properties["TenantId"] = tenant.Id;
	}
}
