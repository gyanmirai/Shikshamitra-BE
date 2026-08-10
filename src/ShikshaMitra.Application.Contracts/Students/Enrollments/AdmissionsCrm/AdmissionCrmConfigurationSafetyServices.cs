using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmConfigurationReferenceService : IApplicationService
{
	Task<AdmissionCrmConfigurationReferenceResultDto> GetAsync(Guid academicYearId,
		AdmissionCrmConfigurationReferenceKind kind, string code);
}

public interface IAdmissionCrmConfigurationImpactService : IApplicationService
{
	Task<AdmissionCrmConfigurationImpactPreviewDto> PreviewAsync(Guid academicYearId,
		PreviewAdmissionCrmConfigurationImpactDto input);
}

public interface IAdmissionCrmConfigurationCloneService : IApplicationService
{
	Task<AdmissionCrmConfigurationClonePreviewDto> PreviewAsync(PreviewAdmissionCrmConfigurationCloneDto input);
	Task<AdmissionCrmConfigurationCloneResultDto> ConfirmAsync(ConfirmAdmissionCrmConfigurationCloneDto input);
}
