using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.EntityFrameworkCore.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using Shouldly;
using Volo.Abp.Data;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Xunit;

namespace ShikshaMitra.EntityFrameworkCore.Tests.Finance;

public class ConcessionApplicationsQueryPerformance_Tests : IDisposable
{
    private readonly ServiceProvider services = new ServiceCollection()
        .AddOptions()
        .Configure<AbpDataFilterOptions>(options =>
        {
            options.DefaultStates[typeof(ISoftDelete)] = new DataFilterState(true);
            options.DefaultStates[typeof(IMultiTenant)] = new DataFilterState(true);
        })
        .AddSingleton<IDataFilter, DataFilter>()
        .AddTransient(typeof(IDataFilter<>), typeof(DataFilter<>))
        .AddSingleton<ICurrentTenantAccessor>(AsyncLocalCurrentTenantAccessor.Instance)
        .AddTransient<ICurrentTenant, CurrentTenant>()
        .AddTransient<IClock, Clock>()
        .BuildServiceProvider();

    [Fact]
    public void Default_worklist_page_does_not_join_display_tables_or_child_graphs()
    {
        using var tenant = services.GetRequiredService<ICurrentTenant>().Change(Guid.NewGuid());
        ShikshaMitraEfCoreEntityExtensionMappings.Configure();
        using var db = new ShikshaMitraDbContext(new DbContextOptionsBuilder<ShikshaMitraDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=translation_only;Username=translation_only").Options)
        { LazyServiceProvider = new AbpLazyServiceProvider(services) };
        var options = FinanceTableQuery.Parse(null, null, null, null, ConcessionApplicationsTable.Columns);
        var requests = db.Set<ConcessionRequest>().IgnoreAutoIncludes().AsNoTracking();

        var filtered = ConcessionApplicationsTableQuery.Filter(requests, db, options);
        var sql = ConcessionApplicationsTableQuery.Order(filtered, db, options)
            .Skip(10).Take(10).ToQueryString();

        sql.ShouldContain("LIMIT");
        sql.ShouldContain("OFFSET");
        sql.ShouldNotContain("JOIN");
        sql.ShouldNotContain("Finance_ConcessionRequestDocuments");
    }

    public void Dispose() => services.Dispose();
}
