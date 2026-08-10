using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmDocumentApplicantWorkflow(
	AdmissionCrmDocumentChecklistWorkflow checklistWorkflow,
	AdmissionCrmDocumentReviewWorkflow reviewWorkflow) : ITransientDependency
{
	public Task<AdmissionCrmApplicantDocumentChecklistDto?> GetChecklistAsync(Guid leadId) =>
		checklistWorkflow.GetChecklistFromApplicantAsync(leadId);

	public Task<AdmissionCrmApplicantDocumentChecklistDto> UploadAsync(
		Guid leadId,
		ProcessedAdmissionCrmApplicantDocumentUploadDto input) =>
		reviewWorkflow.UploadFromApplicantAsync(leadId, input);
}
