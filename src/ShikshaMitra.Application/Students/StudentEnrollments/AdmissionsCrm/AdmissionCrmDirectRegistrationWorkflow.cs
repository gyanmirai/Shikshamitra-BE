using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmDirectRegistrationWorkflow(
	AdmissionCrmLeadCaptureWorkflow capture,
	AdmissionCrmLeadAccess access,
	IRepository<AdmissionInquiryLead, Guid> leadRepository,
	AdmissionCrmRegistrationCoordinator registrationCoordinator,
	AdmissionCrmLeadTimelineWriter timeline) : ApplicationService, ITransientDependency
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Add)]
	public async Task<AdmissionCrmDirectRegistrationResultDto> CreateDirectRegistrationAsync(
		Guid academicYearId,
		CreateDirectAdmissionCrmRegistrationDto input)
	{
		await AuthorizationService.CheckAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm);

		input.Applicant.Channel = AdmissionInquiryChannel.ManualBackOffice;
		var created = await capture.CreateAsync(academicYearId, input.Applicant);
		var lead = await access.GetLeadForCurrentTenantAsync(created.Id, nameof(CreateDirectRegistrationAsync));
		lead.ChangeStage(AdmissionInquiryStage.Eligible);
		await leadRepository.UpdateAsync(lead, autoSave: false);
		await timeline.AddTimelineEventAsync(
			lead,
			AdmissionCrmTimelineEventType.StageChanged,
			"Direct registration started",
			"The applicant entered Enrollment at registration through an authorized staff workflow.",
			lead.Id);

		var registration = await registrationCoordinator.IssueDirectAsync(lead.Id, input.Registration);
		await CurrentUnitOfWork!.SaveChangesAsync();

		return new AdmissionCrmDirectRegistrationResultDto
		{
			Applicant = AdmissionCrmLeadMapper.ToDto(lead, created.LikelyDuplicates),
			Registration = registration
		};
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Add)]
	public async Task<AdmissionCrmDirectRegistrationResultDto> CreatePaperRegistrationAsync(
		Guid academicYearId,
		CreatePaperAdmissionCrmRegistrationDto input)
	{
		await AuthorizationService.CheckAsync(
			ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.IssueRegistrationForm);

		input.Applicant.Channel = AdmissionInquiryChannel.ManualBackOffice;
		input.Applicant.AdmissionIntent = string.IsNullOrWhiteSpace(input.Applicant.AdmissionIntent)
			? "Paper registration"
			: input.Applicant.AdmissionIntent;
		var created = await capture.CreateAsync(academicYearId, input.Applicant);
		var lead = await access.GetLeadForCurrentTenantAsync(created.Id, nameof(CreatePaperRegistrationAsync));
		lead.ChangeStage(AdmissionInquiryStage.Eligible);
		await leadRepository.UpdateAsync(lead, autoSave: false);
		await timeline.AddTimelineEventAsync(
			lead,
			AdmissionCrmTimelineEventType.StageChanged,
			"Paper registration started",
			"The applicant entered Enrollment from a physical registration form entered by authorized staff.",
			lead.Id);

		var registration = await registrationCoordinator.IssueFromPaperAsync(lead.Id, input);
		await CurrentUnitOfWork!.SaveChangesAsync();

		return new AdmissionCrmDirectRegistrationResultDto
		{
			Applicant = AdmissionCrmLeadMapper.ToDto(lead, created.LikelyDuplicates),
			Registration = registration
		};
	}
}