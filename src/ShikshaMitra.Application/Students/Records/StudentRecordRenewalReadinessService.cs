using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp.Application.Services;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.Records;

public sealed class StudentRecordRenewalRequirements
{
	public bool Identity { get; set; } = true;
	public bool Family { get; set; } = true;
	public bool Communication { get; set; } = true;
	public bool Documents { get; set; } = true;
	public bool Medical { get; set; }
	public bool StudentAccount { get; set; }
	public bool GuardianAccount { get; set; }
	public bool ServiceConsent { get; set; }
}

/// <summary>Shares the Onboarding policy resolver and source evidence; renewal never invents an Admission case.</summary>
[RemoteService(false)]
public class StudentRecordRenewalReadinessService(OnboardingConfigurationManager configuration,
	IRepository<StudentOnboardingPolicy, Guid> legacyPolicies, IRepository<StudentOnboardingCase, Guid> initialCases,
	IRepository<Standard, Guid> standards, IStudentRepository students, StudentDocumentManager documents,
	AdmissionStudentEvidenceReader evidence, IRepository<StudentPortalAccountLink, Guid> links,
	IdentityUserManager users, OnboardingFeeReadinessReader finance,
	IRepository<StudentGuardianAccessReview, Guid> guardianReviews) : ApplicationService, ITransientDependency
{
	public async Task<string> CaptureRulesAsync(Guid studentId, Guid yearId, Guid standardId, Guid divisionId)
	{
		var requirement = new StudentRecordRenewalRequirements();
		var settings = await configuration.GetAsync(yearId);
		if (settings != null)
		{
			var standard = await standards.GetAsync(standardId, includeDetails: true);
			var stage = standard.EducationMediumStage; var medium = stage.EducationMedium;
			var previous = (await initialCases.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == studentId && x.Status == StudentOnboardingStatus.Completed))
				.OrderByDescending(x => x.CompletedAt).FirstOrDefault();
			var campusId = previous?.GetCampusId();
			Guid?[] path = [medium.EducationBoardId, medium.Id, stage.Id, standardId, divisionId];
			var candidates = settings.Profiles.Where(profile => OnboardingRuleConfiguration.Strings(profile, "admissionRoutes").Contains("Returning student", StringComparer.OrdinalIgnoreCase))
				.Where(profile =>
				{
					var applicability = profile.GetProperty("applicability");
					return (OnboardingRuleConfiguration.Text(applicability, "campusMode", "all") != "selected" || campusId.HasValue && OnboardingRuleConfiguration.Ids(applicability, "campusIds").Contains(campusId.Value)) &&
						OnboardingRuleConfiguration.Levels.Select((level, index) => OnboardingRuleConfiguration.Text(applicability, level.Mode) == "all" || path[index].HasValue && OnboardingRuleConfiguration.Ids(applicability, level.Ids).Contains(path[index]!.Value)).All(x => x);
				}).OrderByDescending(x => OnboardingRuleConfiguration.Score(x.GetProperty("applicability"))).ToList();
			if (candidates.Count > 1 && OnboardingRuleConfiguration.Score(candidates[0].GetProperty("applicability")) == OnboardingRuleConfiguration.Score(candidates[1].GetProperty("applicability")))
				throw OnboardingRuleConfiguration.Invalid("More than one returning-student onboarding rule matches this renewal.");
			var rules = candidates.Count > 0 ? candidates[0].GetProperty("rules") : settings.Root.GetProperty("readinessAccess");
			requirement.Documents = OnboardingRuleConfiguration.Boolean(rules, "requireDocuments", true);
			requirement.StudentAccount = OnboardingRuleConfiguration.Boolean(rules, "studentAccount");
			requirement.GuardianAccount = OnboardingRuleConfiguration.Boolean(rules, "guardianAccount");
			if (rules.TryGetProperty("checks", out var checks))
				foreach (var check in checks.EnumerateArray())
				{
					var required = OnboardingRuleConfiguration.Boolean(check, "required", true);
						switch (OnboardingRuleConfiguration.Text(check, "key"))
						{
							// These owner checks are mandatory structural gates in the coordinator:
							// an eligible permanent source, target placement/capacity, and unique preserved identifiers.
							case "admission": case "placement": case "identifier": case "placement_seat": case "identifiers_roll": case "final_readiness": break;
							case "accounts": case "finance_accounts_services":
								if (!required) { requirement.StudentAccount = false; requirement.GuardianAccount = false; } break;
						case "identity": requirement.Identity = required; break;
						case "family": requirement.Family = required; break;
						case "communication": requirement.Communication = required; break;
						case "documents": requirement.Documents &= required; break;
							case "medical-previous-school": case "medical_previous_school": case "medical": requirement.Medical = required; break;
						default: if (required) throw OnboardingRuleConfiguration.Invalid("The required renewal check has no owner integration: " + OnboardingRuleConfiguration.Text(check, "key")); break;
					}
				}
		}
		else
		{
			var legacy = (await legacyPolicies.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.AcademicYearId == yearId && x.IsActive && x.OfferingId == null && (x.StandardId == standardId || x.StandardId == null)))
				.OrderByDescending(x => x.StandardId.HasValue).FirstOrDefault();
			if (legacy != null)
			{
				requirement.Documents = legacy.RequireDocuments; requirement.Family = legacy.RequireFamilyDetails;
				requirement.Communication = legacy.RequireCommunicationDetails; requirement.Medical = legacy.RequireMedicalDetails;
				requirement.StudentAccount = legacy.RequireStudentAccount; requirement.GuardianAccount = legacy.RequireGuardianAccount; requirement.ServiceConsent = legacy.RequireServiceConsent;
			}
		}
		return JsonSerializer.Serialize(requirement);
	}
	public async Task<List<StudentRecordTransitionIssueDto>> GetIssuesAsync(StudentRecordRenewalCase item, bool provisioning)
	{
		var requirements = JsonSerializer.Deserialize<StudentRecordRenewalRequirements>(item.OnboardingRulesJson)!;
		var student = await students.GetWithDetailsAsync(item.StudentId);
		var issues = new List<StudentRecordTransitionIssueDto>();
		void Add(string code, string message) => issues.Add(new() { Code = "Students:Records:" + code, Message = message });
		if (student == null) { Add("EnrollmentMissing", "The permanent student profile is unavailable."); return issues; }
		if (requirements.GuardianAccount && await guardianReviews.AnyAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == item.StudentId && x.Status == "Pending"))
			Add(provisioning ? "RenewalProvisioningRequired" : "RenewalChecklistRequired", "Resolve the pending guardian access review before confirming required guardian portal access.");
		if (!provisioning)
		{
			if (requirements.Identity && (string.IsNullOrWhiteSpace(student.FirstName) || student.AdditionalDetail?.DateOfBirth.HasValue != true || student.AdditionalDetail.Gender.HasValue != true))
				Add("RenewalChecklistRequired", "Complete the permanent identity details before renewal activation.");
			if (requirements.Family && (student.FamilyDetail == null || student.FamilyDetail.FatherDetails == null && student.FamilyDetail.MotherDetails == null && student.FamilyDetail.GuardianDetails == null))
				Add("RenewalChecklistRequired", "Add at least one recorded parent or guardian.");
			if (requirements.Communication && (student.ContactDetail == null || string.IsNullOrWhiteSpace(student.ContactDetail.PrimaryContactNo) || string.IsNullOrWhiteSpace(student.ContactDetail.Address1) && student.CommunicationDetail?.LocalAddress == null))
				Add("RenewalChecklistRequired", "Complete the current contact and address details.");
			if ((requirements.Family || requirements.Communication || requirements.ServiceConsent) && !item.GuardianReviewConfirmed)
				Add("RenewalChecklistRequired", "Confirm the guardian/contact review and any required service consent with a staff audit reason.");
			if (requirements.Medical && student.MedicalRecord == null && student.PreviousSchoolDetail == null)
				Add("RenewalChecklistRequired", "Provide the required medical or previous-school information.");
			if (requirements.Documents)
			{
				var required = await documents.GetRequiredDocumentsStatusAsync(student.Id);
				var accepted = await evidence.GetAcceptedUploadDocumentTypeIdsAsync(student.Id);
				if (required.Where(x => x.documentType.IsRequired).Any(x => x.uploadedDocument?.ValidatedAt.HasValue != true && !accepted.Contains(x.documentType.Id)))
					Add("RenewalChecklistRequired", "Validate every required Student document or accepted Admission upload.");
			}
			if (!item.RollNumber.HasValue) Add("RenewalChecklistRequired", "Assign a next-year roll number before renewal activation.");
			if (string.IsNullOrWhiteSpace(student.GrNumber) && !student.GrNo.HasValue) Add("RenewalChecklistRequired", "The permanent student needs a verified GR number; renewals preserve it.");
		}
		else
		{
			var readiness = await finance.GetAsync(item.StudentId, item.AcademicYearId);
			if (!readiness.Assigned || !readiness.FirstInstallmentSettled) Add("RenewalProvisioningRequired", readiness.Detail);
		}
		// Portal provisioning follows enrollment activation, matching initial Onboarding.
		foreach (var accountType in new[] { "student", "guardian" }.Where(type => provisioning && (type == "student" ? requirements.StudentAccount : requirements.GuardianAccount)))
		{
			var accounts = await links.GetListAsync(x => x.TenantId == CurrentTenant.Id && x.StudentId == item.StudentId && x.AccountType == accountType);
			var eligible = false;
			foreach (var link in accounts)
			{
				var user = await users.FindByIdAsync(link.UserId.ToString());
				if (user == null || user.TenantId != CurrentTenant.Id || !user.IsActive || link.VerifiedByUserId == Guid.Empty) continue;
				var roleNames = await users.GetRolesAsync(user); var expectedRole = accountType == "student" ? "Student" : "Parent";
				var recordedEmail = accountType == "student" && link.Relationship == "self" ? student.ContactDetail?.Email : link.Relationship switch
				{
					"father" => student.FamilyDetail?.FatherDetails?.Email, "mother" => student.FamilyDetail?.MotherDetails?.Email,
					"guardian" => student.FamilyDetail?.GuardianDetails?.Email, _ => null
				};
				if (roleNames.Count == 1 && roleNames[0] == expectedRole && !string.IsNullOrWhiteSpace(recordedEmail) &&
					string.Equals(recordedEmail.Trim(), user.Email, StringComparison.OrdinalIgnoreCase)) { eligible = true; break; }
			}
			if (!eligible) Add("RenewalProvisioningRequired", $"Review or provision the required {accountType} portal link through its owning module.");
		}
		return issues;
	}
}
