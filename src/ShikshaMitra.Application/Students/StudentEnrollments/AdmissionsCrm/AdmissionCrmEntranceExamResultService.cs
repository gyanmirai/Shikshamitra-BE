using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmEntranceExamResultService(AdmissionCrmEntranceExamResultWorkflow workflow)
	: ApplicationService, IAdmissionCrmEntranceExamResultService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ChangeExamResult)]
	public Task<AdmissionCrmEntranceExamDto> UploadResultAsync(
		Guid examRecordId,
		UploadAdmissionCrmEntranceExamResultDto input) => workflow.UploadAsync(examRecordId, input);
}
