using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmPublicStatusCoreReader(
	IRepository<AdmissionCrmPipelineStage, Guid> stageRepository,
	IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
	IRepository<AdmissionCrmRouteAssignment, Guid> routeRepository,
	IRepository<AdmissionCrmActivation, Guid> activationRepository,
	IRepository<AdmissionCrmAdmissionQuotation, Guid> quotationRepository) : ApplicationService, ITransientDependency
{
	public async Task<string> GetStageNameAsync(Guid academicYearId, AdmissionInquiryStage stage)
	{
		var queryable = await stageRepository.GetQueryableAsync();
		var configured = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(item =>
			item.TenantId == CurrentTenant.Id && item.AcademicYearId == academicYearId && item.SystemStage == stage));
		return configured?.StageName ?? stage.ToString();
	}

	public async Task<AdmissionCrmEntranceExamRecord?> GetExamAsync(Guid leadId)
	{
		var queryable = await examRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable
			.Where(exam => exam.TenantId == CurrentTenant.Id && exam.LeadId == leadId)
			.OrderByDescending(exam => exam.ScheduledAt)
			.ThenByDescending(exam => exam.CreationTime));
	}

	public async Task<AdmissionApplicantRouteStatusDto?> GetRouteAsync(Guid leadId)
	{
		var queryable = await routeRepository.GetQueryableAsync();
		var assignment = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(item =>
			item.TenantId == CurrentTenant.Id && item.LeadId == leadId && item.IsPrimary));
		return assignment == null
			? null
			: new AdmissionApplicantRouteStatusDto
			{
				RouteType = assignment.RouteType,
				EligibilityStatus = assignment.Status,
				AssignedAt = assignment.AssignedAt,
				EvaluatedAt = assignment.EvaluatedAt
			};
	}

	public async Task<AdmissionCrmActivation?> GetActivationAsync(Guid leadId)
	{
		var queryable = await activationRepository.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(activation =>
			activation.TenantId == CurrentTenant.Id && activation.LeadId == leadId));
	}

	public async Task<AdmissionApplicantFinancialStatusDto?> GetFinancialStatusAsync(Guid leadId)
	{
		var queryable = await quotationRepository.GetQueryableAsync();
		var quotation = await AsyncExecuter.FirstOrDefaultAsync(queryable
			.Where(item => item.TenantId == CurrentTenant.Id && item.LeadId == leadId &&
				item.Status == AdmissionCrmQuotationStatus.Issued)
			.OrderByDescending(item => item.Version));
		if (quotation == null)
		{
			return null;
		}

		var components = JsonSerializer.Deserialize<IReadOnlyList<AdmissionCrmFeeComponentDto>>(
			quotation.ComponentsSnapshotJson, AdmissionCrmPublicFormat.JsonOptions) ?? [];
		return new AdmissionApplicantFinancialStatusDto
		{
			QuotationId = quotation.Id,
			QuotationNumber = quotation.QuotationNumber,
			Currency = quotation.Currency,
			Components = components.Select(component => new AdmissionApplicantFeeComponentDto
			{
				Name = component.Name,
				Amount = component.Amount,
				IsRefundable = component.IsRefundable,
				ParentDescription = component.ParentDescription
			}).ToList(),
			GrossAmount = quotation.GrossAmount,
			ConcessionAmount = quotation.ConcessionAmount,
			NetAmount = quotation.NetAmount,
			ConfirmedAmount = quotation.ConfirmedAmount,
			OutstandingAmount = Math.Max(0, quotation.NetAmount - quotation.ConfirmedAmount),
			FinanceState = quotation.FinanceState,
			GateType = quotation.GateType,
			GateMilestone = quotation.GateMilestone,
			IsGateSatisfied = quotation.IsGateSatisfiedAt(Clock.Now),
			ValidUntil = quotation.ValidUntil,
			FinanceReference = quotation.FinanceReference
		};
	}

	internal static AdmissionApplicantActivationStatusDto ToActivationDto(AdmissionCrmActivation activation) => new()
	{
		ActivationReference = activation.ActivationReference,
		Status = activation.Status,
		FirstDayReadyAt = activation.FirstDayReadyAt
	};
}
