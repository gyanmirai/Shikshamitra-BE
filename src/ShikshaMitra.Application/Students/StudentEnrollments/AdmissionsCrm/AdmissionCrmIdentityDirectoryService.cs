using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

[Authorize]
[RemoteService(false)]
public class AdmissionCrmIdentityDirectoryService(
	AdmissionCrmIdentityDirectoryReader directoryReader,
	AdmissionCrmGovernmentIdentityWorkflow governmentIdentityWorkflow,
	AdmissionCrmIdentityAccountWorkflow accountWorkflow) : ApplicationService, IAdmissionCrmIdentityDirectoryService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewHouseholds)]
	public Task<PagedResultDto<AdmissionCrmPersonDto>> SearchPersonsAsync(SearchAdmissionCrmPersonsInput input) =>
		directoryReader.SearchPersonsAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ViewHouseholds)]
	public Task<PagedResultDto<AdmissionCrmHouseholdSummaryDto>> SearchHouseholdsAsync(
		SearchAdmissionCrmHouseholdsInput input) => directoryReader.SearchHouseholdsAsync(input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.ManageGovernmentReadiness)]
	public Task<AdmissionCrmPersonDto> UpdatePersonIdentityAsync(
		Guid personId,
		UpdateAdmissionCrmPersonIdentityDto input) => governmentIdentityWorkflow.UpdateAsync(personId, input);

	[Authorize(ShikshaMitraSubmodulePermissions.Students.Enrollment_AdmissionsCrm.LinkAccounts)]
	public Task<AdmissionCrmApplicantIdentityProfileDto> LinkPersonAccountAsync(
		Guid leadId,
		Guid personId,
		LinkAdmissionCrmPersonAccountDto input) =>
		accountWorkflow.LinkAsync(leadId, personId, input, nameof(LinkPersonAccountAsync));
}
