using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
public class AdmissionCrmFinancialReadinessController(
	IAdmissionCrmFinancialReadinessAppService appService) : AbpControllerBase
{
	[HttpPost("{academicYearId}/financial/fee-policies")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageFeePolicies)]
	public Task<AdmissionCrmFeePolicyDto> CreateFeePolicyAsync(
		Guid academicYearId, [FromBody] CreateAdmissionCrmFeePolicyDto input) =>
		appService.CreateFeePolicyAsync(academicYearId, input);

	[HttpGet("{academicYearId}/financial/fee-policies")]
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewFinancialStatus)]
	public Task<PagedResultDto<AdmissionCrmFeePolicyDto>> GetFeePoliciesAsync(
		Guid academicYearId, [FromQuery] GetAdmissionCrmFeePolicyListInput input) =>
		appService.GetFeePoliciesAsync(academicYearId, input);

	[HttpPost("financial/fee-policies/{id}/submit")]
	public Task<AdmissionCrmFeePolicyDto> SubmitFeePolicyAsync(Guid id) => appService.SubmitFeePolicyAsync(id);

	[HttpPost("financial/fee-policies/{id}/approve")]
	public Task<AdmissionCrmFeePolicyDto> ApproveFeePolicyAsync(Guid id) => appService.ApproveFeePolicyAsync(id);

	[HttpPost("financial/fee-policies/{id}/publish")]
	public Task<AdmissionCrmFeePolicyDto> PublishFeePolicyAsync(Guid id) => appService.PublishFeePolicyAsync(id);

	[HttpPost("leads/{leadId}/financial/quotations")]
	public Task<AdmissionCrmAdmissionQuotationDto> IssueQuotationAsync(
		Guid leadId, [FromBody] IssueAdmissionCrmQuotationDto input) =>
		appService.IssueQuotationAsync(leadId, input);

	[HttpGet("leads/{leadId}/financial/readiness")]
	public Task<AdmissionCrmFinancialReadinessDto> GetFinancialReadinessAsync(Guid leadId) =>
		appService.GetFinancialReadinessAsync(leadId);

	[HttpPut("financial/quotations/{quotationId}/finance-state")]
	public Task<AdmissionCrmAdmissionQuotationDto> RecordFinanceStateAsync(
		Guid quotationId, [FromBody] RecordAdmissionCrmFinanceStateDto input) =>
		appService.RecordFinanceStateAsync(quotationId, input);

	[HttpPost("financial/quotations/{quotationId}/override-gate")]
	public Task<AdmissionCrmAdmissionQuotationDto> OverridePaymentGateAsync(
		Guid quotationId, [FromBody] OverrideAdmissionCrmPaymentGateDto input) =>
		appService.OverridePaymentGateAsync(quotationId, input);

	[HttpPost("leads/{leadId}/financial/concessions")]
	public Task<AdmissionCrmConcessionRequestDto> RequestConcessionAsync(
		Guid leadId, [FromBody] CreateAdmissionCrmConcessionRequestDto input) =>
		appService.RequestConcessionAsync(leadId, input);

	[HttpGet("leads/{leadId}/financial/concessions")]
	public Task<IReadOnlyList<AdmissionCrmConcessionRequestDto>> GetConcessionsAsync(Guid leadId) =>
		appService.GetConcessionsAsync(leadId);

	[HttpPost("financial/concessions/{id}/decision")]
	public Task<AdmissionCrmConcessionRequestDto> DecideConcessionAsync(
		Guid id, [FromBody] DecideAdmissionCrmConcessionDto input) =>
		appService.DecideConcessionAsync(id, input);

	[HttpPost("financial/concessions/{id}/finance-acknowledgement")]
	public Task<AdmissionCrmConcessionRequestDto> AcknowledgeConcessionFinanceAsync(
		Guid id, [FromBody] AcknowledgeAdmissionCrmConcessionFinanceDto input) =>
		appService.AcknowledgeConcessionFinanceAsync(id, input);
}
