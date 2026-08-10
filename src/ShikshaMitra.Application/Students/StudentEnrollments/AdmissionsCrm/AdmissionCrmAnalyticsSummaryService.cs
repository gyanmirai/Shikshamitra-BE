using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize, RemoteService(false)]
public class AdmissionCrmAnalyticsSummaryService(AdmissionCrmAnalyticsSummaryWorkflow workflow)
	: ApplicationService, IAdmissionCrmAnalyticsSummaryService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Analytics)]
	public Task<AdmissionCrmAnalyticsSummaryDto> GetAsync(Guid yearId, GetAdmissionCrmAnalyticsSummaryInput input) =>
		workflow.GetAsync(yearId, input);
}
