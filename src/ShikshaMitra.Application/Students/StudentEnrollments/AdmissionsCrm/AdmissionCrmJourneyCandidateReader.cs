using System;
using System.Collections.Generic;
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

public class AdmissionCrmJourneyCandidateReader(
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	StudentScopeAccessService scopeAccess,
	IRepository<AdmissionCrmFollowUpTask, Guid> taskRepository,
	IRepository<AdmissionCrmMessageDeliveryLog, Guid> deliveryLogRepository) : ApplicationService, ITransientDependency
{
	internal async Task<IReadOnlyList<AdmissionInquiryLead>> GetJourneyCandidateLeadsAsync(
		Guid academicYearId,
		RunAdmissionCrmJourneyRulesDto input,
		bool enforceUserScope)
	{
		var queryable = await leadRepository.GetQueryableAsync();
		var query = queryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == academicYearId &&
			x.MergedIntoLeadId == null);
		if (enforceUserScope)
		{
			query = await scopeAccess.ApplyCurrentUserAdmissionLeadScopeAsync(query);
		}

		if (input.LeadId.HasValue)
		{
			query = query.Where(x => x.Id == input.LeadId.Value);
		}

		if (input.Trigger == AdmissionCrmJourneyTrigger.StageReached && input.Stage.HasValue)
		{
			query = query.Where(x => x.Stage == input.Stage.Value);
		}
		else if (input.Trigger == AdmissionCrmJourneyTrigger.StaleMarked)
		{
			query = query.Where(x => x.Stage == AdmissionInquiryStage.Stale);
		}
		else if (input.Trigger == AdmissionCrmJourneyTrigger.LeadCreated)
		{
			query = query.Where(x => x.Stage == AdmissionInquiryStage.NewInquiry);
		}

		var leads = await AsyncExecuter.ToListAsync(query);
		return leads
			.OrderByDescending(x => x.LastActivityTime)
			.ToList();
	}

	internal async Task<bool> HasPendingJourneyTaskAsync(AdmissionInquiryLead lead, string title)
	{
		var taskQueryable = await taskRepository.GetQueryableAsync();
		return await AsyncExecuter.AnyAsync(taskQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == lead.AcademicYearId &&
			x.LeadId == lead.Id &&
			x.Status == AdmissionCrmTaskStatus.Pending &&
			x.Title == title));
	}

	internal async Task<bool> HasJourneyMessageLogAsync(AdmissionInquiryLead lead, Guid templateId)
	{
		var deliveryQueryable = await deliveryLogRepository.GetQueryableAsync();
		return await AsyncExecuter.AnyAsync(deliveryQueryable.Where(x =>
			x.TenantId == CurrentTenant.Id &&
			x.AcademicYearId == lead.AcademicYearId &&
			x.LeadId == lead.Id &&
			x.TemplateId == templateId));
	}
}