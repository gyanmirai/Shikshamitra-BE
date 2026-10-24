using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace ShikshaMitra.Students.StudentEnrollments.Onboarding;

[ExposeServices(typeof(IStudentCredentialEmailEligibilityResolver), typeof(StudentOnboardingCredentialEligibilityResolver))]
public class StudentOnboardingCredentialEligibilityResolver(
	IRepository<StudentOnboardingCase, Guid> cases,
	IRepository<StudentEnrollment, Guid> enrollments,
	IRepository<StudentPortalAccountLink, Guid> links,
	IRepository<AdmissionOffering, Guid> offerings,
	IRepository<AdmissionApplication, Guid> applications,
	OnboardingRequirementSnapshotService snapshots,
	IStudentRepository students,
	IStudentOnboardingAppService onboarding,
	OnboardingConfigurationManager configuration,
	StudentDocumentManager documents,
	AdmissionStudentEvidenceReader evidence,
	IdentityUserManager users,
	ICurrentTenant tenant,
	ITenantRepository tenants,
	IConfiguration appConfiguration,
	StudentCredentialsRecipientReader recipients) : IStudentCredentialEmailEligibilityResolver, ITransientDependency
{
	public const string AccountRequired = "Create and verify the selected portal account first.";
	public async Task<StudentCredentialEmailEligibility> ResolveAsync(Guid caseId, string accountType, bool enforceStaffScope = true)
		=> await ResolveCoreAsync(caseId, accountType, enforceStaffScope, prepareAccount: false);
	public async Task<StudentCredentialEmailEligibility> ResolveExplicitAsync(Guid caseId, string accountType, bool enforceStaffScope = true)
		=> await ResolveCoreAsync(caseId, accountType, enforceStaffScope, prepareAccount: false, allowManualEmail: true);

	public async Task<StudentCredentialEmailEligibility> ResolveAccountPreparationAsync(Guid caseId, string accountType)
		=> await ResolveCoreAsync(caseId, accountType, enforceStaffScope: true, prepareAccount: true);

	/// <summary>Checks required access/readiness for explicit manual delivery without changing automatic-email settings.</summary>
	public async Task<StudentCredentialEmailEligibility> ResolveSendPreparationAsync(Guid caseId, string accountType)
		=> await ResolveCoreAsync(caseId, accountType, enforceStaffScope: true, prepareAccount: false, allowMissingAccount: true, allowManualEmail: true);

	private async Task<StudentCredentialEmailEligibility> ResolveCoreAsync(Guid caseId, string accountType,
		bool enforceStaffScope, bool prepareAccount, bool allowMissingAccount = false, bool allowManualEmail = false)
	{
		if (enforceStaffScope) await onboarding.EnsureCaseAccessAsync(caseId);
		var item = await cases.FindAsync(caseId);
		if (item == null || !tenant.Id.HasValue || item.TenantId != tenant.Id)
			throw new EntityNotFoundException(typeof(StudentOnboardingCase), caseId);
		if (!item.HasCampusSnapshot) item.UseLegacyCampusId((await offerings.FindAsync(item.OfferingId))?.CampusId);
		var student = await students.GetCredentialsProfileAsync(item.StudentId);
		var settings = await configuration.GetAsync(item.AcademicYearId);
		var rules = settings?.Readiness(item);
        if (item.ExtraProperties.TryGetValue("ReadinessRulesSnapshot", out var snapshot) && snapshot is string text)
        {
            using var saved = System.Text.Json.JsonDocument.Parse(text); rules = saved.RootElement.Clone();
        }
		var requiredAccount = rules.HasValue && StudentPortalAccountRules.IsRequired(rules.Value, accountType);
		var enabled = rules.HasValue && StudentPortalAccountRules.IsEnabled(rules.Value, accountType) &&
			(prepareAccount || allowManualEmail || OnboardingRuleConfiguration.Boolean(rules.Value,
			accountType == "student" ? "sendStudentCredentialEmail" : "sendGuardianCredentialEmail"));
		var link = await links.FindAsync(x => x.StudentId == item.StudentId && x.AccountType == accountType);
		var user = link == null ? null : await users.FindByIdAsync(link.UserId.ToString());
		var name = student == null ? "Student" : string.Join(" ", new[] { student.FirstName, student.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)));
		var school = await tenants.FindAsync(tenant.Id.Value);
		var loginUrl = (appConfiguration["App:ClientUrl"] ?? appConfiguration["App:AngularUrl"] ?? "").TrimEnd('/') + "/login";
		StudentCredentialEmailEligibility Result(string? reason, bool waitingForCompletion = false) => new(item.Id, item.StudentId, link?.Id, user?.Id,
			user?.Email ?? "", name, enabled, reason == null, reason, loginUrl, school?.Name ?? "ShikshaMitra", waitingForCompletion, link?.Relationship);
		if (accountType == "student")
		{
			// Share capture/approved-correction proof across manual, automatic and queued
			// delivery. A guardian contact fallback is never a Student email recipient.
			var owner = (await recipients.GetAsync(item)).FirstOrDefault(value => value.AccountType == "student");
			if (owner?.BlockedReason != null || owner?.Email == null)
				return Result(owner?.BlockedReason ?? "Add or review the Student email in Student Records.") with { Email = string.Empty };
		}
		if (!enabled) return Result(allowManualEmail
			? $"{(accountType == "student" ? "Student" : "Parent")} app access is not enabled for this case. Review its saved portal-access requirements."
			: prepareAccount ? "This portal account is not required by the school." : "Credential email is disabled in the school's portal access settings.");
		if (student == null || student.Status != StudentLifeCycleStatusEnum.Enrolled || item.Status == StudentOnboardingStatus.Cancelled)
			return Result("The student must be actively enrolled in the school.");
		var enrollment = item.StudentEnrollmentId.HasValue ? await enrollments.FindAsync(item.StudentEnrollmentId.Value) : null;
		if (enrollment?.IsActive != true || enrollment.StudentId != student.Id || enrollment.AcademicYearId != item.AcademicYearId)
			return Result("Activate the student's enrollment before sending credentials.");
		if ((string.IsNullOrWhiteSpace(student.GrNumber) && !student.GrNo.HasValue) || enrollment.RollNumber is not > 0 || !enrollment.DivisionId.HasValue ||
			enrollment.DivisionId != student.DivisionId || enrollment.RollNumber != student.RollNo ||
			item.Status != StudentOnboardingStatus.Completed && enrollment.DivisionId != item.DivisionId)
			return Result("Complete and commit the GR number, roll number and division before sending credentials.");
		if (item.ExtraProperties.ContainsKey("DocumentPoliciesSnapshot"))
		{
			if (!await snapshots.DocumentsReadyAsync(item, await applications.GetAsync(item.ApplicationId)))
				return Result("Verify every applicable Offering document before sending credentials.");
		}
		else
		{
			var required = await documents.GetRequiredDocumentsStatusAsync(student.Id);
			var accepted = await evidence.GetAcceptedUploadDocumentTypeIdsAsync(student.Id);
			if (required.Where(value => value.documentType.IsRequired).Any(value => value.uploadedDocument?.ValidatedAt.HasValue != true && !accepted.Contains(value.documentType.Id)))
				return Result("Validate every required document before sending credentials.");
		}
		if (link == null && allowMissingAccount && item.Status != StudentOnboardingStatus.Cancelled &&
			(item.Status != StudentOnboardingStatus.Completed || !requiredAccount))
			return Result(null);
		if (link == null || user == null || user.TenantId != tenant.Id || !user.IsActive || link.VerifiedByUserId == Guid.Empty)
			return Result(AccountRequired);
		var recordedEmail = accountType == "student" && link.Relationship == "self" ? student.ContactDetail?.Email : link.Relationship switch
		{
			"father" => student.FamilyDetail?.FatherDetails?.Email,
			"mother" => student.FamilyDetail?.MotherDetails?.Email,
			"guardian" => student.FamilyDetail?.GuardianDetails?.Email,
			_ => null
		};
		if (string.IsNullOrWhiteSpace(recordedEmail) || !string.Equals(recordedEmail.Trim(), user.Email, StringComparison.OrdinalIgnoreCase))
			return Result("A matching email address is mandatory. Review the saved account-owner profile and link.");
		var roles = await users.GetRolesAsync(user);
		if (roles.Count != 1 || roles[0] != (accountType == "student" ? "Student" : "Parent"))
			return Result("The linked identity must have only the expected portal role.");
		if (!prepareAccount && OnboardingRuleConfiguration.Text(rules!.Value, "credentialSendingTime", "activation") == "completion" &&
			item.Status != StudentOnboardingStatus.Completed)
			return Result("The school sends credentials after full onboarding completion, including requested app access.", waitingForCompletion: true);
		return Result(null);
	}
}
