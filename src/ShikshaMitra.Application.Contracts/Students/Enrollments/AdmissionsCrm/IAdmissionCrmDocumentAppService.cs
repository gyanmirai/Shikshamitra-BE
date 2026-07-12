using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmDocumentAppService : IApplicationService
{
	Task<PagedResultDto<AdmissionCrmDocumentChecklistTemplateDto>> GetTemplatesAsync(Guid academicYearId, GetAdmissionCrmDocumentChecklistTemplateListInput input);
	Task<AdmissionCrmDocumentChecklistTemplateDto> CreateTemplateAsync(Guid academicYearId, AdmissionCrmDocumentChecklistTemplateUpsertDto input);
	Task<AdmissionCrmDocumentChecklistTemplateDto> UpdateTemplateAsync(Guid id, AdmissionCrmDocumentChecklistTemplateUpsertDto input);
	Task<AdmissionCrmDocumentChecklistTemplateDto> PublishTemplateAsync(Guid id);
	Task<AdmissionCrmDocumentChecklistTemplateDto> CreateTemplateVersionAsync(Guid id);
	Task<AdmissionCrmDocumentChecklistTemplateDto> RetireTemplateAsync(Guid id);
	Task<AdmissionCrmApplicantDocumentChecklistDto> AssignChecklistAsync(Guid registrationApplicationId, AssignAdmissionCrmDocumentChecklistDto input);
	Task<AdmissionCrmApplicantDocumentChecklistDto?> AssignApplicableChecklistFromAutomationAsync(
		Guid registrationApplicationId,
		AdmissionCrmPlaybookApplicabilityDto applicability);
	Task<AdmissionCrmApplicantDocumentChecklistDto> GetChecklistAsync(Guid registrationApplicationId);
	Task<AdmissionCrmApplicantDocumentChecklistDto?> GetChecklistFromApplicantAsync(Guid leadId);
	Task<AdmissionCrmApplicantDocumentChecklistDto> UploadFromApplicantAsync(Guid leadId, ProcessedAdmissionCrmApplicantDocumentUploadDto input);
	Task<AdmissionCrmApplicantDocumentDto> ReviewAsync(Guid documentId, ReviewAdmissionCrmApplicantDocumentDto input);
	Task<AdmissionCrmApplicantDocumentDto> WaiveAsync(Guid assignmentId, string requirementKey, AdmissionCrmDocumentReasonDto input);
	Task<AdmissionCrmApplicantDocumentDto> MarkOriginalSeenAsync(Guid documentId);
	Task<AdmissionCrmApplicantDocumentChecklistDto> ConfirmReadinessAsync(Guid assignmentId, ConfirmAdmissionCrmReadinessDto input);
	Task<AdmissionCrmApplicantDocumentDownloadDto> DownloadAsync(Guid documentId);
}
