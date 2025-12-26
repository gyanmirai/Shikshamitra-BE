using ShikshaMitra.Security.Permissions;
using Volo.Abp.Account;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.AutoMapper;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.TenantManagement;

namespace ShikshaMitra;

[DependsOn(
    typeof(ShikshaMitraDomainModule),
    typeof(AbpAccountApplicationModule),
    typeof(ShikshaMitraApplicationContractsModule),
    typeof(AbpIdentityApplicationModule),
    typeof(AbpPermissionManagementApplicationModule),
    typeof(AbpTenantManagementApplicationModule),
    typeof(AbpFeatureManagementApplicationModule),
    typeof(AbpSettingManagementApplicationModule)
    )]
public class ShikshaMitraApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options =>
        {
            options.AddMaps<ShikshaMitraApplicationModule>();
        });

        // Configure permission management with admin bypass
        Configure<AbpPermissionOptions>(options =>
        {
            // Add AdminPermissionValueProvider as the first provider
            // This ensures admin users bypass all permission checks
            options.ValueProviders.Insert(0, typeof(AdminPermissionValueProvider));
        });

        // Register the CSV import service
        //context.Services.AddTransient<IStudentCsvImportService, StudentCsvImportService>();
    }
}
