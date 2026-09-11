using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Microsoft.AspNetCore.Authorization;
using WorkflowPermissions = ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Students.AdmissionWorkflows;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public class AdmissionInquirySettingsAppService(AdmissionInquiryPeopleService people)
	: ApplicationService, IAdmissionInquirySettingsAppService
{
	[Authorize(WorkflowPermissions.View)]
	public Task<List<AdmissionInquiryCounsellorDto>> GetCounsellorsAsync(string? filter = null) =>
		people.GetCounsellorsAsync(filter);
}
