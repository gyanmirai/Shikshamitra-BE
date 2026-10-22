using Localization.Resources.AbpUi;
using ShikshaMitra.Localization;
using Volo.Abp.Account;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement.HttpApi;
using Volo.Abp.SettingManagement;
using Volo.Abp.TenantManagement;

namespace ShikshaMitra;

[DependsOn(
	typeof(ShikshaMitraApplicationContractsModule),
	typeof(AbpAccountHttpApiModule),
	typeof(AbpIdentityHttpApiModule),
	typeof(AbpPermissionManagementHttpApiModule),
	typeof(AbpTenantManagementHttpApiModule),
	typeof(AbpFeatureManagementHttpApiModule),
	typeof(AbpSettingManagementHttpApiModule)
	)]
public class ShikshaMitraHttpApiModule : AbpModule
{
	public override void ConfigureServices(ServiceConfigurationContext context)
	{
		ConfigureLocalization();
		Configure<Volo.Abp.AspNetCore.ExceptionHandling.AbpExceptionHttpStatusCodeOptions>(options =>
		{
			options.Map("MobileAcademics:SubmissionConflict", System.Net.HttpStatusCode.Conflict);
			options.Map("MobileAcademics:IdempotencyConflict", System.Net.HttpStatusCode.Conflict);
			foreach (var code in new[] {
				Academic.Mobile.MobileSubmissionErrorCodes.AmbiguousAnswer,
				Academic.Mobile.MobileSubmissionErrorCodes.InvalidRequestId,
				Academic.Mobile.MobileSubmissionErrorCodes.RequestMetadataRequired,
				Academic.Mobile.MobileSubmissionErrorCodes.InvalidAttachments,
				Academic.Mobile.MobileSubmissionErrorCodes.InvalidFile,
				Academic.Mobile.MobileSubmissionErrorCodes.SubmissionTooLarge,
				Academic.Mobile.MobileSubmissionErrorCodes.SubmissionRequired,
				Academic.Mobile.MobileSubmissionErrorCodes.AcknowledgmentRequired,
				Academic.Mobile.MobileSubmissionErrorCodes.SubmissionNotSupported,
				Academic.Mobile.MobileSubmissionErrorCodes.DraftAfterSubmission,
				Academic.Mobile.MobileSubmissionErrorCodes.FinalAttachmentProtected })
				options.Map(code, System.Net.HttpStatusCode.BadRequest);
		});
	}

	private void ConfigureLocalization()
	{
		Configure<AbpLocalizationOptions>(options =>
		{
			options.Resources
				.Get<ShikshaMitraResource>()
				.AddBaseTypes(
					typeof(AbpUiResource)
				);
		});
	}
}
