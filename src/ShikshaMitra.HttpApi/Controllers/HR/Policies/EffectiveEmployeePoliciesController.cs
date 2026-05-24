using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShikshaMitra.HR.Policies;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;

namespace ShikshaMitra.Controllers.HR.Policies;

[Area("app")]
[RemoteService(Name = "Default")]
[Route("api/hr")]
[Authorize]
public class EffectiveEmployeePoliciesController(
	IHrPolicyAssignmentAppService assignmentAppService,
	IPolicyHolidayImpactAppService holidayImpactAppService) : AbpControllerBase
{
	[HttpGet("employees/{employeeId:guid}/effective-policies")]
	public Task<EffectiveEmployeePoliciesDto> GetEffectivePoliciesAsync(Guid employeeId, [FromQuery] DateTime date) =>
		assignmentAppService.GetEffectivePoliciesAsync(employeeId, date);

	[HttpGet("policy-assignment-preview")]
	public Task<EffectiveEmployeePoliciesDto> GetPolicyAssignmentPreviewAsync([FromQuery] PolicyAssignmentPreviewRequestDto input) =>
		assignmentAppService.GetPreviewAsync(input);

	[HttpGet("policy-holiday-impact-preview")]
	public Task<PolicyHolidayImpactPreviewDto> GetHolidayImpactPreviewAsync(
		[FromQuery] Guid academicYearId,
		[FromQuery] Guid? workCalendarPolicyId,
		[FromQuery] Guid? payrollPolicyId,
		[FromQuery] Guid? attendancePolicyId,
		[FromQuery] DateTime? fromDate,
		[FromQuery] DateTime? toDate,
		[FromQuery] string? month) =>
		holidayImpactAppService.GetPreviewAsync(academicYearId, workCalendarPolicyId, payrollPolicyId, attendancePolicyId, fromDate, toDate, month);
}
