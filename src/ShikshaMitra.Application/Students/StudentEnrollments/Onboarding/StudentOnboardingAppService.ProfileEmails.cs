using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Portal;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

public partial class StudentOnboardingAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit)]
	[UnitOfWork(true)]
	public async Task<StudentCredentialsWorkspaceDto> UpdateCredentialsEmailAsync(Guid id, UpdateStudentCredentialsEmailDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit);
		EnsureCredentialsActor();
		var validation = await new UpdateStudentCredentialsEmailDtoValidator().ValidateAsync(input);
		if (!validation.IsValid) throw PortalInvalid("Enter a valid email and select Student, Father or Mother.");
		var item = await GetCaseAsync(id);
		EnsureConcurrency(item, input.ConcurrencyStamp);
		if (item.Status == StudentOnboardingStatus.Cancelled) throw new BusinessException(StudentOnboardingErrorCodes.CaseNotEditable);
		if (ShikshaMitra.Students.Records.StudentRecordAdmissionReturnService.ReturnTransitionId(item).HasValue)
			await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.Records.StudentRecordAdmissionReturnService>().ValidateOnboardingAsync(item, false);
		var owner = (await LazyServiceProvider.LazyGetRequiredService<StudentCredentialsRecipientReader>().GetAsync(item))
			.SingleOrDefault(x => x.AccountType == input.AccountType && x.Relationship == input.Relationship)
			?? throw PortalInvalid("Choose a saved Student, Father or Mother profile.");
		// Display provenance may deliberately hide a fallback Student email. Guard both
		// the reviewed display and the actual profile through the student's concurrency stamp.
		if (!StudentPortalProfileEmail.Same(owner.Email, input.ExpectedEmail)) throw PortalInvalid("The saved email changed. Refresh App Login before editing.");
		var link = await portalLinkRepository.FindAsync(x => x.StudentId == item.StudentId && x.AccountType == input.AccountType);
		if (link != null)
		{
			await AuthorizationService.CheckAsync(IdentityPermissions.Users.Update);
			if (link.Relationship != input.Relationship) throw PortalInvalid("A different account owner is already linked.");
			await LazyServiceProvider.LazyGetRequiredService<StudentPortalEmailChangeService>().RequestAsync(item, link, input);
		}
		else
		{
			if (input.CancelPendingChange) throw PortalInvalid("There is no linked login email change to cancel.");
			var student = await studentRepository.GetCredentialsProfileAsync(item.StudentId, trackChanges: true) ?? throw PortalInvalid("Student profile is unavailable.");
			if (input.EmailEditToken != StudentPortalEmailChangeService.ProfileToken(student)) throw PortalInvalid("The profile changed. Refresh App Login before editing.");
			// Saving a profile never grants access to an existing identity. The explicit
			// confirmed-family linking flow still governs that separate operation.
			if (input.AccountType == "guardian" && await SiblingParents.IsParentFlowAsync(item))
			{
				var parent = await SiblingParents.GetAsync(item);
				if (parent.SelectedRelationship == input.Relationship || parent.Candidates.Any(x => x.Relationship == input.Relationship))
					throw PortalInvalid("This parent account is already confirmed. Link it before requesting an email change.");
			}
			StudentPortalProfileEmail.Apply(student, input.Relationship, input.Email.Trim(), CurrentUser.Id!.Value);
			await studentRepository.UpdateAsync(student, false);
			await AddEventAsync(id, "CredentialProfileEmailSaved", $"Staff saved the {input.AccountType} profile email ({input.Relationship}). Credentials were not sent.");
		}
		await caseRepository.UpdateAsync(item, false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		return await MapCredentialsAsync(item);
	}
}
