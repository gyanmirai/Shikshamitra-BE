using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public interface IAdmissionCrmFinancialReadinessAppService : IApplicationService
{
	Task<AdmissionCrmFeePolicyDto> CreateFeePolicyAsync(Guid academicYearId, CreateAdmissionCrmFeePolicyDto input);
	Task<PagedResultDto<AdmissionCrmFeePolicyDto>> GetFeePoliciesAsync(Guid academicYearId, GetAdmissionCrmFeePolicyListInput input);
	Task<AdmissionCrmFeePolicyDto> SubmitFeePolicyAsync(Guid id);
	Task<AdmissionCrmFeePolicyDto> ApproveFeePolicyAsync(Guid id);
	Task<AdmissionCrmFeePolicyDto> PublishFeePolicyAsync(Guid id);
	Task<AdmissionCrmAdmissionQuotationDto> IssueQuotationAsync(Guid leadId, IssueAdmissionCrmQuotationDto input);
	Task<AdmissionCrmFinancialReadinessDto> GetFinancialReadinessAsync(Guid leadId);
	Task<AdmissionCrmAdmissionQuotationDto> RecordFinanceStateAsync(Guid quotationId, RecordAdmissionCrmFinanceStateDto input);
	Task<AdmissionCrmAdmissionQuotationDto> OverridePaymentGateAsync(Guid quotationId, OverrideAdmissionCrmPaymentGateDto input);
	Task<AdmissionCrmConcessionRequestDto> RequestConcessionAsync(Guid leadId, CreateAdmissionCrmConcessionRequestDto input);
	Task<IReadOnlyList<AdmissionCrmConcessionRequestDto>> GetConcessionsAsync(Guid leadId);
	Task<AdmissionCrmConcessionRequestDto> DecideConcessionAsync(Guid id, DecideAdmissionCrmConcessionDto input);
	Task<AdmissionCrmConcessionRequestDto> AcknowledgeConcessionFinanceAsync(Guid id, AcknowledgeAdmissionCrmConcessionFinanceDto input);
}
