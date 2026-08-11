using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmEntranceExamSchedulingWorkflow(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
	IRepository<AdmissionCrmExamSlot, Guid> slotRepository,
	AdmissionCrmEntranceExamAccess access,
	AdmissionCrmEntranceExamTimeline timeline,
	AdmissionCrmEntranceExamJourney journey) : ApplicationService, ITransientDependency
{
	private const AdmissionCrmExamReadinessField DefaultReadiness =
		AdmissionCrmExamReadinessField.Contact |
		AdmissionCrmExamReadinessField.AdmissionIntent |
		AdmissionCrmExamReadinessField.DesiredClass;

	public Task<AdmissionCrmEntranceExamDto> ScheduleAsync(
		Guid leadId,
		ScheduleAdmissionCrmEntranceExamDto input) => ScheduleCoreAsync(leadId, input, enforceUserScope: true);

	public Task<AdmissionCrmEntranceExamDto> ScheduleFromApplicantAsync(Guid leadId, Guid slotId) =>
		ScheduleCoreAsync(leadId, new ScheduleAdmissionCrmEntranceExamDto { EntranceExamSlotId = slotId }, false);

	private async Task<AdmissionCrmEntranceExamDto> ScheduleCoreAsync(
		Guid leadId,
		ScheduleAdmissionCrmEntranceExamDto input,
		bool enforceUserScope)
	{
		var lead = await access.GetLeadAsync(leadId, "ScheduleAsync");
		var cycle = await access.FindCycleAsync(lead.AcademicYearId);
		if (cycle?.EntranceExamRequired == false) throw new BusinessException("AdmissionCrm:EntranceExamDisabled");
		await EnsureRegistrationReadyAsync(lead, cycle?.RegistrationFormRequiredBeforeExam == true);
		EnsureExamReady(lead, cycle?.ExamReadinessFields ?? DefaultReadiness);

		var slot = await slotRepository.GetAsync(input.EntranceExamSlotId);
		if (slot.TenantId != CurrentTenant.Id || slot.AcademicYearId != lead.AcademicYearId)
			throw new BusinessException("AdmissionCrm:ExamSlotNotFound").WithData("EntranceExamSlotId", input.EntranceExamSlotId);
		if (enforceUserScope) await access.EnsureYearAsync(slot.AcademicYearId, "ScheduleAsync");
		slot.EnsureCanAcceptReservation(Clock.Now);
		if (!enforceUserScope && input.OverrideCapacity)
			throw new BusinessException("AdmissionCrm:ApplicantCapacityOverrideNotAllowed");
		if (input.OverrideCapacity) await EnsureCapacityOverridePermissionAsync();

		var existing = await access.FindExamByLeadAsync(lead.Id);
		var isReschedule = existing != null;
		await EnsureCapacityAsync(slot, input.OverrideCapacity, existing?.Id);
		await slotRepository.UpdateAsync(slot, autoSave: false);
		var previousStage = lead.Stage;
		if (existing == null)
		{
			existing = new AdmissionCrmEntranceExamRecord(
				GuidGenerator.Create(), CurrentTenant.Id, lead.AcademicYearId, lead.Id, slot,
				input.OverrideCapacity, input.CapacityOverrideReason, Clock.Now);
			await examRepository.InsertAsync(existing, autoSave: false);
		}
		else
		{
			existing.Reschedule(slot, input.OverrideCapacity, input.CapacityOverrideReason, Clock.Now);
			await examRepository.UpdateAsync(existing, autoSave: false);
		}

		lead.ChangeStage(AdmissionInquiryStage.ExamScheduled);
		await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.ExamScheduled,
			isReschedule ? "Entrance exam rescheduled" : "Entrance exam scheduled",
			$"{slot.SlotName} in room {slot.RoomNo}", existing.Id);
		await leadRepository.UpdateAsync(lead, autoSave: true);
		await journey.RunAsync(lead, AdmissionCrmJourneyTrigger.ExamScheduled);
		await journey.RunStageReachedIfChangedAsync(lead, previousStage);
		return AdmissionCrmEntranceExamMapper.ToDto(existing, lead);
	}

	private async Task EnsureCapacityAsync(AdmissionCrmExamSlot slot, bool overrideCapacity, Guid? excludedExamId)
	{
		var exams = await examRepository.GetQueryableAsync();
		var crmCount = await AsyncExecuter.CountAsync(exams.Where(x =>
			x.TenantId == CurrentTenant.Id && x.EntranceExamSlotId == slot.Id &&
			(!excludedExamId.HasValue || x.Id != excludedExamId.Value)));
		if (crmCount >= slot.StudentLimit && !overrideCapacity)
			throw new BusinessException("AdmissionCrm:ExamSlotCapacityFull")
				.WithData("EntranceExamSlotId", slot.Id).WithData("StudentLimit", slot.StudentLimit)
				.WithData("ScheduledCount", crmCount);
	}

	private async Task EnsureCapacityOverridePermissionAsync()
	{
		if (!await AuthorizationService.IsGrantedAsync(
			    ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverrideExamCapacity))
			throw new AbpAuthorizationException("Capacity override permission is required.");
	}

	private async Task EnsureRegistrationReadyAsync(AdmissionInquiryLead lead, bool required)
	{
		if (!required || (lead.RegistrationFormIssuedAt.HasValue &&
		                  !string.IsNullOrWhiteSpace(lead.RegistrationFormNumber))) return;
		throw new BusinessException("AdmissionCrm:RegistrationFormRequiredBeforeExam")
			.WithData("LeadId", lead.Id).WithData("AcademicYearId", lead.AcademicYearId);
	}

	private static void EnsureExamReady(AdmissionInquiryLead lead, AdmissionCrmExamReadinessField fields)
	{
		if (lead.Stage == AdmissionInquiryStage.Archived)
			throw new BusinessException("AdmissionCrm:ArchivedLeadCannotScheduleExam").WithData("LeadId", lead.Id);
		var missing = new List<string>();
		if (fields.HasFlag(AdmissionCrmExamReadinessField.Contact) &&
		    string.IsNullOrWhiteSpace(lead.PrimaryContactNo) && string.IsNullOrWhiteSpace(lead.Email)) missing.Add("Contact");
		if (fields.HasFlag(AdmissionCrmExamReadinessField.AdmissionIntent) &&
		    string.IsNullOrWhiteSpace(lead.AdmissionIntent)) missing.Add("Admission intent");
		if (fields.HasFlag(AdmissionCrmExamReadinessField.DesiredClass) && !lead.TargetStandardId.HasValue &&
		    string.IsNullOrWhiteSpace(lead.DesiredClassName)) missing.Add("Desired class");
		if (fields.HasFlag(AdmissionCrmExamReadinessField.StudentName) && string.IsNullOrWhiteSpace(lead.FirstName) &&
		    string.IsNullOrWhiteSpace(lead.MiddleName) && string.IsNullOrWhiteSpace(lead.LastName)) missing.Add("Student name");
		if (fields.HasFlag(AdmissionCrmExamReadinessField.ParentGuardianName) &&
		    string.IsNullOrWhiteSpace(lead.ParentGuardianName)) missing.Add("Parent or guardian name");
		if (fields.HasFlag(AdmissionCrmExamReadinessField.InquirySource) && !lead.InquirySourceId.HasValue &&
		    string.IsNullOrWhiteSpace(lead.InquirySourceSnapshot)) missing.Add("Inquiry source");
		if (missing.Count > 0)
			throw new BusinessException("AdmissionCrm:ExamReadinessIncomplete")
				.WithData("LeadId", lead.Id).WithData("MissingFields", string.Join(", ", missing));
	}
}
