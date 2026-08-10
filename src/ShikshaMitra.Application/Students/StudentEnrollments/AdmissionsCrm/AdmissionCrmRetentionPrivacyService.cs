using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmRetentionPrivacyService(AdmissionCrmRetentionPrivacyWorkflow workflow)
	: ApplicationService, IAdmissionCrmRetentionPrivacyService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRetention)]
	public Task<AdmissionCrmRetentionPolicyDto> CreateRetentionPolicyAsync(Guid academicYearId, CreateAdmissionCrmRetentionPolicyDto input) => workflow.CreateRetentionPolicyAsync(academicYearId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRetention)]
	public Task<IReadOnlyList<AdmissionCrmRetentionPolicyDto>> GetRetentionPoliciesAsync(Guid academicYearId) => workflow.GetRetentionPoliciesAsync(academicYearId);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRetention)]
	public Task<AdmissionCrmRetentionPolicyDto> SubmitRetentionPolicyAsync(Guid id) => workflow.SubmitRetentionPolicyAsync(id);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRetention)]
	public Task<AdmissionCrmRetentionPolicyDto> PublishRetentionPolicyAsync(Guid id) => workflow.PublishRetentionPolicyAsync(id);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRetention)]
	public Task<AdmissionCrmPrivacyPreviewDto> PreviewPrivacyRequestAsync(Guid leadId, AdmissionCrmPrivacyRequestType type) => workflow.PreviewPrivacyRequestAsync(leadId, type);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRetention)]
	public Task<AdmissionCrmPrivacyRequestDto> CreatePrivacyRequestAsync(Guid leadId, CreateAdmissionCrmPrivacyRequestDto input) => workflow.CreatePrivacyRequestAsync(leadId, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRetention)]
	public Task<AdmissionCrmPrivacyRequestDto> DecidePrivacyRequestAsync(Guid id, DecideAdmissionCrmPrivacyRequestDto input) => workflow.DecidePrivacyRequestAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRetention)]
	public Task<AdmissionCrmPrivacyRequestDto> CompletePrivacyRequestAsync(Guid id, CompleteAdmissionCrmPrivacyRequestDto input) => workflow.CompletePrivacyRequestAsync(id, input);
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageRetention)]
	public Task<IReadOnlyList<AdmissionCrmPrivacyRequestDto>> GetPrivacyRequestsAsync(Guid academicYearId) => workflow.GetPrivacyRequestsAsync(academicYearId);
}
