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

public class AdmissionCrmRouteAssignmentWorkflow(
	IRepository<AdmissionCrmRouteAssignment, Guid> assignmentRepository,
	AdmissionCrmRouteDefinitionWorkflow definitionWorkflow,
	AdmissionCrmRouteDataValidator routeDataValidator,
	AdmissionCrmLeadAccessReader leadAccessReader,
	AdmissionCrmRouteTimelineWriter timelineWriter) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmRouteAssignmentDto> AssignAsync(
		Guid leadId,
		AssignAdmissionCrmSpecialRouteDto input,
		string operation)
	{
		var lead = await leadAccessReader.GetAsync(leadId, operation);
		var definition = await definitionWorkflow.GetAsync(input.RouteDefinitionId);
		var queryable = await assignmentRepository.GetQueryableAsync();
		var current = await AsyncExecuter.FirstOrDefaultAsync(queryable.Where(entity =>
			entity.TenantId == CurrentTenant.Id && entity.LeadId == lead.Id && entity.IsPrimary));
		EnsureCanAssign(definition, lead);
		await routeDataValidator.ValidateAsync(definition.RouteType, input.RouteData, lead);
		AdmissionCrmRouteEvidenceValidator.EnsureValid(definition, input.EvidenceReferences);
		if (current != null && string.IsNullOrWhiteSpace(input.Reason))
			throw new BusinessException("AdmissionCrm:RouteChangeReasonRequired");
		current?.Supersede();
		if (current != null)
			await assignmentRepository.UpdateAsync(current);
		var entity = new AdmissionCrmRouteAssignment(
			GuidGenerator.Create(),
			CurrentTenant.Id,
			lead.AcademicYearId,
			lead.Id,
			definition.Id,
			definition.Version,
			definition.RouteType,
			AdmissionCrmRouteMapper.Serialize(input.RouteData),
			AdmissionCrmRouteMapper.Serialize(input.EvidenceReferences),
			current?.Id,
			input.Reason,
			GetCurrentUserId(),
			Clock.Now);
		await assignmentRepository.InsertAsync(entity, autoSave: true);
		await timelineWriter.AddAsync(
			lead,
			current == null ? AdmissionCrmTimelineEventType.AdmissionRouteAssigned : AdmissionCrmTimelineEventType.AdmissionRouteChanged,
			current == null ? "Admission route assigned" : "Admission route changed",
			$"{definition.Name}: {input.Reason}",
			entity.Id);
		return AdmissionCrmRouteMapper.Map(entity);
	}

	public async Task<IReadOnlyList<AdmissionCrmRouteAssignmentDto>> GetHistoryAsync(Guid leadId, string operation)
	{
		await leadAccessReader.GetAsync(leadId, operation);
		var queryable = await assignmentRepository.GetQueryableAsync();
		var entities = await AsyncExecuter.ToListAsync(queryable.Where(entity =>
			entity.TenantId == CurrentTenant.Id && entity.LeadId == leadId));
		return entities.OrderByDescending(entity => entity.AssignedAt)
			.Select(AdmissionCrmRouteMapper.Map)
			.ToList();
	}

	public async Task<AdmissionCrmRouteAssignmentDto> EvaluateAsync(
		Guid id,
		EvaluateAdmissionCrmSpecialRouteDto input,
		string operation)
	{
		var entity = await assignmentRepository.GetAsync(id);
		var lead = await leadAccessReader.GetAsync(entity.LeadId, operation);
		var definition = await definitionWorkflow.GetAsync(entity.RouteDefinitionId);
		if (definition.AcademicYearId != entity.AcademicYearId ||
			definition.Version != entity.RouteDefinitionVersion ||
			definition.RouteType != entity.RouteType)
		{
			throw new BusinessException("AdmissionCrm:RouteDefinitionSnapshotMismatch")
				.WithData("RouteAssignmentId", entity.Id)
				.WithData("RouteDefinitionId", definition.Id);
		}
		AdmissionCrmRouteEvidenceValidator.EnsureValid(
			definition,
			AdmissionCrmRouteMapper.Deserialize<IReadOnlyList<string>>(entity.EvidenceJson) ?? []);
		if (entity.AssignedByUserId == GetCurrentUserId() &&
			input.Status is AdmissionCrmRouteEligibilityStatus.Eligible or AdmissionCrmRouteEligibilityStatus.ExceptionApproved)
			throw new BusinessException("AdmissionCrm:DistinctRouteEligibilityApproverRequired");
		entity.Evaluate(input.Status, input.Reason, GetCurrentUserId(), Clock.Now);
		await assignmentRepository.UpdateAsync(entity, autoSave: true);
		await timelineWriter.AddAsync(
			lead,
			AdmissionCrmTimelineEventType.AdmissionRouteEvaluated,
			"Admission route evaluated",
			$"{input.Status}: {input.Reason}",
			entity.Id);
		return AdmissionCrmRouteMapper.Map(entity);
	}

	public void EnsureCanAssign(
		AdmissionCrmRouteDefinition definition,
		AdmissionInquiryLead lead)
	{
		var capturedDefinitionCanComplete = lead.IntendedRouteDefinitionId == definition.Id &&
			lead.IntendedRouteDefinitionVersion == definition.Version &&
			definition.Status is AdmissionCrmRouteDefinitionStatus.Published or AdmissionCrmRouteDefinitionStatus.Retired;
		if (definition.AcademicYearId != lead.AcademicYearId ||
			(!definition.IsEffectiveAt(Clock.Now) && !capturedDefinitionCanComplete))
		{
			throw new BusinessException("AdmissionCrm:PublishedRouteDefinitionRequired")
				.WithData("RouteDefinitionId", definition.Id)
				.WithData("AcademicYearId", lead.AcademicYearId);
		}
	}

	private Guid GetCurrentUserId() =>
		CurrentUser.Id ?? throw new AbpAuthorizationException("An authenticated user is required.");
}
