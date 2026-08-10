using System;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.TenantManagement;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPublicPortalWorkflow(ITenantRepository tenantRepository) : ApplicationService, ITransientDependency
{
	public async Task<PublicAdmissionPortalDto> GetAsync(Guid tenantId)
	{
		using (CurrentTenant.Change(null))
		{
			var tenant = await tenantRepository.FindAsync(tenantId);
			if (tenant == null)
			{
				throw new BusinessException("AdmissionCrm:PublicPortalNotFound");
			}

			var supportedLanguages = AdmissionCrmPublicBranding.ParseSupportedLanguages(
				tenant.GetProperty<string>("AdmissionsSupportedLanguages"));
			return new PublicAdmissionPortalDto
			{
				SchoolName = AdmissionCrmPublicBranding.FirstValue(
					tenant.GetProperty<string>("InstituteName"), tenant.Name) ?? "School",
				LogoDataUrl = AdmissionCrmPublicBranding.BuildLogoDataUrl(
					tenant.GetProperty<byte[]>("Logo"), tenant.GetProperty<string>("LogoContentType")),
				AdmissionsPhone = AdmissionCrmPublicBranding.FirstValue(
					tenant.GetProperty<string>("AdmissionsPhone"),
					tenant.GetProperty<string>("ContactNo"),
					tenant.GetProperty<string>("ContactNumber")),
				AdmissionsEmail = tenant.GetProperty<string>("AdmissionsEmail"),
				OfficeHours = tenant.GetProperty<string>("AdmissionsOfficeHours"),
				HelpText = tenant.GetProperty<string>("AdmissionsHelpText"),
				PrimaryColor = AdmissionCrmPublicBranding.NormalizeColor(tenant.GetProperty<string>("AdmissionsPrimaryColor")),
				DefaultLanguage = AdmissionCrmPublicBranding.NormalizeLanguage(
					tenant.GetProperty<string>("AdmissionsDefaultLanguage"), supportedLanguages),
				SupportedLanguages = supportedLanguages
			};
		}
	}
}
