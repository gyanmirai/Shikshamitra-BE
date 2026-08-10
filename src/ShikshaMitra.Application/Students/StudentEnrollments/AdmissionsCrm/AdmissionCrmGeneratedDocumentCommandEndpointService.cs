using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmGeneratedDocumentCommandService(
	AdmissionCrmGeneratedDocumentCommandWorkflow workflow)
	: ApplicationService, IAdmissionCrmGeneratedDocumentCommandService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.View)]
	public Task<AdmissionCrmGeneratedDocumentDto> PreviewAsync(
		Guid leadId,
		PreviewAdmissionCrmGeneratedDocumentDto input) => workflow.PreviewAsync(leadId, input);

	public Task<AdmissionCrmGeneratedDocumentDto> IssueAsync(
		Guid leadId,
		IssueAdmissionCrmGeneratedDocumentDto input) => workflow.IssueAsync(leadId, input);
}
