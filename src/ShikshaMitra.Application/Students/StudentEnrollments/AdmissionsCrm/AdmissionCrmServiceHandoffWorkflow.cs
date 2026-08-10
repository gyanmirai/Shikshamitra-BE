using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmServiceHandoffWorkflow(
	IRepository<AdmissionCrmServiceHandoff, Guid> repository,
	AdmissionCrmActivationAccess access,
	AdmissionCrmActivationTimeline timeline) : ApplicationService, ITransientDependency
{
	public async Task<IReadOnlyList<AdmissionCrmServiceHandoffDto>> GetAsync(Guid activationId)
	{
		await access.GetActivationAsync(activationId, "GetServiceHandoffsAsync");
		var query = await repository.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(query.Where(x =>
			x.TenantId == CurrentTenant.Id && x.ActivationId == activationId));
		return items.OrderBy(x => x.Type).Select(AdmissionCrmActivationMapper.ServiceHandoff).ToList();
	}

	public async Task<AdmissionCrmServiceHandoffDto> UpdateAsync(Guid id, UpdateAdmissionCrmServiceHandoffDto input)
	{
		var entity = await repository.GetAsync(id);
		var lead = await access.GetLeadAsync(entity.LeadId, "UpdateServiceHandoffAsync");
		switch (input.Status)
		{
			case AdmissionCrmServiceHandoffStatus.Dispatched: entity.MarkDispatched(Clock.Now, input.ExternalReference); break;
			case AdmissionCrmServiceHandoffStatus.Acknowledged:
				entity.Acknowledge(Clock.Now, input.ExternalReference ??
					throw new BusinessException("AdmissionCrm:ServiceHandoffReferenceRequired")); break;
			case AdmissionCrmServiceHandoffStatus.Failed:
				entity.Fail(input.Reason ?? throw new BusinessException("AdmissionCrm:ServiceHandoffFailureReasonRequired")); break;
			case AdmissionCrmServiceHandoffStatus.Cancelled:
				entity.Cancel(input.Reason ?? throw new BusinessException("AdmissionCrm:ServiceHandoffCancellationReasonRequired")); break;
			default: throw new BusinessException("AdmissionCrm:InvalidServiceHandoffTransition");
		}
		await repository.UpdateAsync(entity, autoSave: true);
		await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.ServiceHandoffChanged,
			$"{entity.Type} handoff {entity.Status}", input.Reason ?? input.ExternalReference ?? entity.Status.ToString(), entity.Id);
		return AdmissionCrmActivationMapper.ServiceHandoff(entity);
	}

	public async Task EnsureAsync(
		AdmissionCrmActivation activation,
		AdmissionInquiryLead lead,
		Guid studentId,
		List<AdmissionCrmActivationStepDto> steps)
	{
		var requests = AdmissionCrmActivationMapper.Deserialize<IReadOnlyList<AdmissionCrmServiceHandoffRequestDto>>(
			activation.ServiceHandoffRequestsJson) ?? [];
		if (requests.Count == 0)
		{
			AdmissionCrmActivationMapper.UpsertStep(steps, "service_handoffs",
				AdmissionCrmActivationStepStatus.NotRequired, "No optional service handoffs were requested.");
			return;
		}
		var query = await repository.GetQueryableAsync();
		var existing = await AsyncExecuter.ToListAsync(query.Where(x =>
			x.TenantId == CurrentTenant.Id && x.ActivationId == activation.Id));
		foreach (var request in requests.Where(request => existing.All(x => x.Type != request.Type)))
		{
			var entity = new AdmissionCrmServiceHandoff(GuidGenerator.Create(), CurrentTenant.Id,
				activation.AcademicYearId, lead.Id, activation.Id, studentId, request.Type,
				request.DestinationModule, request.ConsentConfirmed,
				AdmissionCrmActivationMapper.Serialize(request.Payload), Clock.Now);
			await repository.InsertAsync(entity, autoSave: true);
			await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.ServiceHandoffRequested,
				$"{request.Type} service handoff requested", request.DestinationModule, entity.Id);
		}
		AdmissionCrmActivationMapper.UpsertStep(steps, "service_handoffs",
			AdmissionCrmActivationStepStatus.Completed,
			$"{requests.Count} optional service handoff request(s) recorded; fulfillment remains with the owning module.");
	}
}
