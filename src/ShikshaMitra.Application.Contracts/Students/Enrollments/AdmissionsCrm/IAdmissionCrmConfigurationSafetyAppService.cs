using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmConfigurationSafetyAppService : IApplicationService
{
	Task<AdmissionCrmConfigurationReferenceResultDto> GetReferencesAsync(
		Guid academicYearId,
		AdmissionCrmConfigurationReferenceKind kind,
		string code);

	Task<AdmissionCrmConfigurationImpactPreviewDto> PreviewImpactAsync(
		Guid academicYearId,
		PreviewAdmissionCrmConfigurationImpactDto input);

	Task<AdmissionCrmConfigurationClonePreviewDto> PreviewCloneAsync(
		PreviewAdmissionCrmConfigurationCloneDto input);

	Task<AdmissionCrmConfigurationCloneResultDto> ConfirmCloneAsync(
		ConfirmAdmissionCrmConfigurationCloneDto input);
}
