using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Finance.Fees.Receipts;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Onboarding;
using ShikshaMitra.HR.Payroll.RunPayroll;
using Volo.Abp.MultiTenancy;
using Volo.Abp;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Sqlite;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.Uow;

namespace ShikshaMitra;

/// <summary>
/// Extends the standard EF Core repository so all ABP IRepository members are
/// satisfied, but replaces IncrementAndGetAsync with a thread-safe in-memory
/// counter so SQLite tests never execute the PostgreSQL-specific upsert SQL.
/// </summary>
internal sealed class FakeReceiptNumberSequenceRepository(
    IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
    : EfCoreRepository<ShikshaMitraDbContext, ReceiptNumberSequence, Guid>(dbContextProvider),
      IReceiptNumberSequenceRepository
{
    // Static so values persist across test-method boundary within the same process run.
    private static readonly ConcurrentDictionary<string, long> _sequences = new();

    public Task<long> IncrementAndGetAsync(int year, FeeReceiptKind? kind, CancellationToken ct = default)
    {
        var key = $"{year}:{kind?.ToString() ?? "null"}";
        var next = _sequences.AddOrUpdate(key, 1L, (_, current) => current + 1);
        return Task.FromResult(next);
    }
}

internal sealed class FakeEmployeeLeaveRepository(
    IDbContextProvider<ShikshaMitraDbContext> dbContextProvider)
    : EfCoreEmployeeLeaveRepository(dbContextProvider)
{
    private static int _nextPublicId;

    public override async Task<EmployeeLeave> InsertAsync(
        EmployeeLeave entity,
        bool autoSave = false,
        CancellationToken cancellationToken = default)
    {
        if (entity.PublicId <= 0)
        {
            var dbSet = await GetDbSetAsync();
            var maxPublicId = await dbSet
                .Select(x => (int?)x.PublicId)
                .MaxAsync(cancellationToken) ?? 0;
            var nextPublicId = Math.Max(
                Interlocked.Increment(ref _nextPublicId),
                maxPublicId + 1);

            typeof(EmployeeLeave)
                .GetProperty(nameof(EmployeeLeave.PublicId))!
                .SetValue(entity, nextPublicId);
        }

        return await base.InsertAsync(entity, autoSave, cancellationToken);
    }
}

// This harness disables every UOW transaction. PostgreSQL API tests cover the real lock fence.
internal sealed class NonTransactionalPayrollInputRevisionStore(
    IDbContextProvider<ShikshaMitraDbContext> provider, ICurrentTenant tenant) : IPayrollInputRevisionStore
{
    public async Task<Guid> ReadAsync()
    {
        var db = await provider.GetDbContextAsync();
        var tenantId = tenant.Id ?? Guid.Empty;
        return await db.Set<PayrollInputRevision>().AsNoTracking().Where(x => x.Id == tenantId)
            .Select(x => x.Revision).FirstOrDefaultAsync();
    }
    public Task<Guid> LockAsync() => ReadAsync();
}

public sealed record SentEmployeeOnboardingInvitation(Guid ApplicationId, string? Email, string PublicPath);

public sealed record SentEmployeeWelcomeEmail(
    string Email,
    string FullName,
    string Username);

public sealed class FakeEmployeeEmailService : IEmployeeEmailService
{
    private readonly ConcurrentQueue<SentEmployeeWelcomeEmail> _sent = new();

    public IReadOnlyCollection<SentEmployeeWelcomeEmail> Sent => _sent.ToList();

    public Task SendWelcomeEmailAsync(
        string employeeEmail,
        string employeeName,
        string username,
        string password)
    {
        _sent.Enqueue(new(employeeEmail, employeeName, username));
        return Task.CompletedTask;
    }

    public void Reset()
    {
        while (_sent.TryDequeue(out _))
        {
        }
    }
}

public sealed class FakeEmployeeOnboardingInvitationEmailService : IEmployeeOnboardingInvitationEmailService
{
    private readonly ConcurrentQueue<SentEmployeeOnboardingInvitation> _sent = new();

    public bool FailNextSend { get; set; }
    public IReadOnlyCollection<SentEmployeeOnboardingInvitation> Sent => _sent.ToList();

    public Task<EmployeeOnboardingInvitationEmailResult> SendInvitationAsync(
        EmployeeApplication application,
        string publicPath)
    {
        _sent.Enqueue(new(application.Id, application.Email, publicPath));

        if (FailNextSend)
        {
            FailNextSend = false;
            return Task.FromResult(new EmployeeOnboardingInvitationEmailResult(
                false,
                "Invite was created, but email could not be sent."));
        }

        return Task.FromResult(new EmployeeOnboardingInvitationEmailResult(true, "Invite email sent."));
    }

    public void Reset()
    {
        FailNextSend = false;
        while (_sent.TryDequeue(out _))
        {
        }
    }
}

[DependsOn(
    typeof(ShikshaMitraApplicationModule),
    typeof(ShikshaMitraDomainTestModule),
    typeof(ShikshaMitraEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCoreSqliteModule)
)]
public class ShikshaMitraApplicationTestModule : AbpModule
{
    private SqliteConnection? _sqliteConnection;

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Use real protected reset/verification tokens for portal owner-setup tests.
        new Microsoft.AspNetCore.Identity.IdentityBuilder(typeof(Volo.Abp.Identity.IdentityUser),
            typeof(Volo.Abp.Identity.IdentityRole), context.Services).AddDefaultTokenProviders();
        Configure<FeatureManagementOptions>(options =>
        {
            options.SaveStaticFeaturesToDatabase = false;
            options.IsDynamicFeatureStoreEnabled = false;
        });
        Configure<PermissionManagementOptions>(options =>
        {
            options.SaveStaticPermissionsToDatabase = false;
            options.IsDynamicPermissionStoreEnabled = false;
        });
        Configure<SettingManagementOptions>(options =>
        {
            options.SaveStaticSettingsToDatabase = false;
            options.IsDynamicSettingStoreEnabled = false;
        });

        context.Services.AddAlwaysDisableUnitOfWorkTransaction();
        context.Services.AddSingleton<Microsoft.AspNetCore.DataProtection.IDataProtectionProvider>(new Microsoft.AspNetCore.DataProtection.EphemeralDataProtectionProvider());
        context.Services.AddHttpClient();
        ConfigureInMemorySqlite(context.Services);

        // Replace the PostgreSQL-specific sequence repository with an in-memory fake
        // so that application tests using SQLite don't fail on PostgreSQL-only SQL syntax.
        context.Services.AddTransient<IReceiptNumberSequenceRepository, FakeReceiptNumberSequenceRepository>();
        context.Services.AddTransient<IEmployeeLeaveRepository, FakeEmployeeLeaveRepository>();
        context.Services.AddTransient<IPayrollInputRevisionStore, NonTransactionalPayrollInputRevisionStore>();
        context.Services.AddSingleton<FakeEmployeeEmailService>();
        context.Services.Replace(ServiceDescriptor.Singleton<IEmployeeEmailService>(sp =>
            sp.GetRequiredService<FakeEmployeeEmailService>()));
        context.Services.AddSingleton<FakeEmployeeOnboardingInvitationEmailService>();
        context.Services.Replace(ServiceDescriptor.Singleton<IEmployeeOnboardingInvitationEmailService>(sp =>
            sp.GetRequiredService<FakeEmployeeOnboardingInvitationEmailService>()));
    }

    public override void OnApplicationShutdown(ApplicationShutdownContext context)
    {
        _sqliteConnection?.Dispose();
    }

    private void ConfigureInMemorySqlite(IServiceCollection services)
    {
        _sqliteConnection = CreateDatabaseAndGetConnection();

        services.Configure<AbpDbContextOptions>(options =>
        {
            options.Configure(dbContextConfigurationContext =>
            {
                dbContextConfigurationContext.DbContextOptions.UseSqlite(_sqliteConnection)
                    // Query-count tests attach listeners after seeding; do not cache a disabled diagnostic listener.
                    .ConfigureLoggingCacheTime(TimeSpan.Zero)
                    .ReplaceService<IMemberTranslatorProvider, SqliteApplicationTestMemberTranslatorProvider>();
            });
        });
    }

    private static SqliteConnection CreateDatabaseAndGetConnection()
    {
        var connection = new AbpUnitTestSqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<ShikshaMitraDbContext>()
            .UseSqlite(connection)
            .Options;

        using (var context = new ShikshaMitraDbContext(options))
        {
            context.GetService<IRelationalDatabaseCreator>().CreateTables();
        }

        return connection;
    }
}
