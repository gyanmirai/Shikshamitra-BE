using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmLeadLifecycleWorkflow(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	AdmissionCrmLeadAccess access,
	AdmissionCrmLeadConfigurationReader configuration,
	AdmissionCrmLeadTimelineWriter timeline,
	AdmissionCrmLeadDuplicateDetector duplicates) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmLeadDto> RecalculateScoreAsync(Guid leadId)
	{
		var lead = await access.GetLeadForCurrentTenantAsync(leadId, nameof(RecalculateScoreAsync));
		var scoringRules = await configuration.GetLeadScoringRulesAsync(lead.AcademicYearId);
		lead.UpdateScore(AdmissionCrmLeadScoreCalculator.Calculate(lead, scoringRules));
		await leadRepository.UpdateAsync(lead, autoSave: true);
		await timeline.AddTimelineEventAsync(
			lead,
			AdmissionCrmTimelineEventType.ScoreUpdated,
			"Lead score recalculated",
			$"Score: {lead.LeadScore}",
			relatedEntityId: lead.Id);
		return AdmissionCrmLeadMapper.ToDto(lead, Array.Empty<AdmissionCrmLeadDuplicateDto>());
	}

	public async Task<AdmissionCrmLeadDto> MergeDuplicateAsync(
		Guid targetLeadId,
		MergeAdmissionCrmDuplicateLeadDto input)
	{
		var targetLead = await access.GetLeadForCurrentTenantAsync(targetLeadId, nameof(MergeDuplicateAsync));
		var duplicateLead = await access.GetLeadForCurrentTenantAsync(input.DuplicateLeadId, nameof(MergeDuplicateAsync));

		if (targetLead.AcademicYearId != duplicateLead.AcademicYearId)
		{
			throw new BusinessException("AdmissionCrm:DuplicateMergeAcademicYearMismatch")
				.WithData("TargetLeadId", targetLead.Id)
				.WithData("DuplicateLeadId", duplicateLead.Id);
		}

		if (!AdmissionCrmLeadDuplicateMatcher.AreLikelyDuplicates(targetLead, duplicateLead))
		{
			throw new BusinessException("AdmissionCrm:DuplicateMergeRequiresMatch")
				.WithData("TargetLeadId", targetLead.Id)
				.WithData("DuplicateLeadId", duplicateLead.Id);
		}

		duplicateLead.MergeInto(targetLead.Id, input.Reason, Clock.Now);
		await leadRepository.UpdateAsync(duplicateLead, autoSave: true);
		await timeline.AddTimelineEventAsync(
			targetLead,
			AdmissionCrmTimelineEventType.DuplicateMerged,
			"Duplicate merged",
			input.Reason,
			relatedEntityId: duplicateLead.Id);
		await timeline.AddTimelineEventAsync(
			duplicateLead,
			AdmissionCrmTimelineEventType.DuplicateMerged,
			"Merged into another lead",
			input.Reason,
			relatedEntityId: targetLead.Id);

		var likelyDuplicates = await duplicates.FindDuplicatesAsync(
			targetLead.AcademicYearId,
			targetLead.NormalizedPrimaryContactNo,
			targetLead.NormalizedEmail,
			AdmissionCrmDuplicateProfileProbe.FromLead(targetLead),
			targetLead.Id);
		return AdmissionCrmLeadMapper.ToDto(targetLead, likelyDuplicates);
	}

	public async Task<AdmissionCrmLeadDto> ArchiveAsync(Guid leadId, ArchiveAdmissionCrmLeadDto input)
	{
		var lead = await access.GetLeadForCurrentTenantAsync(leadId, nameof(ArchiveAsync));
		lead.Archive(input.Reason, Clock.Now);
		await leadRepository.UpdateAsync(lead, autoSave: true);
		await timeline.AddTimelineEventAsync(
			lead,
			AdmissionCrmTimelineEventType.Archived,
			"Lead archived",
			input.Reason,
			relatedEntityId: lead.Id);
		return AdmissionCrmLeadMapper.ToDto(lead, Array.Empty<AdmissionCrmLeadDuplicateDto>());
	}

	public async Task<AdmissionCrmLeadDto> ReopenAsync(Guid leadId, ReopenAdmissionCrmLeadDto input)
	{
		var lead = await access.GetLeadForCurrentTenantAsync(leadId, nameof(ReopenAsync));
		lead.Reopen(input.Reason, Clock.Now);
		await leadRepository.UpdateAsync(lead, autoSave: true);
		await timeline.AddTimelineEventAsync(
			lead,
			AdmissionCrmTimelineEventType.Reopened,
			"Lead reopened",
			input.Reason,
			relatedEntityId: lead.Id);
		return AdmissionCrmLeadMapper.ToDto(lead, Array.Empty<AdmissionCrmLeadDuplicateDto>());
	}
}