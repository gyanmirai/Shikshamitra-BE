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
		if (!validation.IsValid) throw PortalInvalid("Enter a valid email and select Student, Father, Mother or Guardian.");
		var item = await GetCaseAsync(id);
		EnsureConcurrency(item, input.ConcurrencyStamp);
		if (item.Status == StudentOnboardingStatus.Cancelled) throw new BusinessException(StudentOnboardingErrorCodes.CaseNotEditable);
		if (ShikshaMitra.Students.Records.StudentRecordAdmissionReturnService.ReturnTransitionId(item).HasValue)
			await LazyServiceProvider.LazyGetRequiredService<ShikshaMitra.Students.Records.StudentRecordAdmissionReturnService>().ValidateOnboardingAsync(item, false);
		var owner = (await LazyServiceProvider.LazyGetRequiredService<StudentCredentialsRecipientReader>().GetAsync(item))
			.SingleOrDefault(x => x.AccountType == input.AccountType && x.Relationship == input.Relationship)
			?? throw PortalInvalid("Choose a saved Student, Father, Mother or Guardian profile.");
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
			var email = input.Email.Trim();
			var otherOwners = await LazyServiceProvider.LazyGetRequiredService<StudentCredentialsRecipientReader>().GetAsync(item);
			if (otherOwners.Any(x => x.AccountType != input.AccountType && StudentPortalProfileEmail.Same(x.Email, email)))
				throw PortalInvalid("Student and adult app logins need different saved email addresses.");
			var duplicate = await portalUserManager.FindByEmailAsync(email) ?? await portalUserManager.FindByNameAsync(email);
			if (duplicate != null && (input.AccountType != "guardian" || !await SiblingParents.CanSaveExistingParentEmailAsync(item, input.Relationship, email)))
				throw PortalInvalid("This email is already used by a conflicting, inactive or restricted account. Existing accounts cannot be merged or repurposed through a profile email change.");
			// Saving a profile never grants access to an existing identity. The explicit
			// confirmed-family linking flow still governs that separate operation.
			if (input.AccountType == "guardian" && await SiblingParents.IsParentFlowAsync(item))
			{
				var parent = await SiblingParents.GetAsync(item);
				if (parent.SelectedRelationship == input.Relationship)
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
