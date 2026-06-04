using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.FeeAdjustment;
using ShikshaMitra.Security.Permissions;
using Volo.Abp.Account;
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
		context.Services.AddHttpContextAccessor();
		context.Services.AddSingleton<IAuthorizationHandler, FeeAdjustmentLegacyPermissionAuthorizationHandler>();
		context.Services.Replace(ServiceDescriptor.Transient<IPermissionAppService, ShikshaMitraPermissionAppService>());
		context.Services.Replace(ServiceDescriptor.Transient<PermissionAppService, SecureAbpPermissionAppService>());

		Configure<AbpAutoMapperOptions>(options =>
		{
			options.AddMaps<ShikshaMitraApplicationModule>();
		});
	}
}
