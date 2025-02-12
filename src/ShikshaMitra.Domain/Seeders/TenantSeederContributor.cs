using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.ObjectExtending;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public class TenantSeederContributor : BaseSeederContributor
{
	private readonly ITenantManager _tenantManager;
	private readonly ITenantRepository _tenantRepository;
	private readonly IUnitOfWork _unitOfWork;
	private readonly IDataSeeder _dataSeeder;

	public TenantSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IDataSeeder dataSeeder,
		ITenantManager tenantManager,
		ITenantRepository tenantRepository)
		: base(currentTenant, guidGenerator, unitOfWork)
	{
		_tenantManager = tenantManager;
		_tenantRepository = tenantRepository;
		_unitOfWork = unitOfWork;
		_dataSeeder = dataSeeder;
	}

	public override async Task SeedAsync(DataSeedContext context)
	{
		if (await _tenantRepository.GetCountAsync() > 0)
		{
			return;
		}

		TenantCreateDto input = new TenantCreateDto()
		{
			Name = "DDVS",
			AdminEmailAddress = "ddvs@gmail.com",
			AdminPassword = "Temp@123",
			ExtraProperties =
				{
					{ "InstituteName", "DDVS" },
					{ "Address", "Parvat Patiya" },
					{ "City", "Surat" },
					{ "State", "Gujarat" },
					{ "Country", "India" },
					{ "PostalCode", "395010" },
					{ "ContactNo", "+91-9876543210" },
				}
		};

		var tenant = await _tenantManager.CreateAsync(input.Name);
		input.MapExtraPropertiesTo(tenant);

		await _tenantRepository.InsertAsync(tenant);

		await _unitOfWork.SaveChangesAsync();

		await _dataSeeder.SeedAsync(
			new DataSeedContext(tenant.Id)
				.WithProperty("AdminEmail", input.AdminEmailAddress)
				.WithProperty("AdminPassword", input.AdminPassword)
		);

		context.Properties["TenantId"] = tenant.Id;
	}
}
