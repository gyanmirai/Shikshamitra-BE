using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmActivationExecutionWorkflow(
	IRepository<AdmissionCrmActivation, Guid> activationRepository,
	AdmissionCrmActivationAccess access,
	AdmissionCrmActivationReadinessWorkflow readiness,
	AdmissionCrmActivationMapper mapper,
	AdmissionCrmActivationTimeline timeline,
	AdmissionCrmActivationStudentWorkflow studentWorkflow,
	AdmissionCrmServiceHandoffWorkflow handoffWorkflow,
	AdmissionCrmActivationSeatWorkflow seatWorkflow) : ApplicationService, ITransientDependency
{
	public async Task<AdmissionCrmActivationDto> ExecuteAsync(Guid id)
	{
		var activation = await access.GetActivationAsync(id, "ExecuteAsync");
		await AuthorizationService.CheckAsync(activation.Status == AdmissionCrmActivationStatus.RepairRequired
			? ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.RepairActivation
			: ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ExecuteActivation);
		if (activation.Status == AdmissionCrmActivationStatus.FirstDayReady)
			return await mapper.ActivationAsync(activation);
		var lead = await access.GetLeadAsync(activation.LeadId, "ExecuteAsync");
		await readiness.EnsureCurrentAsync(activation, lead, "ExecuteAsync");
		activation.StartAttempt();
		var steps = AdmissionCrmActivationMapper.Deserialize<List<AdmissionCrmActivationStepDto>>(activation.StepsJson) ?? [];
		try
		{
			var student = await studentWorkflow.ActivateAsync(activation, lead, steps);
			if (student.Failure != null)
			{
				AdmissionCrmActivationMapper.UpsertStep(steps, "accounts_and_enrollment",
					AdmissionCrmActivationStepStatus.Failed, student.Failure);
				activation.UpdateSteps(AdmissionCrmActivationMapper.Serialize(steps));
				activation.RequireRepair("accounts_and_enrollment", student.Failure);
				await activationRepository.UpdateAsync(activation, autoSave: true);
				await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.ActivationRepairRequired,
					"Activation repair required", student.Failure, activation.Id);
				return await mapper.ActivationAsync(activation);
			}
			AdmissionCrmActivationMapper.UpsertStep(steps, "academic_placement",
				AdmissionCrmActivationStepStatus.Completed, "Academic placement committed.");
			AdmissionCrmActivationMapper.UpsertStep(steps, "accounts_and_enrollment",
				AdmissionCrmActivationStepStatus.Completed, "Enrollment and required access completed.");
			await handoffWorkflow.EnsureAsync(activation, lead, student.StudentId, steps);
			await seatWorkflow.MarkActivatedAsync(lead.Id);
			activation.UpdateSteps(AdmissionCrmActivationMapper.Serialize(steps));
			activation.Complete(Clock.Now);
			await activationRepository.UpdateAsync(activation, autoSave: true);
			await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.StudentActivated,
				"Student is first-day ready", activation.ActivationReference, activation.Id);
			return await mapper.ActivationAsync(activation);
		}
		catch (Exception exception) when (exception is not AbpAuthorizationException)
		{
			var message = exception is BusinessException business ? business.Code ?? business.Message : "Unexpected activation failure.";
			AdmissionCrmActivationMapper.UpsertStep(steps, "execution", AdmissionCrmActivationStepStatus.Failed, message);
			activation.UpdateSteps(AdmissionCrmActivationMapper.Serialize(steps));
			activation.RequireRepair("execution", message);
			await activationRepository.UpdateAsync(activation, autoSave: true);
			await timeline.AddAsync(lead, AdmissionCrmTimelineEventType.ActivationRepairRequired,
				"Activation repair required", message, activation.Id);
			return await mapper.ActivationAsync(activation);
		}
	}
}
