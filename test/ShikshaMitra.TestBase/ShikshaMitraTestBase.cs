using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Modularity;
using Volo.Abp.Uow;
using Volo.Abp.Testing;

namespace ShikshaMitra;

/* All test classes are derived from this class, directly or indirectly.
 */
public abstract class ShikshaMitraTestBase<TStartupModule> : AbpIntegratedTest<TStartupModule>
    where TStartupModule : IAbpModule
{
    private static int _panCounter = Random.Shared.Next(4000, 7000);
    private static int _adharCounter = Random.Shared.Next(400000000, 700000000);

    protected static string UniquePan() =>
        $"TESTA{Interlocked.Increment(ref _panCounter) % 10000:0000}Z";

    protected static string UniqueAdhar() =>
        $"900{Interlocked.Increment(ref _adharCounter) % 1000000000:000000000}";

    protected override void SetAbpApplicationCreationOptions(AbpApplicationCreationOptions options)
    {
        options.UseAutofac();
    }

    protected virtual Task WithUnitOfWorkAsync(Func<Task> func)
    {
        return WithUnitOfWorkAsync(new AbpUnitOfWorkOptions(), func);
    }

    protected virtual async Task WithUnitOfWorkAsync(AbpUnitOfWorkOptions options, Func<Task> action)
    {
        using (var scope = ServiceProvider.CreateScope())
        {
            var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

            using (var uow = uowManager.Begin(options))
            {
                await action();

                await uow.CompleteAsync();
            }
        }
    }

    protected virtual Task<TResult> WithUnitOfWorkAsync<TResult>(Func<Task<TResult>> func)
    {
        return WithUnitOfWorkAsync(new AbpUnitOfWorkOptions(), func);
    }

    protected virtual async Task<TResult> WithUnitOfWorkAsync<TResult>(AbpUnitOfWorkOptions options, Func<Task<TResult>> func)
    {
        using (var scope = ServiceProvider.CreateScope())
        {
            var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

            using (var uow = uowManager.Begin(options))
            {
                var result = await func();
                await uow.CompleteAsync();
                return result;
            }
        }
    }
}
