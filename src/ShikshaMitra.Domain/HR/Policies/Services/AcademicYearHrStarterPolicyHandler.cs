using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities.Events;
using Volo.Abp.EventBus;

namespace ShikshaMitra.HR.Policies;

/// <summary>Local pre-commit event: policy failure also fails academic-year creation.</summary>
public class AcademicYearHrStarterPolicyHandler(HrStarterPolicyInitializer initializer)
	: ILocalEventHandler<EntityCreatedEventData<AcademicYear>>, ITransientDependency
{
	public Task HandleEventAsync(EntityCreatedEventData<AcademicYear> eventData) =>
		initializer.InitializeAsync(eventData.Entity);
}
