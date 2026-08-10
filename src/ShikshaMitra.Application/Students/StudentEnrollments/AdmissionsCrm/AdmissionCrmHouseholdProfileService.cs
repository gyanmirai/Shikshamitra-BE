using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmHouseholdProfileService(
	AdmissionCrmHouseholdProfileWorkflow workflow) : ApplicationService, IAdmissionCrmHouseholdProfileService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageHouseholds)]
	public Task<AdmissionCrmApplicantIdentityProfileDto> CreateProfileAsync(
		Guid leadId,
		CreateAdmissionCrmApplicantIdentityProfileDto input) => workflow.CreateAsync(leadId, input, nameof(CreateProfileAsync));

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewHouseholds)]
	public Task<AdmissionCrmApplicantIdentityProfileDto?> GetProfileAsync(Guid leadId) =>
		workflow.GetAsync(leadId, nameof(GetProfileAsync));

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageGuardianAuthority)]
	public Task<AdmissionCrmApplicantIdentityProfileDto> AddGuardianAsync(
		Guid leadId,
		CreateAdmissionCrmGuardianRelationshipDto input) => workflow.AddGuardianAsync(leadId, input, nameof(AddGuardianAsync));
}
