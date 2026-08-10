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

public class AdmissionCrmActivationLifecycleWorkflow(
	IRepository<AdmissionCrmActivation, Guid> activationRepository,
	AdmissionCrmActivationAccess access,
	AdmissionCrmActivationReadinessWorkflow readiness,
	AdmissionCrmActivationMapper mapper,
	AdmissionCrmActivationTimeline timeline) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmActivationPreviewDto> PreviewAsync(Guid leadId, PreviewAdmissionCrmActivationDto input)
	{
		var lead = await access.GetLeadAsync(leadId, "PreviewAsync");
		return await readiness.PreviewAsync(lead, input, "PreviewAsync");
	}

	public async Task<AdmissionCrmActivationDto> RequestAsync(Guid leadId, RequestAdmissionCrmActivationDto input)
	{
		var lead = await access.GetLeadAsync(leadId, "RequestAsync");
		var existing = await access.FindActivationAsync(leadId);
		if (existing != null) return await mapper.ActivationAsync(existing);
		var preview = await readiness.PreviewAsync(lead, input, "RequestAsync");
		if (!preview.IsReady)
			throw new BusinessException("AdmissionCrm:ActivationReadinessFailed")
				.WithData("BlockingItems", string.Join(",", preview.Items.Where(x =>
					x.Blocking && x.Status == AdmissionCrmActivationStepStatus.Failed).Select(x => x.Key)));
		var reference = $"ACT-{Clock.Now:yyyyMMdd}-{Guid.NewGuid():N}"[..32].ToUpperInvariant();
		var entity = new AdmissionCrmActivation(GuidGenerator.Create(), CurrentTenant.Id, lead.AcademicYearId,
			lead.Id, reference, input.EducationBoardId, input.AcademicMediumId, input.AcademicMediumStageId,
			input.StandardId, input.DivisionId, input.GrNumberSeriesId, input.RollNumber, input.Gender,
			AdmissionCrmActivationMapper.Serialize(preview), AdmissionCrmActivationMapper.Serialize(input.ServiceHandoffs),
			input.RequireStudentAccount, input.RequireGuardianAccount, GetUserId(), Clock.Now);
		await activationRepository.InsertAsync(entity, autoSave: true);
		await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.ActivationRequested,
			"Student activation requested", reference, entity.Id);
		return await mapper.ActivationAsync(entity);
	}

	public async Task<AdmissionCrmActivationDto> ApproveAsync(Guid id)
	{
		var entity = await access.GetActivationAsync(id, "ApproveAsync");
		var lead = await access.GetLeadAsync(entity.LeadId, "ApproveAsync");
		await readiness.EnsureCurrentAsync(entity, lead, "ApproveAsync");
		entity.Approve(GetUserId(), Clock.Now);
		await activationRepository.UpdateAsync(entity, autoSave: true);
		await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.ActivationApproved,
			"Student activation approved", entity.ActivationReference, entity.Id);
		return await mapper.ActivationAsync(entity);
	}

	public async Task<AdmissionCrmActivationDto?> GetByLeadAsync(Guid leadId)
	{
		await access.GetLeadAsync(leadId, "GetByLeadAsync");
		var entity = await access.FindActivationAsync(leadId);
		return entity == null ? null : await mapper.ActivationAsync(entity);
	}

	private Guid GetUserId() => CurrentUser.Id ?? throw new AbpAuthorizationException("An authenticated user is required.");
}
