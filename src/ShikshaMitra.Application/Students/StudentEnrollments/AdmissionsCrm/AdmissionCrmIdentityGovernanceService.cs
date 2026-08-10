using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmIdentityGovernanceService(
	AdmissionCrmIdentityGovernanceWorkflow workflow) : ApplicationService, IAdmissionCrmIdentityGovernanceService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageHouseholds)]
	public Task<AdmissionCrmIdentityConsentDto> RecordConsentAsync(
		Guid leadId,
		RecordAdmissionCrmIdentityConsentDto input) => workflow.RecordConsentAsync(leadId, input, nameof(RecordConsentAsync));

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageGuardianAuthority)]
	public Task<AdmissionCrmApplicantIdentityProfileDto> VerifyGuardianAuthorityAsync(
		Guid relationshipId,
		VerifyAdmissionCrmGuardianAuthorityDto input) =>
		workflow.VerifyGuardianAsync(relationshipId, input, nameof(VerifyGuardianAuthorityAsync));

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageProtectedRelationships)]
	public Task<AdmissionCrmApplicantIdentityProfileDto> SetGuardianRestrictionAsync(
		Guid relationshipId,
		SetAdmissionCrmGuardianRestrictionDto input) =>
		workflow.SetRestrictionAsync(relationshipId, input, nameof(SetGuardianRestrictionAsync));

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.OverrideIdentityReadiness)]
	public Task<AdmissionCrmApplicantIdentityProfileDto> OverrideReadinessAsync(
		Guid leadId,
		OverrideAdmissionCrmIdentityReadinessDto input) =>
		workflow.OverrideReadinessAsync(leadId, input, nameof(OverrideReadinessAsync));
}
