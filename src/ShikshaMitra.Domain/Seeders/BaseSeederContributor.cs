using System.Threading.Tasks;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

public abstract class BaseSeederContributor : ITransientDependency
{
	protected readonly ICurrentTenant CurrentTenant;
	protected readonly IGuidGenerator GuidGenerator;
	protected readonly IUnitOfWork CurrentUnitOfWork;

	protected BaseSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork currentUnitOfWork)
	{
		CurrentTenant = currentTenant;
		GuidGenerator = guidGenerator;
		CurrentUnitOfWork = currentUnitOfWork;
	}

	public abstract Task SeedAsync(DataSeedContext context);
}
