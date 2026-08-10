using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.Students.StudentEnrollments.AdmissionsCrm;

[ApiController]
[Authorize]
[Route("api/student-enrollment/admissions-crm")]
public class AdmissionCrmEventController(IAdmissionCrmEventAppService service) : AbpControllerBase
{
	[HttpGet("{academicYearId}/events")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewEvents)]
	public Task<PagedResultDto<AdmissionCrmEventDto>> GetListAsync(
		[FromRoute] Guid academicYearId, [FromQuery] GetAdmissionCrmEventListInput input) =>
		service.GetListAsync(academicYearId, input);

	[HttpGet("events/{eventId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewEvents)]
	public Task<AdmissionCrmEventDto> GetAsync([FromRoute] Guid eventId) => service.GetAsync(eventId);

	[HttpPost("{academicYearId}/events")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageEvents)]
	public Task<AdmissionCrmEventDto> CreateAsync(
		[FromRoute] Guid academicYearId, [FromBody] CreateUpdateAdmissionCrmEventDto input) =>
		service.CreateAsync(academicYearId, input);

	[HttpPut("events/{eventId}")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageEvents)]
	public Task<AdmissionCrmEventDto> UpdateAsync(
		[FromRoute] Guid eventId, [FromBody] CreateUpdateAdmissionCrmEventDto input) =>
		service.UpdateAsync(eventId, input);

	[HttpPost("events/{eventId}/publish")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageEvents)]
	public Task<AdmissionCrmEventDto> PublishAsync([FromRoute] Guid eventId) => service.PublishAsync(eventId);

	[HttpPost("events/{eventId}/cancel")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageEvents)]
	public Task<AdmissionCrmEventDto> CancelAsync(
		[FromRoute] Guid eventId, [FromBody] CancelAdmissionCrmEventDto input) => service.CancelAsync(eventId, input);

	[HttpPost("events/{eventId}/complete")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageEvents)]
	public Task<AdmissionCrmEventDto> CompleteAsync([FromRoute] Guid eventId) => service.CompleteAsync(eventId);

	[HttpPost("events/{eventId}/registrations")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageEvents)]
	public Task<AdmissionCrmEventDto> ReserveAsync(
		[FromRoute] Guid eventId, [FromBody] ReserveAdmissionCrmEventRegistrationDto input) =>
		service.ReserveAsync(eventId, input);

	[HttpPost("events/{eventId}/registrations/{registrationId}/cancel")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageEvents)]
	public Task<AdmissionCrmEventDto> CancelRegistrationAsync([FromRoute] Guid eventId,
		[FromRoute] Guid registrationId, [FromBody] CancelAdmissionCrmEventRegistrationDto input) =>
		service.CancelRegistrationAsync(eventId, registrationId, input);

	[HttpPost("events/{eventId}/registrations/{registrationId}/check-in")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.CheckInEvents)]
	public Task<AdmissionCrmEventDto> CheckInAsync([FromRoute] Guid eventId, [FromRoute] Guid registrationId) =>
		service.CheckInAsync(eventId, registrationId);

	[HttpPost("events/{eventId}/registrations/{registrationId}/no-show")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.CheckInEvents)]
	public Task<AdmissionCrmEventDto> MarkNoShowAsync([FromRoute] Guid eventId, [FromRoute] Guid registrationId) =>
		service.MarkNoShowAsync(eventId, registrationId);
}
