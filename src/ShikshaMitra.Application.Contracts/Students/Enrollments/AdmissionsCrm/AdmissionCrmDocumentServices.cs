using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmDocumentTemplateService : IApplicationService
{
	Task<PagedResultDto<AdmissionCrmDocumentChecklistTemplateDto>> GetTemplatesAsync(Guid academicYearId, GetAdmissionCrmDocumentChecklistTemplateListInput input);
	Task<AdmissionCrmDocumentChecklistTemplateDto> CreateTemplateAsync(Guid academicYearId, AdmissionCrmDocumentChecklistTemplateUpsertDto input);
	Task<AdmissionCrmDocumentChecklistTemplateDto> UpdateTemplateAsync(Guid id, AdmissionCrmDocumentChecklistTemplateUpsertDto input);
	Task<AdmissionCrmDocumentChecklistTemplateDto> PublishTemplateAsync(Guid id);
	Task<AdmissionCrmDocumentChecklistTemplateDto> CreateTemplateVersionAsync(Guid id);
	Task<AdmissionCrmDocumentChecklistTemplateDto> RetireTemplateAsync(Guid id);
}

public interface IAdmissionCrmDocumentChecklistService : IApplicationService
{
	Task<AdmissionCrmApplicantDocumentChecklistDto> AssignChecklistAsync(Guid registrationApplicationId, AssignAdmissionCrmDocumentChecklistDto input);
	Task<AdmissionCrmApplicantDocumentChecklistDto> GetChecklistAsync(Guid registrationApplicationId);
}

public interface IAdmissionCrmDocumentReviewService : IApplicationService
{
	Task<AdmissionCrmApplicantDocumentChecklistDto> UploadFromStaffAsync(Guid registrationApplicationId, ProcessedAdmissionCrmApplicantDocumentUploadDto input);
	Task<AdmissionCrmApplicantDocumentDto> ReviewAsync(Guid documentId, ReviewAdmissionCrmApplicantDocumentDto input);
	Task<AdmissionCrmApplicantDocumentDto> WaiveAsync(Guid assignmentId, string requirementKey, AdmissionCrmDocumentReasonDto input);
	Task<AdmissionCrmApplicantDocumentDto> MarkOriginalSeenAsync(Guid documentId);
	Task<AdmissionCrmApplicantDocumentChecklistDto> ConfirmReadinessAsync(Guid assignmentId, ConfirmAdmissionCrmReadinessDto input);
	Task<AdmissionCrmApplicantDocumentDownloadDto> DownloadAsync(Guid documentId);
}
