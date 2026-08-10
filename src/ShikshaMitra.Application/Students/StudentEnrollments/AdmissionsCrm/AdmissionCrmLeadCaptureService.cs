using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
public class AdmissionCrmLeadCaptureService(
	AdmissionCrmQuickInquiryOptionsWorkflow quickInquiry,
	AdmissionCrmOperationalStandardOptionsWorkflow standardOptions,
	AdmissionCrmLeadCaptureWorkflow capture,
	AdmissionCrmDirectRegistrationWorkflow registration) : ApplicationService, IAdmissionCrmLeadCaptureService
{
	public Task<AdmissionCrmQuickInquiryOptionsDto> GetQuickInquiryOptionsAsync(Guid academicYearId) =>
		quickInquiry.GetQuickInquiryOptionsAsync(academicYearId);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Add)]
	public Task<IReadOnlyList<AdmissionCrmAdmissionYearStandardOptionDto>> GetOperationalStandardOptionsAsync(Guid academicYearId) =>
		standardOptions.GetOperationalStandardOptionsAsync(academicYearId);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Add)]
	public Task<AdmissionCrmLeadDto> CreateAsync(Guid academicYearId, CreateAdmissionCrmLeadDto input) =>
		capture.CreateAsync(academicYearId, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Add)]
	public Task<AdmissionCrmDirectRegistrationResultDto> CreateDirectRegistrationAsync(
		Guid academicYearId,
		CreateDirectAdmissionCrmRegistrationDto input) =>
		registration.CreateDirectRegistrationAsync(academicYearId, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.Add)]
	public Task<AdmissionCrmDirectRegistrationResultDto> CreatePaperRegistrationAsync(
		Guid academicYearId,
		CreatePaperAdmissionCrmRegistrationDto input) =>
		registration.CreatePaperRegistrationAsync(academicYearId, input);
}