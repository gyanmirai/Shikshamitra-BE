using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.Enrollments;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class AdmissionCrmHouseholdIdentityApi_Tests : ShikshaMitraApiTestBase
{
	public AdmissionCrmHouseholdIdentityApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Should_Reuse_Household_And_Guardian_Across_Siblings_With_Governed_Readiness()
	{
		var firstLead = await CreateLeadAsync("First sibling");
		var profileResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{firstLead.Id}/household-identity",
			new CreateAdmissionCrmApplicantIdentityProfileDto
			{
				HouseholdDisplayName = "Patel household",
				PreferredLanguage = "gu",
				Addresses =
				[
					new AdmissionCrmAddressDto
					{
						Type = "Correspondence", Line1 = "1 School Road", City = "Ahmedabad",
						State = "Gujarat", PostalCode = "380001", Country = "India"
					}
				],
				Applicant = new AdmissionCrmPersonInputDto
				{
					FirstName = firstLead.FirstName!, Phone = firstLead.PrimaryContactNo,
					Provenance = AdmissionCrmPersonProvenance.StaffSupplied
				}
			}, JsonOptions);
		profileResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await profileResponse.Content.ReadAsStringAsync());
		var profile = await profileResponse.Content.ReadFromJsonAsync<AdmissionCrmApplicantIdentityProfileDto>(JsonOptions);
		profile.ShouldNotBeNull();

		var missingGuardianResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{firstLead.Id}/household-identity/guardians",
			new CreateAdmissionCrmGuardianRelationshipDto
			{
				RelationshipType = AdmissionCrmGuardianRelationshipType.Mother,
				IsPrimaryContact = true
			}, JsonOptions);
		missingGuardianResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await missingGuardianResponse.Content.ReadAsStringAsync()).ShouldContain("Guardian");

		var guardianInput = new CreateAdmissionCrmGuardianRelationshipDto
		{
			Guardian = new AdmissionCrmPersonInputDto
			{
				FirstName = "Meera", LastName = "Patel", Phone = "9876500011",
				Email = "meera.patel@example.test",
				Provenance = AdmissionCrmPersonProvenance.StaffSupplied
			},
			RelationshipType = AdmissionCrmGuardianRelationshipType.Mother,
			IsPrimaryContact = true,
			IsEmergencyContact = true,
			IsAccountableContact = true,
			HasFeeResponsibility = true,
			HasPickupAuthorization = true,
			HasPortalAccess = true,
			CommunicationPriority = 1
		};
		var guardianResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{firstLead.Id}/household-identity/guardians",
			guardianInput, JsonOptions);
		guardianResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await guardianResponse.Content.ReadAsStringAsync());
		profile = await guardianResponse.Content.ReadFromJsonAsync<AdmissionCrmApplicantIdentityProfileDto>(JsonOptions);
		profile.ShouldNotBeNull();
		var guardian = profile.Guardians.ShouldHaveSingleItem();

		var duplicateGuardianResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{firstLead.Id}/household-identity/guardians",
			guardianInput, JsonOptions);
		duplicateGuardianResponse.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await duplicateGuardianResponse.Content.ReadAsStringAsync()).ShouldContain("AdmissionCrm:PossiblePersonDuplicate");

		var verifyResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/household-identity/guardians/{guardian.Id}/verify-authority",
			new VerifyAdmissionCrmGuardianAuthorityDto { EvidenceReference = "BIRTH-CERT-100" }, JsonOptions);
		verifyResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await verifyResponse.Content.ReadAsStringAsync());

		var restrictionResponse = await Client.PutAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/household-identity/guardians/{guardian.Id}/protected-restriction",
			new SetAdmissionCrmGuardianRestrictionDto { Restriction = "Do not disclose the protected address." }, JsonOptions);
		restrictionResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await restrictionResponse.Content.ReadAsStringAsync());
		profile = await restrictionResponse.Content.ReadFromJsonAsync<AdmissionCrmApplicantIdentityProfileDto>(JsonOptions);
		profile!.Guardians[0].ProtectedRestriction.ShouldBe("Do not disclose the protected address.");

		var consentResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{firstLead.Id}/household-identity/consents",
			new RecordAdmissionCrmIdentityConsentDto
			{
				PersonId = profile.ApplicantPersonId,
				Purpose = "Identity processing",
				TextVersion = "identity-v1",
				Language = "gu",
				Actor = "Meera Patel",
				Channel = "Reception",
				IsGranted = true
			}, JsonOptions);
		consentResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await consentResponse.Content.ReadAsStringAsync());

		var linkResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{firstLead.Id}/household-identity/persons/{guardian.GuardianPersonId}/link-account",
			new LinkAdmissionCrmPersonAccountDto { UserId = Guid.Parse(TestAuthHandler.TestUserId) }, JsonOptions);
		linkResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await linkResponse.Content.ReadAsStringAsync());
		profile = await linkResponse.Content.ReadFromJsonAsync<AdmissionCrmApplicantIdentityProfileDto>(JsonOptions);
		profile.ShouldNotBeNull();
		profile.Status.ShouldBe(AdmissionCrmIdentityResolutionStatus.Ready);
		profile.AccountableContactReady.ShouldBeTrue();
		profile.IdentityConsentReady.ShouldBeTrue();
		profile.AccountLinkReady.ShouldBeTrue();

		var secondLead = await CreateLeadAsync("Second sibling");
		var siblingProfileResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{secondLead.Id}/household-identity",
			new CreateAdmissionCrmApplicantIdentityProfileDto
			{
				ExistingHouseholdId = profile.HouseholdId,
				HouseholdDisplayName = "Patel household",
				Applicant = new AdmissionCrmPersonInputDto
				{
					FirstName = secondLead.FirstName!, Phone = secondLead.PrimaryContactNo,
					Provenance = AdmissionCrmPersonProvenance.StaffSupplied
				}
			}, JsonOptions);
		siblingProfileResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await siblingProfileResponse.Content.ReadAsStringAsync());
		var siblingProfile = await siblingProfileResponse.Content.ReadFromJsonAsync<AdmissionCrmApplicantIdentityProfileDto>(JsonOptions);
		siblingProfile.ShouldNotBeNull();
		siblingProfile.HouseholdId.ShouldBe(profile.HouseholdId);

		var reusedGuardianResponse = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/leads/{secondLead.Id}/household-identity/guardians",
			new CreateAdmissionCrmGuardianRelationshipDto
			{
				ExistingGuardianPersonId = guardian.GuardianPersonId,
				RelationshipType = AdmissionCrmGuardianRelationshipType.Mother,
				IsPrimaryContact = true,
				IsAccountableContact = true,
				HasPortalAccess = true,
				CommunicationPriority = 1
			}, JsonOptions);
		reusedGuardianResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await reusedGuardianResponse.Content.ReadAsStringAsync());
		var reused = await reusedGuardianResponse.Content.ReadFromJsonAsync<AdmissionCrmApplicantIdentityProfileDto>(JsonOptions);
		reused.ShouldNotBeNull();
		reused.Guardians.ShouldHaveSingleItem().GuardianPersonId.ShouldBe(guardian.GuardianPersonId);
	}

	private async Task<AdmissionCrmLeadDto> CreateLeadAsync(string intent)
	{
		var response = await Client.PostAsJsonAsync(
			$"/api/student-enrollment/admissions-crm/{Factory.SeededAcademicYearId}/leads",
			new CreateAdmissionCrmLeadDto
			{
				AdmissionIntent = intent,
				DesiredClassName = "Grade 1",
				PrimaryContactNo = "9" + Random.Shared.NextInt64(100000000, 999999999),
				Channel = AdmissionInquiryChannel.QuickCallEntry,
				FirstName = "Applicant" + Guid.NewGuid().ToString("N")[..6]
			}, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<AdmissionCrmLeadDto>(JsonOptions))!;
	}
}
