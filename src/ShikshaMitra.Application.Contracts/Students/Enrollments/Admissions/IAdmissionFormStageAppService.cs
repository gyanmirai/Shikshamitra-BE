using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public interface IAdmissionFormStageAppService : IApplicationService
{
	Task<AdmissionFormStageDto> GetAsync(Guid applicationId, Guid stageId);
	Task<AdmissionFormStageDto> VerifySectionAsync(Guid applicationId, Guid stageId, Guid sectionId, VerifyAdmissionFormSectionDto input);
	Task<AdmissionFormStageDto> RequestCorrectionAsync(Guid applicationId, Guid stageId, RequestAdmissionFormCorrectionDto input);
}
