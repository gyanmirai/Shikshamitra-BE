using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities.Events;
using Volo.Abp.EventBus;

namespace ShikshaMitra.Domain.Finance.Accounts.Settings;

public class AcademicYearNoneCategoryHandler(NoneCategoryInitializer initializer)
	: ILocalEventHandler<EntityCreatedEventData<AcademicYear>>, ITransientDependency
{
	public Task HandleEventAsync(EntityCreatedEventData<AcademicYear> eventData) =>
		initializer.InitializeAsync(eventData.Entity);
}
