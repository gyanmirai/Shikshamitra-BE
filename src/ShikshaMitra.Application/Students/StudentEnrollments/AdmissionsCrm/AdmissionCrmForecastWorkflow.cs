using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmForecastWorkflow(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	IRepository<AdmissionCrmSeatOffer, Guid> offerRepository,
	IRepository<AdmissionCrmActivation, Guid> activationRepository,
	IRepository<AdmissionCrmSeatPool, Guid> seatPoolRepository,
	IRepository<AdmissionCrmPipelineStage, Guid> pipelineStageRepository,
	IRepository<AdmissionCrmCycleConfiguration, Guid> cycleConfigurationRepository,
	IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
	StudentScopeAccessService scopeAccess)
	: ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmForecastDto> GetAsync(Guid academicYearId)
	{
		await scopeAccess.EnsureCanAccessAcademicYearAsync(academicYearId, "GetAdvisoryForecastAsync");
		var leadQuery = await leadRepository.GetQueryableAsync();
		var scopedLeadQuery = leadQuery.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId);
		scopedLeadQuery = await scopeAccess.ApplyCurrentUserAdmissionLeadScopeAsync(scopedLeadQuery);
		var leads = await AsyncExecuter.ToListAsync(scopedLeadQuery);
		var scopedLeadIds = leads.Select(x => x.Id).ToList();
		var offerQuery = await offerRepository.GetQueryableAsync();
		var offers = await AsyncExecuter.ToListAsync(offerQuery.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && scopedLeadIds.Contains(x.LeadId)));
		var activationQuery = await activationRepository.GetQueryableAsync();
		var activations = await AsyncExecuter.ToListAsync(activationQuery.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && scopedLeadIds.Contains(x.LeadId)));
		var capacityIsScopeComplete = await scopeAccess.CurrentUserHasAcademicYearWideScopeAsync(academicYearId);
		var seatPools = new List<AdmissionCrmSeatPool>();
		if (capacityIsScopeComplete)
		{
			var seatPoolQuery = await seatPoolRepository.GetQueryableAsync();
			seatPools = await AsyncExecuter.ToListAsync(seatPoolQuery.Where(x => x.TenantId == CurrentTenant.Id &&
				x.AcademicYearId == academicYearId && x.Status == AdmissionCrmSeatPoolStatus.Active));
		}
		var pipelineStageQuery = await pipelineStageRepository.GetQueryableAsync();
		var newInquiryStage = await AsyncExecuter.FirstOrDefaultAsync(pipelineStageQuery.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && x.SystemStage == AdmissionInquiryStage.NewInquiry));
		var cycleQuery = await cycleConfigurationRepository.GetQueryableAsync();
		var cycle = await AsyncExecuter.FirstOrDefaultAsync(cycleQuery.Where(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == academicYearId));
		var firstResponseSlaHours = newInquiryStage?.SlaTargetHours ?? cycle?.DefaultLeadSlaHours ?? 24;
		var timelineQuery = await timelineRepository.GetQueryableAsync();
		var responseLeadIds = (await AsyncExecuter.ToListAsync(timelineQuery.Where(x => x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId && scopedLeadIds.Contains(x.LeadId) && x.ActorUserId != null)))
			.Where(IsFirstResponseEvent).Select(x => x.LeadId).ToHashSet();
		var responded = offers.Count(x => x.Status is AdmissionCrmSeatOfferStatus.Accepted or AdmissionCrmSeatOfferStatus.Declined);
		var accepted = offers.Count(x => x.Status == AdmissionCrmSeatOfferStatus.Accepted);
		var offerRate = responded == 0 ? 0m : (decimal)accepted / responded;
		var eligible = leads.Count(x => x.Stage >= AdmissionInquiryStage.Eligible && x.Stage is not (AdmissionInquiryStage.Rejected or AdmissionInquiryStage.Archived));
		var ready = activations.Count(x => x.Status == AdmissionCrmActivationStatus.FirstDayReady);
		var activationRate = eligible == 0 ? 0m : (decimal)ready / eligible;
		var open = leads.Count(x => x.Stage is not (AdmissionInquiryStage.Eligible or AdmissionInquiryStage.Rejected or AdmissionInquiryStage.Archived));
		var projectedAdditionalActivations = (int)Math.Round(open * activationRate);
		var remainingSeatCapacity = seatPools.Sum(x => x.RemainingCapacity);
		var now = new DateTimeOffset(Clock.Now);
		var pending = leads.Where(x => x.Stage is not (AdmissionInquiryStage.Rejected or AdmissionInquiryStage.Archived) && !responseLeadIds.Contains(x.Id)).ToList();
		var atRisk = pending.Count(x =>
		{
			var createdAt = new DateTimeOffset(DateTime.SpecifyKind(x.CreationTime, DateTimeKind.Utc));
			var dueAt = createdAt.AddHours(firstResponseSlaHours);
			return now <= dueAt && now >= createdAt.AddHours(firstResponseSlaHours * 0.75);
		});
		var breached = pending.Count(x => new DateTimeOffset(DateTime.SpecifyKind(x.CreationTime, DateTimeKind.Utc))
			.AddHours(firstResponseSlaHours) < now);
		return new AdmissionCrmForecastDto
		{
			AcademicYearId = academicYearId, GeneratedAt = Clock.Now, HistoricalLeadCount = leads.Count, CurrentOpenLeadCount = open,
			ObservedOfferAcceptanceRate = Math.Round(offerRate, 4), ObservedActivationRate = Math.Round(activationRate, 4),
			ProjectedAdditionalAcceptedOffers = (int)Math.Round(open * offerRate), ProjectedAdditionalActivations = projectedAdditionalActivations,
			ApprovedSeatCapacity = seatPools.Sum(x => x.ApprovedCapacity), RemainingSeatCapacity = remainingSeatCapacity,
			ProjectedSeatSurplus = remainingSeatCapacity - projectedAdditionalActivations, CapacityIsScopeComplete = capacityIsScopeComplete,
			FirstResponseAtRiskCount = atRisk, FirstResponseAlreadyBreachedCount = breached,
			Confidence = leads.Count >= 200 ? "Moderate" : leads.Count >= 50 ? "Low" : "Insufficient sample",
			Inputs = ["Current-cycle lead volume", "Observed issued-offer responses", "Observed eligible-to-activation conversion"],
			Limitations = ["Current-cycle descriptive projection only.", "No protected attributes are used.", "The projection cannot make or change an admission decision."],
			IsAdvisory = true
		};
	}

	private static bool IsFirstResponseEvent(AdmissionCrmTimelineEvent value) => value.EventType is
		AdmissionCrmTimelineEventType.NoteAdded or AdmissionCrmTimelineEventType.TaskCompleted or AdmissionCrmTimelineEventType.StageChanged or
		AdmissionCrmTimelineEventType.MessageTemplateChanged or AdmissionCrmTimelineEventType.RegistrationFormIssued or AdmissionCrmTimelineEventType.ExamScheduled or
		AdmissionCrmTimelineEventType.ExamAttendanceMarked or AdmissionCrmTimelineEventType.ExamResultRecorded or AdmissionCrmTimelineEventType.EligibilityDecided or
		AdmissionCrmTimelineEventType.Archived or AdmissionCrmTimelineEventType.Reopened or AdmissionCrmTimelineEventType.DuplicateMerged;
}
