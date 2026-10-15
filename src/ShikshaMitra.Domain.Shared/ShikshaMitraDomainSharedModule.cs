using ShikshaMitra.Localization;
using Volo.Abp.AuditLogging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Localization;
using Volo.Abp.Localization.ExceptionHandling;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.TenantManagement;
using Volo.Abp.Validation.Localization;
using Volo.Abp.VirtualFileSystem;
using Volo.Abp.BlobStoring.Database;

namespace ShikshaMitra;

[DependsOn(
    typeof(AbpAuditLoggingDomainSharedModule),
    typeof(AbpBackgroundJobsDomainSharedModule),
    typeof(AbpFeatureManagementDomainSharedModule),
    typeof(AbpIdentityDomainSharedModule),
    typeof(AbpOpenIddictDomainSharedModule),
    typeof(AbpPermissionManagementDomainSharedModule),
    typeof(AbpSettingManagementDomainSharedModule),
    typeof(AbpTenantManagementDomainSharedModule)
    )]
[DependsOn(typeof(BlobStoringDatabaseDomainSharedModule))]
public class ShikshaMitraDomainSharedModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        ShikshaMitraGlobalFeatureConfigurator.Configure();
        ShikshaMitraModuleExtensionConfigurator.Configure();
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpVirtualFileSystemOptions>(options =>
        {
            options.FileSets.AddEmbedded<ShikshaMitraDomainSharedModule>();
        });

        Configure<AbpLocalizationOptions>(options =>
        {
            options.Resources
                .Add<ShikshaMitraResource>("en")
                .AddBaseTypes(typeof(AbpValidationResource))
                .AddVirtualJson("/Localization/ShikshaMitra");

            options.DefaultResourceType = typeof(ShikshaMitraResource);
        });

        Configure<AbpExceptionLocalizationOptions>(options =>
        {
            options.MapCodeNamespace("ShikshaMitra", typeof(ShikshaMitraResource));
            options.MapCodeNamespace("ShikshaMitra.TenantSettings", typeof(ShikshaMitraResource));
            options.MapCodeNamespace("ST", typeof(ShikshaMitraResource));
            options.MapCodeNamespace("HR", typeof(ShikshaMitraResource));
            options.MapCodeNamespace("Academic", typeof(ShikshaMitraResource));
            options.MapCodeNamespace("Finance", typeof(ShikshaMitraResource));
            options.MapCodeNamespace("Admissions", typeof(ShikshaMitraResource));
            options.MapCodeNamespace("Students", typeof(ShikshaMitraResource));
			options.MapCodeNamespace("StudentAttendance", typeof(ShikshaMitraResource));
            options.MapCodeNamespace("Document", typeof(ShikshaMitraResource));
            options.MapCodeNamespace("SchoolIdentity", typeof(ShikshaMitraResource));
        });
    }
}
