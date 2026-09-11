using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.HttpApi.Controllers.Student.Enrollment.Admissions;

[ApiController, Authorize, Route("api/student/enrollment/admissions/inquiries/{inquiryId:guid}/management")]
public class AdmissionInquiryManagementController(IAdmissionInquiryManagementAppService service) : AbpController
{
	[HttpGet, Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.View)]
	public async Task<Result<AdmissionInquiryManagementDto>> GetAsync(Guid inquiryId) =>
		Result.Success(await service.GetAsync(inquiryId));

	[HttpPost("contact"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<Result<AdmissionInquiryManagementDto>> RecordContactAsync(Guid inquiryId, RecordAdmissionInquiryContactDto input) =>
		Result.Success(await service.RecordContactAsync(inquiryId, input));

	[HttpPost("follow-ups"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<Result<AdmissionInquiryManagementDto>> ScheduleFollowUpAsync(Guid inquiryId, ScheduleAdmissionInquiryFollowUpDto input) =>
		Result.Success(await service.ScheduleFollowUpAsync(inquiryId, input));

	[HttpPost("follow-ups/{followUpId:guid}/complete"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<Result<AdmissionInquiryManagementDto>> CompleteFollowUpAsync(Guid inquiryId, Guid followUpId,
		CompleteAdmissionInquiryFollowUpDto input) => Result.Success(await service.CompleteFollowUpAsync(inquiryId, followUpId, input));

	[HttpPost("follow-ups/{followUpId:guid}/cancel"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<Result<AdmissionInquiryManagementDto>> CancelFollowUpAsync(Guid inquiryId, Guid followUpId,
		CancelAdmissionInquiryFollowUpDto input) => Result.Success(await service.CancelFollowUpAsync(inquiryId, followUpId, input));

	[HttpPost("qualification"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<Result<AdmissionInquiryManagementDto>> QualifyAsync(Guid inquiryId, QualifyAdmissionInquiryDto input) =>
		Result.Success(await service.QualifyAsync(inquiryId, input));

	[HttpPost("reopen"), Authorize(ShikshaMitraSubmodulePermissions.Students.AdmissionInquiries.Manage)]
	public async Task<Result<AdmissionInquiryManagementDto>> ReopenAsync(Guid inquiryId, ReopenAdmissionInquiryDto input) =>
		Result.Success(await service.ReopenAsync(inquiryId, input));
}
