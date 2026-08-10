using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmActivationCancellationWorkflow(
	IRepository<AdmissionCrmActivation, Guid> activationRepository,
	IStudentRepository studentRepository,
	IRepository<AdmissionCrmIdentifierAllocation, Guid> identifierRepository,
	AdmissionCrmActivationAccess access,
	AdmissionCrmActivationMapper mapper,
	AdmissionCrmActivationTimeline timeline,
	AdmissionCrmActivationCancellationPreviewWorkflow previewWorkflow,
	AdmissionCrmActivationCancellationSeatWorkflow seatWorkflow) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmActivationCancellationPreviewDto> PreviewAsync(Guid id)
	{
		var activation = await access.GetActivationAsync(id, "PreviewCancellationAsync");
		return await previewWorkflow.BuildAsync(activation);
	}

	public async Task<AdmissionCrmActivationDto> RequestAsync(Guid id, RequestAdmissionCrmActivationCancellationDto input)
	{
		var activation = await access.GetActivationAsync(id, "RequestCancellationAsync");
		var preview = await previewWorkflow.BuildAsync(activation);
		if (!preview.CanCancel)
			throw new BusinessException("AdmissionCrm:ActivationCancellationChecksFailed")
				.WithData("FailedChecks", string.Join(",", preview.Checks.Where(x => !x.Passed).Select(x => x.Key)));
		activation.RequestCancellation(input.Reason, GetUserId(), Clock.Now);
		await activationRepository.UpdateAsync(activation, autoSave: true);
		var lead = await access.GetLeadAsync(activation.LeadId, "RequestCancellationAsync");
		await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.ActivationCancellationRequested,
			"Activation cancellation requested", input.Reason, activation.Id);
		return await mapper.ActivationAsync(activation);
	}

	public async Task<AdmissionCrmActivationDto> ApproveAsync(Guid id)
	{
		var activation = await access.GetActivationAsync(id, "ApproveCancellationAsync");
		if (activation.Status != AdmissionCrmActivationStatus.CancellationPending)
			throw new BusinessException("AdmissionCrm:ActivationCancellationNotPending");
		if (activation.CancellationRequestedByUserId == GetUserId())
			throw new BusinessException("AdmissionCrm:DistinctActivationCancellationApproverRequired");
		var preview = await previewWorkflow.BuildAsync(activation);
		if (!preview.CanCancel)
			throw new BusinessException("AdmissionCrm:ActivationCancellationChecksFailed")
				.WithData("FailedChecks", string.Join(",", preview.Checks.Where(x => !x.Passed).Select(x => x.Key)));
		if (!activation.StudentId.HasValue) throw new BusinessException("AdmissionCrm:ActivationStudentMissing");
		var student = await studentRepository.GetInquiryForUpdateAsync(activation.StudentId.Value) ??
			throw new BusinessException("AdmissionCrm:ActivationStudentMissing");
		student.ChangeOperationalStatus(StudentStatus.Inactive, activation.CancellationReason);
		await studentRepository.UpdateAsync(student, autoSave: true);
		var identifierQuery = await identifierRepository.GetQueryableAsync();
		var identifiers = await AsyncExecuter.ToListAsync(identifierQuery.Where(x =>
			x.TenantId == CurrentTenant.Id && x.ActivationId == activation.Id));
		foreach (var identifier in identifiers.Where(x => x.Status is AdmissionCrmIdentifierStatus.Allocated or AdmissionCrmIdentifierStatus.Committed))
		{
			identifier.Void(activation.CancellationReason!);
			await identifierRepository.UpdateAsync(identifier);
		}
		var userId = GetUserId();
		await seatWorkflow.CancelAsync(activation, userId);
		activation.Cancel(userId, Clock.Now);
		await activationRepository.UpdateAsync(activation, autoSave: true);
		var lead = await access.GetLeadAsync(activation.LeadId, "ApproveCancellationAsync");
		await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.ActivationCancelled,
			"Student activation cancelled", activation.CancellationReason!, activation.Id);
		return await mapper.ActivationAsync(activation);
	}

	private Guid GetUserId() => CurrentUser.Id ?? throw new AbpAuthorizationException("An authenticated user is required.");
}
