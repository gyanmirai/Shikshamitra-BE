using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.ValueObjects;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Automatic_onboarding_default_guardian_access_completes_after_activation_and_preserves_retried_identity() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateAutomaticRegressionFixtureAsync("AutomaticComplete");
			fixture.Application.Status.ShouldBe(AdmissionApplicationStatus.Approved);
			(await _applicationAppService.GetWorkflowAsync(fixture.Application.Id)).Status.ShouldBe(AdmissionWorkflowInstanceStatus.Completed);
			var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var settings = (await service.GetSettingsAsync(cycle.AcademicYearId)).Single(s => s.AcademicYearId == cycle.AcademicYearId);
			var config = JsonNode.Parse(settings.ConfigurationJson)!.AsObject();
			config["onboardingProfiles"]!.AsArray().ShouldBeEmpty();
			var access = config["readinessAccess"]!.AsObject();
			access.Remove("studentAccount");
			access.Remove("guardianAccount");
			access.Remove("sendStudentCredentialEmail");
			access.Remove("sendGuardianCredentialEmail");
			await service.SaveSettingsAsync(new() { AcademicYearId = cycle.AcademicYearId,
				ConfigurationJson = config.ToJsonString(), ConcurrencyStamp = settings.ConcurrencyStamp });
			var item = await PrepareAutomaticRegressionCaseAsync(fixture, createNew: true,
				"automatic-complete", confirmFeesBeforePlacement: true);
			item.AdmissionConfirmed.ShouldBeTrue();
			item.FeeStructureConfirmed.ShouldBeTrue();
			item.StudentAccountRequired.ShouldBeFalse();
			item.GuardianAccountRequired.ShouldBeTrue();
			var email = $"automatic-guardian-{Guid.NewGuid():N}@example.test";
			await WithUnitOfWorkAsync(async () =>
			{
				var roles = GetRequiredService<IdentityRoleManager>();
				if (!await roles.RoleExistsAsync("Parent"))
					(await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), "Parent", TestTenantId))).Succeeded.ShouldBeTrue();
				var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>();
				var family = await families.GetAsync(item.StudentId);
				family.UpdateFatherDetails(new ParentGuardianDetails("Verified Test Father", null, null, email,
					"9000000002", null, null, null, null, null, null, null, null));
				await families.UpdateAsync(family, true);
			});
			var activation = new ActivateStudentOnboardingDto
			{
				ConcurrencyStamp = item.ConcurrencyStamp, IdempotencyKey = "automatic-complete-activation"
			};
			item = await service.ActivateAsync(item.Id, activation);
			item.Status.ShouldBe(StudentOnboardingStatus.NeedsAttention);
			item.StudentEnrollmentId.ShouldNotBeNull();
			item.CredentialsPending.ShouldBeTrue();
			item.GuardianAccountRequired.ShouldBeTrue();
			item.StudentAccountRequired.ShouldBeFalse();
			(await service.GetPortalAccountsAsync(item.Id)).ShouldBeEmpty();
			var enrollment = await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(item.StudentEnrollmentId!.Value));
			enrollment.IsActive.ShouldBeTrue();
			enrollment.StudentId.ShouldBe(item.StudentId);
			enrollment.OnboardingCaseId.ShouldBe(item.Id);
			enrollment.DivisionId.ShouldBe(item.DivisionId);
			enrollment.RollNumber.ShouldBe(item.RollNumber);
			var records = GetRequiredService<IStudentRecordProfileAppService>();
			var activatedRecord = await records.GetAsync(item.StudentId, cycle.AcademicYearId);
			activatedRecord.EntrySource.ShouldBe("ActivatedOnboarding");
			activatedRecord.Status.ShouldBe(StudentLifeCycleStatusEnum.Enrolled);
			activatedRecord.GrNumber.ShouldBe(item.GrNumber);
			activatedRecord.EnrollmentHistory.ShouldContain(e => e.Id == enrollment.Id && e.OnboardingCaseId == item.Id);
			var accountInput = new ProvisionStudentPortalAccountDto
			{
				AccountType = "guardian", Relationship = "father", Email = email,
				RelationshipVerified = true, ConcurrencyStamp = item.ConcurrencyStamp
			};
			var portal = await service.ProvisionPortalAccountAsync(item.Id, accountInput);
			var completed = await service.GetAsync(item.Id);
			completed.Status.ShouldBe(StudentOnboardingStatus.Completed);
			completed.CredentialsPending.ShouldBeFalse();
			completed.StudentAccountRequired.ShouldBeFalse();
			completed.GuardianAccountStatus.ShouldBe(StudentOnboardingServiceHandoffStatus.Acknowledged);
			(await records.GetAsync(item.StudentId, cycle.AcademicYearId)).EntrySource.ShouldBe("CompletedOnboarding");
			var users = GetRequiredService<IdentityUserManager>();
			var user = await WithUnitOfWorkAsync(() => users.GetByIdAsync(portal.UserId));
			user.PasswordHash.ShouldBeNull();
			user.EmailConfirmed.ShouldBeFalse();
			(await WithUnitOfWorkAsync(() => users.GetRolesAsync(user))).ShouldBe(["Parent"]);
			// Exercise the existing token-validated owner setup before retrying staff commands.
			var token = await WithUnitOfWorkAsync(() => users.GeneratePasswordResetTokenAsync(user));
			await GetRequiredService<IAccountAppService>().ResetPasswordAsync(new ResetPasswordDto
			{
				UserId = user.Id, ResetToken = token, Password = "Automatic-Owner-Test-29!"
			});
			var password = (await WithUnitOfWorkAsync(() => users.GetByIdAsync(user.Id))).PasswordHash;
			password.ShouldNotBeNullOrWhiteSpace();
			var links = GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>();
			var link = await WithUnitOfWorkAsync(() => links.GetAsync(x => x.StudentId == item.StudentId && x.AccountType == "guardian"));
			(await service.ProvisionPortalAccountAsync(item.Id, accountInput)).UserId.ShouldBe(portal.UserId);
			var repeated = await service.ActivateAsync(item.Id, activation);
			repeated.StudentEnrollmentId.ShouldBe(enrollment.Id);
			repeated.Status.ShouldBe(StudentOnboardingStatus.Completed);
			(await WithUnitOfWorkAsync(() => links.CountAsync(x => x.StudentId == item.StudentId))).ShouldBe(1);
			(await WithUnitOfWorkAsync(() => links.GetAsync(x => x.StudentId == item.StudentId && x.AccountType == "guardian"))).Id.ShouldBe(link.Id);
			(await WithUnitOfWorkAsync(() => users.GetByIdAsync(user.Id))).PasswordHash.ShouldBe(password);
			(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(e => e.StudentId == item.StudentId))).ShouldBe(1);
			await WithUnitOfWorkAsync(async () =>
			{
				var stored = await GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().GetAsync(item.Id);
				using var rules = JsonDocument.Parse(stored.ExtraProperties["ReadinessRulesSnapshot"]!.ToString()!);
				OnboardingRuleConfiguration.Boolean(rules.RootElement, "studentAccount").ShouldBeFalse();
				OnboardingRuleConfiguration.Boolean(rules.RootElement, "sendStudentCredentialEmail").ShouldBeFalse();
				OnboardingRuleConfiguration.Boolean(rules.RootElement, "sendGuardianCredentialEmail").ShouldBeFalse();
				(await _applicationRepository.GetAsync(fixture.Application.Id)).ExtraProperties.ContainsKey("HasFinanceFeePayment").ShouldBeFalse();
				(await GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().CountAsync(e => e.CaseId == item.Id)).ShouldBe(0);
			});
		});

	[Fact]
	public Task Automatic_onboarding_repeated_activation_preview_preserves_step_evidence_and_case_stamp() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateAutomaticRegressionFixtureAsync("AutomaticPreview");
			var item = await PrepareAutomaticRegressionCaseAsync(fixture, createNew: true, "automatic-preview");
			var service = GetRequiredService<IStudentOnboardingAppService>();
			await WithUnitOfWorkAsync(async () =>
			{
				var documents = await _onboardingStepRepository.GetAsync(item.Steps.Single(s => s.Key == StudentOnboardingStepKeys.Documents).Id);
				documents.Complete(TestUserId, new DateTime(2025, 1, 2, 3, 4, 5, DateTimeKind.Utc), "Accepted admission document evidence.");
				await _onboardingStepRepository.UpdateAsync(documents, true);
			});
			item = await service.GetAsync(item.Id);
			var before = await ReadAutomaticRegressionStepsAsync(item.Id);
			var first = await service.PreviewActivationAsync(item.Id);
			var second = await service.PreviewActivationAsync(item.Id);
			first.LastError.ShouldBeNullOrEmpty();
			second.LastError.ShouldBeNullOrEmpty();
			first.ConcurrencyStamp.ShouldBe(item.ConcurrencyStamp);
			second.ConcurrencyStamp.ShouldBe(item.ConcurrencyStamp);
			(await ReadAutomaticRegressionStepsAsync(item.Id)).ShouldBe(before);
			(await service.GetAsync(item.Id)).ConcurrencyStamp.ShouldBe(item.ConcurrencyStamp);
		});

	[Theory]
	[InlineData("first-name")]
	[InlineData("last-name")]
	[InlineData("date-of-birth")]
	[InlineData("gender")]
	public Task Automatic_onboarding_blocks_activation_when_reused_student_core_identity_is_incomplete(string missing) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateAutomaticRegressionFixtureAsync("AutomaticIdentity", linkExistingStudent: true);
			fixture.Applicant.FirstName.ShouldNotBeNullOrWhiteSpace();
			fixture.Applicant.LastName.ShouldNotBeNullOrWhiteSpace();
			fixture.Applicant.DateOfBirth.ShouldNotBeNull();
			fixture.Applicant.Gender.ShouldNotBeNullOrWhiteSpace();
			await WithUnitOfWorkAsync(async () =>
			{
				var student = await _studentRepository.GetAsync(fixture.ExistingStudentId!.Value);
				student.UpdateGeneralInformation(missing == "first-name" ? "" : student.FirstName,
					student.MiddleName, missing == "last-name" ? "" : student.LastName);
				var details = GetRequiredService<IRepository<StudentAdditionalDetail, Guid>>();
				var detail = await details.GetAsync(student.Id);
				detail.UpdateDetails(missing == "date-of-birth" ? null : detail.DateOfBirth,
					missing == "gender" ? null : detail.Gender, detail.DateOfInquiry,
					detail.InquiryPreferenceId, detail.PreviousSchool);
				await details.UpdateAsync(detail, false);
				await _studentRepository.UpdateAsync(student, true);
			});
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var finalized = await FinalizeAsync(fixture.Application, false, $"automatic-identity-{missing}");
			var immediate = await service.GetAsync(finalized.OnboardingCaseId);
			immediate.StudentId.ShouldBe(fixture.ExistingStudentId!.Value);
			immediate.Steps.Single(s => s.Key == StudentOnboardingStepKeys.Identity).Required.ShouldBeTrue();
			immediate.Steps.Single(s => s.Key == StudentOnboardingStepKeys.Identity).Status.ShouldBe(StudentOnboardingStepStatus.Blocked);
			var item = await PrepareAutomaticRegressionCaseAsync(fixture, createNew: false,
				$"automatic-identity-{missing}", existingCaseId: finalized.OnboardingCaseId);
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			var identity = item.Steps.Single(s => s.Key == StudentOnboardingStepKeys.Identity);
			identity.Required.ShouldBeTrue();
			identity.Status.ShouldBe(StudentOnboardingStepStatus.Blocked);
			(await Should.ThrowAsync<BusinessException>(() => service.ActivateAsync(item.Id,
				new() { ConcurrencyStamp = item.ConcurrencyStamp, IdempotencyKey = $"automatic-incomplete-{missing}" })))
				.Code.ShouldBe(StudentOnboardingErrorCodes.RequiredStepsIncomplete);
			(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(e => e.StudentId == item.StudentId)))
				.ShouldBe(0);
		});

	[Fact]
	public Task Automatic_onboarding_reads_snapshotless_history_without_resolving_dormant_profile_conflicts() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateAutomaticRegressionFixtureAsync("AutomaticHistoric");
			var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
			var finalized = await FinalizeAsync(fixture.Application, true, "automatic-historic-read");
			var cases = GetRequiredService<IRepository<StudentOnboardingCase, Guid>>();
			var before = await WithUnitOfWorkAsync(async () =>
			{
				var historical = await cases.GetAsync(finalized.OnboardingCaseId);
				var enrollment = StudentEnrollment.Create(Guid.NewGuid(), TestTenantId, finalized.StudentId,
					cycle.AcademicYearId, fixture.Offering.StandardId, null, null, historical.Id);
				await _studentEnrollmentRepository.InsertAsync(enrollment, false);
				historical.CompleteHistoricalImport(enrollment.Id, null, null, DateTime.UtcNow);
				historical.ExtraProperties.Remove("ReadinessRulesSnapshot");
				historical.ExtraProperties.Remove("DocumentPoliciesSnapshot");
				historical.ExtraProperties.Remove(AutomaticOnboardingReadinessRules.SnapshotVersionKey);
				await cases.UpdateAsync(historical, true);
				return (Properties: JsonSerializer.Serialize(historical.ExtraProperties), Stamp: historical.ConcurrencyStamp);
			});
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var settings = (await service.GetSettingsAsync(cycle.AcademicYearId)).Single(s => s.AcademicYearId == cycle.AcademicYearId);
			var config = JsonNode.Parse(settings.ConfigurationJson)!.AsObject();
			var profiles = new JsonArray();
			foreach (var id in new[] { "historic-dormant-a", "historic-dormant-b" })
				profiles.Add(new JsonObject
				{
					["id"] = id,
					["offeringIds"] = new JsonArray(fixture.Offering.Id.ToString(), Guid.NewGuid().ToString()),
					["applicability"] = new JsonObject { ["boardMode"] = "selected", ["boardIds"] = new JsonArray(Guid.NewGuid().ToString()) },
					["rules"] = new JsonObject { ["studentAccount"] = true, ["guardianAccount"] = true }
				});
			config["onboardingProfiles"] = profiles;
			config["readinessAccess"]!["studentAccount"] = false;
			config["readinessAccess"]!["guardianAccount"] = false;
			await service.SaveSettingsAsync(new() { AcademicYearId = cycle.AcademicYearId,
				ConfigurationJson = config.ToJsonString(), ConcurrencyStamp = settings.ConcurrencyStamp });
			var result = await service.GetAsync(finalized.OnboardingCaseId);
			result.Status.ShouldBe(StudentOnboardingStatus.Completed);
			result.CredentialsPending.ShouldBeFalse();
			result.ConcurrencyStamp.ShouldBe(before.Stamp);
			result.HasDocumentRequirementsSnapshot.ShouldBeFalse();
			var list = await service.GetListAsync(new() { Search = "AutomaticHistoric", MaxResultCount = 20 });
			list.Items.ShouldContain(c => c.Id == finalized.OnboardingCaseId);
			await WithUnitOfWorkAsync(async () =>
			{
				var historical = await cases.GetAsync(finalized.OnboardingCaseId);
				JsonSerializer.Serialize(historical.ExtraProperties).ShouldBe(before.Properties);
				historical.ConcurrencyStamp.ShouldBe(before.Stamp);
			});
		});

	private async Task<AdmissionCertificationFixture> CreateAutomaticRegressionFixtureAsync(string name, bool linkExistingStudent = false)
	{
		var fixture = await CreateCertificationFixtureAsync(name, linkExistingStudent: linkExistingStudent, currentWorkflow: true);
		var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
		await ConfigureOfferingReadinessAsync(fixture.Offering, cycle.AcademicYearId);
		await SaveAutomaticAccessAsync(cycle.AcademicYearId, student: false, guardian: false);
		return fixture;
	}

	private async Task<StudentOnboardingCaseDto> PrepareAutomaticRegressionCaseAsync(AdmissionCertificationFixture fixture, bool createNew,
		string key, Guid? existingCaseId = null, bool confirmFeesBeforePlacement = false)
	{
		var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
		var structure = await CreateFeeSelectionReferenceAsync(fixture.Offering, cycle.AcademicYearId);
		var caseId = existingCaseId ?? (await FinalizeAsync(fixture.Application, createNew, key)).OnboardingCaseId;
		var service = GetRequiredService<IStudentOnboardingAppService>();
		var division = await WithUnitOfWorkAsync(async () =>
		{
			var value = await GetRequiredService<DivisionManager>().CreateAsync(fixture.Offering.StandardId, $"Automatic-{Guid.NewGuid():N}"[..24], 40);
			return await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(value, true);
		});
		var item = await service.GetAsync(caseId);
		if (confirmFeesBeforePlacement)
		{
			await service.SaveFeeStructureAsync(item.Id, new() { StructureId = structure.Id, ConcurrencyStamp = item.ConcurrencyStamp });
			item = await service.GetAsync(item.Id);
		}
		item = await service.SetPlacementAsync(item.Id, new() { DivisionId = division.Id, ConcurrencyStamp = item.ConcurrencyStamp });
		var series = (await service.GetAvailableIdentifierSeriesAsync(item.Id)).Single();
		item = await service.AllocateIdentifierAsync(item.Id, new() { NumberSeriesId = series.Id, RollNumber = 1, ConcurrencyStamp = item.ConcurrencyStamp });
		if (!confirmFeesBeforePlacement)
			await service.SaveFeeStructureAsync(item.Id, new() { StructureId = structure.Id, ConcurrencyStamp = item.ConcurrencyStamp });
		return await service.GetAsync(item.Id);
	}

	private Task<(string Key, StudentOnboardingStepStatus Status, bool Required, DateTime? CompletedAt, Guid? CompletedByUserId, string? Detail)[]>
		ReadAutomaticRegressionStepsAsync(Guid id) => WithUnitOfWorkAsync(async () =>
			(await _onboardingStepRepository.GetListAsync(s => s.OnboardingCaseId == id)).OrderBy(s => s.Key)
				.Select(s => (s.Key, s.Status, s.Required, s.CompletedAt, s.CompletedByUserId, s.Detail)).ToArray());
}
