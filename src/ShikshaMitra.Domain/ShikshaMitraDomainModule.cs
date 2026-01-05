using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShikshaMitra.MultiTenancy;
using ShikshaMitra.Security.UserScope;
using Volo.Abp.AuditLogging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
using Volo.Abp.Emailing;
using Volo.Abp.FeatureManagement;
using Volo.Abp.MailKit;
using Volo.Abp.Features;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.OpenIddict;
using Volo.Abp.PermissionManagement.Identity;
using Volo.Abp.PermissionManagement.OpenIddict;
using Volo.Abp.SettingManagement;
using Volo.Abp.TenantManagement;
using Volo.Abp.BlobStoring.Database;

namespace ShikshaMitra;

[DependsOn(
	typeof(ShikshaMitraDomainSharedModule),
	typeof(AbpAuditLoggingDomainModule),
	typeof(AbpBackgroundJobsDomainModule),
	typeof(AbpFeatureManagementDomainModule),
	typeof(AbpIdentityDomainModule),
	typeof(AbpOpenIddictDomainModule),
	typeof(AbpPermissionManagementDomainOpenIddictModule),
	typeof(AbpPermissionManagementDomainIdentityModule),
	typeof(AbpSettingManagementDomainModule),
	typeof(AbpTenantManagementDomainModule),
	typeof(AbpEmailingModule),
	typeof(AbpMailKitModule)
)]
[DependsOn(typeof(BlobStoringDatabaseDomainModule))]
public class ShikshaMitraDomainModule : AbpModule
{
	public override void ConfigureServices(ServiceConfigurationContext context)
	{
		Configure<AbpLocalizationOptions>(options =>
		{
			options.Languages.Add(new LanguageInfo("en", "en", "English"));
			options.Languages.Add(new LanguageInfo("hi", "hi", "Hindi"));
		});

		Configure<AbpMultiTenancyOptions>(options => { options.IsEnabled = MultiTenancyConsts.IsEnabled; });

		Configure<AbpSequentialGuidGeneratorOptions>(options =>
		{
			options.DefaultSequentialGuidType = SequentialGuidType.SequentialAsString;
		});

		Configure<AbpBlobStoringOptions>(options =>
		{
			options.Containers.ConfigureDefault(container =>
			{
				container.UseDatabase();
			});
		});

		// Configure UserScope options
		Configure<UserScopeOptions>(options =>
		{
			// Default values are already set in UserScopeOptions class
			// Can be overridden in appsettings.json
		});

		// Register UserScopeService
		context.Services.AddTransient<IUserScopeService, UserScopeService>();

		//TODO : Configure TenantResolveContributors

//#if DEBUG
//		context.Services.Replace(ServiceDescriptor.Singleton<IEmailSender, NullEmailSender>());
//#endif
	}
}