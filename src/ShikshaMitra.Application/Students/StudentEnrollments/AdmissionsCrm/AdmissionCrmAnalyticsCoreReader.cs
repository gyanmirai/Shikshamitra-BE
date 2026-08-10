using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmAnalyticsCoreReader(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	IRepository<AdmissionCrmEntranceExamRecord, Guid> examRepository,
	IRepository<AdmissionCrmFollowUpTask, Guid> taskRepository,
	IRepository<AdmissionCrmPipelineStage, Guid> stageRepository,
	IRepository<AdmissionCrmTimelineEvent, Guid> timelineRepository,
	IRepository<AdmissionCrmAutomationRun, Guid> automationRepository,
	IRepository<AdmissionCrmCycleConfiguration, Guid> cycleRepository,
	StudentScopeAccessService scopeAccess) : ApplicationService, ITransientDependency
{
	internal async Task<AdmissionCrmAnalyticsCoreSnapshot> LoadAsync(Guid yearId,
		GetAdmissionCrmAnalyticsSummaryInput input)
	{
		await scopeAccess.EnsureCanAccessAcademicYearAsync(yearId, "GetSummaryAsync");
		var hasWideScope = await scopeAccess.CurrentUserHasAcademicYearWideScopeAsync(yearId);
		var leadQuery = (await leadRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId);
		leadQuery = await scopeAccess.ApplyCurrentUserAdmissionLeadScopeAsync(leadQuery);
		if (input.Channel.HasValue) leadQuery = leadQuery.Where(x => x.Channel == input.Channel.Value);
		if (input.InquirySourceId.HasValue) leadQuery = leadQuery.Where(x => x.InquirySourceId == input.InquirySourceId.Value);
		if (!string.IsNullOrWhiteSpace(input.CampaignName))
		{
			var campaign = input.CampaignName.Trim().ToLower();
			leadQuery = leadQuery.Where(x => x.CampaignName != null && x.CampaignName.ToLower() == campaign);
		}
		if (input.OwnerUserId.HasValue) leadQuery = leadQuery.Where(x => x.OwnerUserId == input.OwnerUserId.Value);
		if (input.Stage.HasValue) leadQuery = leadQuery.Where(x => x.Stage == input.Stage.Value);
		if (input.TargetStandardId.HasValue) leadQuery = leadQuery.Where(x => x.TargetStandardId == input.TargetStandardId.Value);
		if (input.MinimumLeadScore.HasValue) leadQuery = leadQuery.Where(x => x.LeadScore >= input.MinimumLeadScore.Value);
		if (input.MaximumLeadScore.HasValue) leadQuery = leadQuery.Where(x => x.LeadScore <= input.MaximumLeadScore.Value);
		var dimensionLeads = await AsyncExecuter.ToListAsync(leadQuery);
		var dimensionIds = dimensionLeads.Select(x => x.Id).ToList();
		var leads = AdmissionCrmAnalyticsFilters.ApplyLeadDateFilter(dimensionLeads, input).ToList();
		var summaryIds = leads.Select(x => x.Id).ToList();

		var dimensionExams = await AsyncExecuter.ToListAsync((await examRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && dimensionIds.Contains(x.LeadId)));
		var exams = AdmissionCrmAnalyticsFilters.ApplyExamDateFilter(
			dimensionExams.Where(x => summaryIds.Contains(x.LeadId)), input).ToList();
		var dimensionTasks = await AsyncExecuter.ToListAsync((await taskRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && dimensionIds.Contains(x.LeadId)));
		var tasks = AdmissionCrmAnalyticsFilters.ApplyTaskDateFilter(
			dimensionTasks.Where(x => summaryIds.Contains(x.LeadId)), input).ToList();
		var stages = await AsyncExecuter.ToListAsync((await stageRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId));
		var dimensionTimeline = await AsyncExecuter.ToListAsync((await timelineRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && dimensionIds.Contains(x.LeadId)));
		var summaryTimeline = AdmissionCrmAnalyticsFilters.ApplyTimelineDateFilter(
			dimensionTimeline.Where(x => summaryIds.Contains(x.LeadId)), input).ToList();
		var automationRuns = await AsyncExecuter.ToListAsync((await automationRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId &&
			((x.LeadId.HasValue && dimensionIds.Contains(x.LeadId.Value)) || (!x.LeadId.HasValue && hasWideScope))));
		automationRuns = AdmissionCrmAnalyticsFilters.ApplyAutomationRunDateFilter(automationRuns, input).ToList();
		var cycle = await AsyncExecuter.FirstOrDefaultAsync((await cycleRepository.GetQueryableAsync()).Where(x =>
			x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId));
		return new AdmissionCrmAnalyticsCoreSnapshot(dimensionLeads, leads, dimensionExams, exams,
			dimensionTasks, tasks, stages, dimensionTimeline, summaryTimeline, automationRuns, cycle);
	}
}
