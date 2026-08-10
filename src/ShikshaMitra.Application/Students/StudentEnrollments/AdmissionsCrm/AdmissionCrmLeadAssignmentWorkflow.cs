using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmLeadAssignmentWorkflow(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	AdmissionCrmLeadAccess access,
	AdmissionCrmStaffEligibilityService staffEligibility,
	AdmissionCrmRegistrationCoordinator registrationCoordinator,
	StudentEnrollmentManager studentEnrollmentManager,
	AdmissionCrmLeadConfigurationReader configuration,
	AdmissionCrmLeadTimelineWriter timeline,
	AdmissionCrmJourneyWorkflow journey) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmLeadDto> AssignOwnerAsync(Guid leadId, AssignAdmissionCrmLeadOwnerDto input)
	{
		var lead = await access.GetLeadForCurrentTenantAsync(leadId, nameof(AssignOwnerAsync));
		await staffEligibility.EnsureOwnerEligibleAsync(input.OwnerUserId, lead.AcademicYearId);
		lead.AssignOwner(input.OwnerUserId);
		await leadRepository.UpdateAsync(lead, autoSave: true);
		await timeline.AddTimelineEventAsync(
			lead,
			AdmissionCrmTimelineEventType.OwnerAssigned,
			input.OwnerUserId.HasValue ? "Owner assigned" : "Owner cleared",
			input.OwnerUserId.HasValue ? $"Owner user: {input.OwnerUserId}" : "Lead owner cleared.",
			relatedEntityId: input.OwnerUserId);
		return AdmissionCrmLeadMapper.ToDto(lead, Array.Empty<AdmissionCrmLeadDuplicateDto>());
	}

	public async Task<AdmissionCrmLeadDto> ChangePriorityAsync(
		Guid leadId,
		ChangeAdmissionCrmLeadPriorityDto input)
	{
		var lead = await access.GetLeadForCurrentTenantAsync(leadId, nameof(ChangePriorityAsync));
		var reason = string.IsNullOrWhiteSpace(input.Reason)
			? null
			: Check.Length(input.Reason.Trim(), nameof(input.Reason), AdmissionCrmConsts.MaxNotesLength);
		var previousPriority = lead.Priority;
		lead.ChangePriority(input.Priority);
		await leadRepository.UpdateAsync(lead, autoSave: true);

		await timeline.AddTimelineEventAsync(
			lead,
			AdmissionCrmTimelineEventType.PriorityChanged,
			"Lead priority changed",
			reason is null
				? $"{previousPriority} -> {input.Priority}"
				: $"{previousPriority} -> {input.Priority}. Reason: {reason}",
			relatedEntityId: lead.Id);

		return AdmissionCrmLeadMapper.ToDto(lead, Array.Empty<AdmissionCrmLeadDuplicateDto>());
	}

	public async Task<AdmissionCrmLeadDto> IssueRegistrationFormAsync(
		Guid leadId,
		IssueAdmissionCrmRegistrationFormDto input)
	{
		var lead = await access.GetLeadForCurrentTenantAsync(leadId, nameof(IssueRegistrationFormAsync));
		await registrationCoordinator.EnsureStandardIssuanceEligibleAsync(leadId);
		var formNumber = input.FormNumber.Trim();
		await studentEnrollmentManager.ValidateFormNumberUniqueAsync(
			formNumber,
			lead.AcademicYearId);
		await EnsureCrmRegistrationFormNumberUniqueAsync(
			formNumber,
			lead.AcademicYearId,
			lead.Id);

		lead.IssueRegistrationForm(formNumber, input.Amount, Clock.Now);
		await leadRepository.UpdateAsync(lead, autoSave: true);
		await timeline.AddTimelineEventAsync(
			lead,
			AdmissionCrmTimelineEventType.RegistrationFormIssued,
			"Registration form issued",
			$"Form {lead.RegistrationFormNumber} issued for amount {lead.RegistrationFormAmount:0.##}.",
			relatedEntityId: lead.Id);

		return AdmissionCrmLeadMapper.ToDto(lead, Array.Empty<AdmissionCrmLeadDuplicateDto>());
	}

	public async Task<AdmissionCrmLeadDto> ChangeStageAsync(Guid leadId, ChangeAdmissionCrmLeadStageDto input)
	{
		if (input.OverrideWorkflowGuards)
		{
			await AuthorizationService.CheckAsync(
				ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.BulkOperations);
		}

		var lead = await access.GetLeadForCurrentTenantAsync(leadId, nameof(ChangeStageAsync));
		var reason = Check.NotNullOrWhiteSpace(
			input.Reason,
			nameof(input.Reason),
			maxLength: AdmissionCrmConsts.MaxNotesLength);
		if (!input.OverrideWorkflowGuards && lead.Stage is AdmissionInquiryStage.ExamScheduled
			or AdmissionInquiryStage.ExamCompleted
			or AdmissionInquiryStage.Eligible
			or AdmissionInquiryStage.Waitlisted
			or AdmissionInquiryStage.Rejected)
		{
			throw new BusinessException("AdmissionCrm:StageManagedByDedicatedWorkflow")
				.WithData("LeadId", lead.Id)
				.WithData("CurrentStage", lead.Stage);
		}

		if (!input.OverrideWorkflowGuards && input.Stage is AdmissionInquiryStage.ExamScheduled
			or AdmissionInquiryStage.ExamCompleted
			or AdmissionInquiryStage.Eligible
			or AdmissionInquiryStage.Waitlisted
			or AdmissionInquiryStage.Rejected
			or AdmissionInquiryStage.Archived)
		{
			throw new BusinessException("AdmissionCrm:StageManagedByDedicatedWorkflow")
				.WithData("LeadId", lead.Id)
				.WithData("RequestedStage", input.Stage);
		}

		var previousStage = lead.Stage;
		await configuration.EnsureConfiguredStageTransitionAllowedAsync(lead, input.Stage);
		lead.ChangeStage(input.Stage);
		await leadRepository.UpdateAsync(lead, autoSave: true);
		await timeline.AddTimelineEventAsync(
			lead,
			AdmissionCrmTimelineEventType.StageChanged,
			"Pipeline stage changed",
			$"{previousStage} -> {input.Stage}. Reason: {reason}" +
			(input.OverrideWorkflowGuards ? " Workflow guards overridden." : string.Empty),
			relatedEntityId: lead.Id);

		if (previousStage != lead.Stage)
		{
			await journey.RunAsync(
				lead.AcademicYearId,
				new RunAdmissionCrmJourneyRulesDto
				{
					Trigger = AdmissionCrmJourneyTrigger.StageReached,
					LeadId = lead.Id,
					Stage = lead.Stage
				});
		}

		return AdmissionCrmLeadMapper.ToDto(lead, Array.Empty<AdmissionCrmLeadDuplicateDto>());
	}

	private async Task EnsureCrmRegistrationFormNumberUniqueAsync(
		string formNumber,
		Guid academicYearId,
		Guid excludeLeadId)
	{
		var queryable = await leadRepository.GetQueryableAsync();
		var exists = await AsyncExecuter.AnyAsync(queryable.Where(lead =>
			lead.TenantId == CurrentTenant.Id &&
			lead.AcademicYearId == academicYearId &&
			lead.Id != excludeLeadId &&
			lead.RegistrationFormNumber == formNumber));
		if (exists)
		{
			throw new BusinessException("AdmissionCrm:RegistrationFormNumberAlreadyExists")
				.WithData("FormNumber", formNumber)
				.WithData("AcademicYearId", academicYearId);
		}
	}
}