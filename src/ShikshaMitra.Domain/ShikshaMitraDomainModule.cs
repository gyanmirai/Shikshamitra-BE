using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShikshaMitra.MultiTenancy;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using Volo.Abp;
using Volo.Abp.AuditLogging;
using Volo.Abp.Auditing;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BlobStoring;
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
			options.Languages.Add(new LanguageInfo("en-US", "en-US", "English (US)"));
			options.Languages.Add(new LanguageInfo("hi", "hi", "Hindi"));
			options.Languages.Add(new LanguageInfo("hi-IN", "hi-IN", "Hindi (India)"));
		});

		Configure<AbpMultiTenancyOptions>(options => { options.IsEnabled = MultiTenancyConsts.IsEnabled; });

		Configure<AbpSequentialGuidGeneratorOptions>(options =>
		{
			options.DefaultSequentialGuidType = SequentialGuidType.SequentialAsString;
		});

		Configure<AbpAuditingOptions>(options =>
		{
			options.EntityHistorySelectors.Add(new NamedTypeSelector(
				"ConcessionType",
				type => type == typeof(ConcessionType)));
			options.EntityHistorySelectors.Add(new NamedTypeSelector(
				"AccountHead",
				type => type == typeof(AccountHead)));
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
		context.Services.AddTransient<UserScopeAssignmentManager>();

		//TODO : Configure TenantResolveContributors

//#if DEBUG
//		context.Services.Replace(ServiceDescriptor.Singleton<IEmailSender, NullEmailSender>());
//#endif
	}
}
