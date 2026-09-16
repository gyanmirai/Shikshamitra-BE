using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities.Events;
using Volo.Abp.EventBus;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace ShikshaMitra.Security.UserScope;

/// <summary>Invalidate local authorization after persisted identity, reporting or scope mutations.</summary>
public sealed class ScopeLocalReuseInvalidator(IUnitOfWorkManager manager) :
    ILocalEventHandler<EntityChangedEventData<UserScopeAssignment>>,
    ILocalEventHandler<EntityChangedEventData<Employee>>,
    ILocalEventHandler<EntityChangedEventData<IdentityUser>>,
    ILocalEventHandler<EntityChangedEventData<IdentityRole>>,
    ITransientDependency
{
    public Task HandleEventAsync(EntityChangedEventData<UserScopeAssignment> data) => ClearAsync();
    public Task HandleEventAsync(EntityChangedEventData<Employee> data) => ClearAsync();
    public Task HandleEventAsync(EntityChangedEventData<IdentityUser> data) => ClearAsync();
    public Task HandleEventAsync(EntityChangedEventData<IdentityRole> data) => ClearAsync();
    private Task ClearAsync()
    {
        UserScopeService.ClearLocalReuse(manager);
        return Task.CompletedTask;
    }

    public static void ClearCurrentUnitOfWork(IUnitOfWorkManager manager) => UserScopeService.ClearLocalReuse(manager);
}
