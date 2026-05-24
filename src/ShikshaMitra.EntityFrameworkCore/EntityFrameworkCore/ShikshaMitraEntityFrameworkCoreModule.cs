using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.EntityFrameworkCore.HR.AttendanceAndLeaves;
using ShikshaMitra.EntityFrameworkCore.Security.UserScope;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Calendar.HolidayTypes;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.EntityFrameworkCore.HR.Calendar.Holidays;
using ShikshaMitra.HR.Calendar.Events;
using ShikshaMitra.EntityFrameworkCore.HR.Calendar.Events;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Students;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp.Uow;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.PostgreSql;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.TenantManagement.EntityFrameworkCore;
using Volo.Abp.BlobStoring.Database.EntityFrameworkCore;
using Volo.Abp.Data;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Concessions;

namespace ShikshaMitra.EntityFrameworkCore;

[DependsOn(
    typeof(ShikshaMitraDomainModule),
    typeof(AbpIdentityEntityFrameworkCoreModule),
    typeof(AbpOpenIddictEntityFrameworkCoreModule),
    typeof(AbpPermissionManagementEntityFrameworkCoreModule),
    typeof(AbpSettingManagementEntityFrameworkCoreModule),
    typeof(AbpEntityFrameworkCorePostgreSqlModule),
    typeof(AbpBackgroundJobsEntityFrameworkCoreModule),
    typeof(AbpAuditLoggingEntityFrameworkCoreModule),
    typeof(AbpTenantManagementEntityFrameworkCoreModule),
    typeof(AbpFeatureManagementEntityFrameworkCoreModule)
    )]
[DependsOn(typeof(BlobStoringDatabaseEntityFrameworkCoreModule))]
public class ShikshaMitraEntityFrameworkCoreModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        // https://www.npgsql.org/efcore/release-notes/6.0.html#opting-out-of-the-new-timestamp-mapping-logic
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        ShikshaMitraEfCoreEntityExtensionMappings.Configure();
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();

        context.Services.AddAbpDbContext<ShikshaMitraDbContext>(options =>
        {
            /* Remove "includeAllEntities: true" to create
             * default repositories only for aggregate roots */
            options.AddDefaultRepositories(includeAllEntities: true);

            options.Entity<AssignedSubject>(opt =>
            {
                opt.DefaultWithDetailsFunc = query =>
                    query.Include(x => x.Chapters)
                         .ThenInclude(c => c.Documents)
                         .Include(x => x.Documents);
            });

            // Register custom repositories
            options.AddRepository<UserScopeAssignment, EfCoreUserScopeAssignmentRepository>();
            options.AddRepository<SalarySlip, EfCoreSalarySlipRepository>();
            options.AddRepository<HolidayType, EFCoreHolidayTypeRepository>();
            options.AddRepository<Holiday, EFCoreHolidayRepository>();
            options.AddRepository<Event, EFCoreEventRepository>();
            options.AddRepository<PayrollRunReview, EfCorePayrollRunReviewRepository>();
            options.AddRepository<Student, EfCoreStudentRepository>();
            options.AddRepository<ConcessionRequest, EfCoreConcessionRequestRepository>();
            options.AddRepository<StaffAttendanceDayLock, EfCoreStaffAttendanceDayLockRepository>();
            options.AddRepository<DueFee, EfCoreDueFeeRepository>();
        });

        // Register custom repository extensions
        context.Services.AddTransient<IIdentityUserRepositoryExtensions, EfCoreIdentityUserRepositoryExtensions>();

        Configure<AbpDbContextOptions>(options =>
        {
            /* The main point to change your DBMS.
             * See also ShikshaMitraMigrationsDbContextFactory for EF Core tooling. */
            options.UseNpgsql();
        });

        // Ensure connection strings are available from configuration
        Configure<AbpDbConnectionOptions>(options =>
        {
            var connectionString = configuration["ConnectionStrings:Default"];
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                options.ConnectionStrings.Default = connectionString;
            }
        });

    }
}
