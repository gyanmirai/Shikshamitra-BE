using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.Students.ValueObjects;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

/// <summary>Onboarding-owned account handoff for a real year-specific renewal; no initial case is fabricated.</summary>
[RemoteService(false)]
public class StudentRenewalPortalCoordinator(StudentRecordRenewalCoordinator renewals, StudentRecordsAccessService access,
	StudentRecordsPolicyResolver policies, IStudentRepository students, IRepository<StudentEnrollment, Guid> enrollments,
	IRepository<StudentPortalAccountLink, Guid> links, IRepository<StudentRecordTransitionEvent, Guid> events,
	IdentityUserManager users, IdentityRoleManager roles, StudentRecordTransitionAuthorityService authority) : ApplicationService, ITransientDependency
{
	public async Task<StudentRecordRenewalPortalOptionsDto> GetOptionsAsync(Guid id)
	{
		var item = await renewals.GetScopedAsync(id); var student = await students.GetWithDetailsAsync(item.StudentId) ?? throw Invalid("Student profile is unavailable.");
		var required = JsonSerializer.Deserialize<StudentRecordRenewalRequirements>(item.OnboardingRulesJson)!;
		await EnsureVisibleAsync(item, student, required.StudentAccount, required.GuardianAccount);
		var result = new StudentRecordRenewalPortalOptionsDto { StudentRequired = required.StudentAccount, GuardianRequired = required.GuardianAccount,
			StudentEmail = required.StudentAccount ? student.ContactDetail?.Email : null,
			CanProvision = item.Status == "Provisioning" && await authority.HasCurrentPermissionsAsync(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create,
				ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit, IdentityPermissions.Users.Create) };
		if (required.GuardianAccount)
			foreach (var relation in StudentPortalOwnerSelection.Relationships(student.FamilyDetail))
			{
				var guardian = GetGuardian(student, relation);
				if (guardian != null && !string.IsNullOrWhiteSpace(guardian.Email)) result.Guardians.Add(new() { Relationship = relation, Name = guardian.FullName, Email = guardian.Email });
			}
		foreach (var link in await links.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == student.Id &&
			(x.AccountType == "student" && required.StudentAccount || x.AccountType == "guardian" && required.GuardianAccount)))
		{
			var user = await users.FindByIdAsync(link.UserId.ToString());
			if (user != null && user.TenantId == CurrentTenant.Id) result.Accounts.Add(await MapAsync(student, item, link, user));
		}
		return result;
	}
	public async Task<StudentPortalAccountDto> ProvisionAsync(Guid id, ProvisionStudentPortalAccountDto input)
	{
		await authority.EnsureCurrentPermissionsAsync(ShikshaMitraSubmodulePermissions.Students.Records_Transitions.Create,
			ShikshaMitraSubmodulePermissions.Students.StudentOnboarding.Edit, IdentityPermissions.Users.Create);
		var validation = await new ProvisionStudentPortalAccountDtoValidator().ValidateAsync(input);
		if (!validation.IsValid) throw Invalid(validation.Errors[0].ErrorMessage);
		var item = await renewals.GetScopedAsync(id);
		if (item.Status != "Provisioning" || !item.StudentEnrollmentId.HasValue || !await enrollments.AnyAsync(x =>
			x.TenantId == CurrentTenant.Id && x.Id == item.StudentEnrollmentId && x.StudentId == item.StudentId && x.AcademicYearId == item.AcademicYearId && x.IsActive))
			throw new BusinessException("Students:Records:InvalidRenewalState");
		if (string.IsNullOrWhiteSpace(input.ConcurrencyStamp) || input.ConcurrencyStamp != item.ConcurrencyStamp) throw new BusinessException("Students:Records:ConcurrencyConflict");
		var required = JsonSerializer.Deserialize<StudentRecordRenewalRequirements>(item.OnboardingRulesJson)!;
		if (!(input.AccountType == "student" ? required.StudentAccount : required.GuardianAccount)) throw Invalid("This account is not required by the captured renewal rules.");
		var student = await students.GetWithDetailsAsync(item.StudentId) ?? throw Invalid("Student profile is unavailable.");
		await EnsureVisibleAsync(item, student, input.AccountType == "student", input.AccountType == "guardian");
		var guardian = GetGuardian(student, input.Relationship); var email = input.Email.Trim();
		var recorded = input.AccountType == "student" ? student.ContactDetail?.Email : guardian?.Email;
		if (string.IsNullOrWhiteSpace(recorded) || !string.Equals(email, recorded.Trim(), StringComparison.OrdinalIgnoreCase)) throw Invalid("The email must match the selected person's recorded profile.");
		var role = input.AccountType == "student" ? "Student" : "Parent";
		var existing = await links.FindAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == student.Id && x.AccountType == input.AccountType);
		if (existing != null)
		{
			var linked = await users.FindByIdAsync(existing.UserId.ToString());
			if (linked == null || linked.TenantId != CurrentTenant.Id || !linked.IsActive || existing.Relationship != input.Relationship ||
				!string.Equals(linked.Email, email, StringComparison.OrdinalIgnoreCase) || (await users.GetRolesAsync(linked)).Count != 1 || !(await users.GetRolesAsync(linked)).Contains(role))
				throw Invalid("The existing portal link requires a reviewed identity repair.");
			return await MapAsync(student, item, existing, linked);
		}
		if (input.AccountType == "guardian" && !StudentPortalOwnerSelection.Relationships(student.FamilyDetail).Contains(input.Relationship))
			throw Invalid("Choose a saved Father or Mother profile. Other guardian is available only when neither parent is recorded.");
		if (!await roles.RoleExistsAsync(role)) throw Invalid("The portal role must be configured by an identity administrator.");
		var user = await users.FindByEmailAsync(email);
		var existingIdentity = user != null;
		if (user != null)
		{
			await authority.EnsureCurrentPermissionsAsync(IdentityPermissions.Users.Update);
			var names = await users.GetRolesAsync(user);
			if (!input.LinkExistingGuardian || input.AccountType != "guardian" || user.TenantId != CurrentTenant.Id || !user.IsActive || !user.EmailConfirmed || names.Count != 1 || names[0] != "Parent" ||
				!await links.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.UserId == user.Id && x.AccountType == "guardian"))
				throw Invalid("Only an explicitly confirmed verified guardian portal identity may be linked for a sibling.");
		}
		else
		{
			user = new IdentityUser(GuidGenerator.Create(), email, email, CurrentTenant.Id) { Name = input.AccountType == "student" ? student.FirstName : guardian?.FullName,
				Surname = input.AccountType == "student" ? student.LastName : null };
			if (!(await users.CreateAsync(user)).Succeeded || !(await users.AddToRoleAsync(user, role)).Succeeded) throw Invalid("The identity policy rejected account creation or portal role assignment.");
		}
		// The account owner completes the existing protected password-setup flow.
		// This handoff neither generates a password nor confirms an unverified mailbox.
		if (existingIdentity && !(await users.UpdateAsync(user)).Succeeded)
			throw Invalid("The existing parent account changed. Refresh before linking.");
		var link = new StudentPortalAccountLink(GuidGenerator.Create(), CurrentTenant.Id!.Value, student.Id, user.Id, input.AccountType, input.Relationship, CurrentUser.Id!.Value);
		await links.InsertAsync(link, autoSave: true);
		if (item.TransitionId.HasValue) await events.InsertAsync(new(GuidGenerator.Create(), CurrentTenant.Id.Value, item.TransitionId.Value, CurrentUser.Id.Value,
			"RenewalPortalLinked", 0, $"Verified {input.AccountType} portal handoff ({input.Relationship}); owner password setup remains separate."));
		return await MapAsync(student, item, link, user);
	}
	private async Task EnsureVisibleAsync(StudentRecordRenewalCase item, Student student, bool studentAccount, bool guardianAccount)
	{
		var names = await access.GetDatabaseRoleNamesAsync();
		var snapshots = new[] { StudentRecordsPolicy.FromJson(item.PolicyJson), (await policies.ResolveAsync(student.AcademicYearId)).Policy, (await policies.ResolveAsync(item.AcademicYearId)).Policy };
		if (snapshots.Any(policy => studentAccount && policy.IsMasked("Contact", names) || guardianAccount && policy.IsMasked("Guardian", names)))
			throw new AbpAuthorizationException();
	}
	private async Task<StudentPortalAccountDto> MapAsync(Student student, StudentRecordRenewalCase item, StudentPortalAccountLink link, IdentityUser user)
	{
		var names = await access.GetDatabaseRoleNamesAsync();
		var identityHidden = StudentRecordsPolicy.FromJson(item.PolicyJson).IsMasked("Identity", names) || (await policies.ResolveAsync(student.AcademicYearId)).Policy.IsMasked("Identity", names) || (await policies.ResolveAsync(item.AcademicYearId)).Policy.IsMasked("Identity", names);
		return new() { StudentId = student.Id, StudentName = identityHidden ? "" : string.Join(" ", new[] { student.FirstName, student.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))),
			UserId = user.Id, AccountType = link.AccountType, Relationship = link.Relationship, Email = user.Email, IsActive = user.IsActive, EmailConfirmed = user.EmailConfirmed };
	}
	private static ParentGuardianDetails? GetGuardian(Student student, string relationship) => relationship switch { "father" => student.FamilyDetail?.FatherDetails,
		"mother" => student.FamilyDetail?.MotherDetails, "guardian" => student.FamilyDetail?.GuardianDetails, _ => null };
	private static BusinessException Invalid(string reason) => new BusinessException(StudentOnboardingErrorCodes.PortalAccountInvalid).WithData("Reason", reason);
}
