using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmPlaybookOperationsService(
	AdmissionCrmPlaybookResolutionWorkflow resolutionWorkflow,
	AdmissionCrmPlaybookSlotWorkflow slotWorkflow,
	AdmissionCrmPlaybookExamResultWorkflow examResultWorkflow)
	: ApplicationService, IAdmissionCrmPlaybookOperationsService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmPlaybookResolutionDto> ResolveAsync(Guid academicYearId, ResolveAdmissionCrmPlaybookDto input) =>
		resolutionWorkflow.ResolveAsync(academicYearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageExamConfiguration)]
	public Task<AdmissionCrmSlotGenerationResultDto> GenerateSlotsAsync(Guid id, GenerateAdmissionCrmPlaybookSlotsDto input) =>
		slotWorkflow.GenerateAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmSlotRecommendationDto> RecommendSlotAsync(Guid id, RecommendAdmissionCrmPlaybookSlotDto input) =>
		slotWorkflow.RecommendAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmExamResultPreviewDto> PreviewExamResultAsync(Guid id, PreviewAdmissionCrmExamResultDto input) =>
		examResultWorkflow.PreviewAsync(id, input);
}
