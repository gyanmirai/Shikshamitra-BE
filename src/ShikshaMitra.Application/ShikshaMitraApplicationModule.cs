using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShikshaMitra.Security.Permissions;
using ShikshaMitra.Security.Roles;
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
		context.Services.AddHttpContextAccessor();
		Configure<AbpPermissionOptions>(options => options.ValueProviders.Add<UserPermissionDenyValueProvider>());
		context.Services.AddDataProtection();
		context.Services.Replace(ServiceDescriptor.Transient<IPermissionAppService, ShikshaMitraPermissionAppService>());
		context.Services.Replace(ServiceDescriptor.Transient<PermissionAppService, SecureAbpPermissionAppService>());
		context.Services.Replace(ServiceDescriptor.Transient<IIdentityRoleAppService, SchoolIdentityRoleAppService>());
		context.Services.Replace(ServiceDescriptor.Transient<IdentityRoleAppService, SchoolIdentityRoleAppService>());

		Configure<AbpAutoMapperOptions>(options =>
		{
			options.AddMaps<ShikshaMitraApplicationModule>();
		});
	}
}
