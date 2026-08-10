using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRouteAppealWorkflow(
	IRepository<AdmissionCrmRouteAssignment, Guid> assignmentRepository,
	IRepository<AdmissionCrmRouteAppeal, Guid> appealRepository,
	AdmissionCrmLeadAccessReader leadAccessReader,
	AdmissionCrmRouteTimelineWriter timelineWriter) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmRouteAppealDto> RequestAsync(
		Guid assignmentId,
		RequestAdmissionCrmRouteAppealDto input,
		string operation)
	{
		var assignment = await assignmentRepository.GetAsync(assignmentId);
		var lead = await leadAccessReader.GetAsync(assignment.LeadId, operation);
		var appealQuery = await appealRepository.GetQueryableAsync();
		if (await AsyncExecuter.AnyAsync(appealQuery.Where(entity =>
			entity.TenantId == CurrentTenant.Id &&
			entity.RouteAssignmentId == assignmentId &&
			(entity.Status == AdmissionCrmRouteAppealStatus.Requested ||
			 entity.Status == AdmissionCrmRouteAppealStatus.UnderReview))))
			throw new BusinessException("AdmissionCrm:PendingRouteAppealAlreadyExists");
		var entity = new AdmissionCrmRouteAppeal(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			assignment.AcademicYearId,
			assignment.LeadId,
			assignment.Id,
			assignment.Status,
			input.RequestedStatus,
			input.Reason,
			AdmissionCrmRouteMapper.Serialize(input.EvidenceReferences),
			GetCurrentUserId(),
			Clock.Now);
		await appealRepository.InsertAsync(entity, autoSave: true);
		await timelineWriter.AddAsync(
			lead,
			AdmissionCrmTimelineEventType.AdmissionRouteAppealed,
			"Admission route appeal requested",
			input.Reason,
			entity.Id);
		return AdmissionCrmRouteMapper.Map(entity);
	}

	public async Task<AdmissionCrmRouteAppealDto> DecideAsync(
		Guid appealId,
		DecideAdmissionCrmRouteAppealDto input,
		string operation)
	{
		var entity = await appealRepository.GetAsync(appealId);
		var lead = await leadAccessReader.GetAsync(entity.LeadId, operation);
		entity.Decide(input.Approved, input.Reason, GetCurrentUserId(), Clock.Now);
		await appealRepository.UpdateAsync(entity, autoSave: true);
		await timelineWriter.AddAsync(
			lead,
			AdmissionCrmTimelineEventType.AdmissionRouteAppealDecided,
			"Admission route appeal decided",
			$"{entity.Status}: {input.Reason}",
			entity.Id);
		return AdmissionCrmRouteMapper.Map(entity);
	}

	public async Task<IReadOnlyList<AdmissionCrmRouteAppealDto>> GetListAsync(Guid leadId, string operation)
	{
		await leadAccessReader.GetAsync(leadId, operation);
		var queryable = await appealRepository.GetQueryableAsync();
		var entities = await AsyncExecuter.ToListAsync(queryable.Where(entity =>
			entity.TenantId == CurrentTenant.Id && entity.LeadId == leadId));
		return entities.OrderByDescending(entity => entity.RequestedAt)
			.Select(AdmissionCrmRouteMapper.Map)
			.ToList();
	}

	private Guid GetCurrentUserId() =>
		CurrentUser.Id ?? throw new AbpAuthorizationException("An authenticated user is required.");
}
