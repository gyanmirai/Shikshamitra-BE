using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.ValueObjects;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Account;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Validation;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Onboarding_document_review_stays_required_without_duplicating_admission_approval() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("OptionalDocs", currentWorkflow: true);
			var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
			await CreateHandoffDocumentTypeAsync();
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var settings = new
			{
				readinessAccess = new { requireDocuments = true },
				onboardingProfiles = new[]
				{
					new
					{
						id = "fresh-optional-documents",
						admissionRoutes = new[] { "Fresh admission" },
						applicability = new { boardMode = "all", mediumMode = "all", stageMode = "all",
							classMode = "all", divisionMode = "all" },
						rules = new { requireDocuments = false, approvalMode = "conditional",
							approvalTriggers = new[] { "incomplete-optional-check" } }
					}
				},
				grNumber = new { configurations = Array.Empty<object>() },
				rollNumber = new { configurations = Array.Empty<object>() },
				divisionChange = new { configurations = Array.Empty<object>() }
			};
			var savedSettings = await service.SaveSettingsAsync(new()
			{
				AcademicYearId = cycle.AcademicYearId,
				ConfigurationJson = JsonSerializer.Serialize(settings)
			});
			var finalized = await FinalizeAsync(fixture.Application, true, "optional-document-approval");
			var item = await service.GetAsync(finalized.OnboardingCaseId);
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			// Optional document rules do not remove the document-review milestone.
			item.Steps.Single(step => step.Key == StudentOnboardingStepKeys.Documents).Required.ShouldBeTrue();
			item.Steps.ShouldNotContain(step => step.Key == StudentOnboardingStepKeys.ApprovalReview && step.Required);

			var requiredDocumentsSettings = JsonSerializer.Serialize(settings).Replace(
				"\"requireDocuments\":false", "\"requireDocuments\":true", StringComparison.Ordinal);
			await service.SaveSettingsAsync(new()
			{
				AcademicYearId = cycle.AcademicYearId,
				ConfigurationJson = requiredDocumentsSettings,
				ConcurrencyStamp = savedSettings.ConcurrencyStamp
			});
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			item.Steps.Single(step => step.Key == StudentOnboardingStepKeys.Documents).Required.ShouldBeTrue();
			item.Steps.ShouldNotContain(step => step.Key == StudentOnboardingStepKeys.ApprovalReview && step.Required);
		});

	[Fact]
	public Task Onboarding_zero_obligation_does_not_count_as_first_payment() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("ZeroFee");
			var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
			var finalized = await FinalizeAsync(fixture.Application, true, "zero-fee-readiness-test");
			var assignment = new StudentFeeStructure(Guid.NewGuid(), TestTenantId, finalized.StudentId,
				Guid.NewGuid(), cycle.AcademicYearId);
			assignment.CaptureAssignment("zero-fee-readiness-test", JsonSerializer.Serialize(new
			{
				StudentId = finalized.StudentId, AcademicYearId = cycle.AcademicYearId,
				ProratedTotal = 0m, Lines = Array.Empty<object>()
			}));
			await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentFeeStructure, Guid>>()
				.InsertAsync(assignment, true));
			var readiness = await WithUnitOfWorkAsync(() => GetRequiredService<OnboardingFeeReadinessReader>()
				.GetAsync(finalized.StudentId, cycle.AcademicYearId));
			readiness.Assigned.ShouldBeTrue();
			readiness.FirstInstallmentSettled.ShouldBeFalse();
		});

	[Fact]
	public Task Onboarding_zero_first_installment_without_payment_stays_incomplete() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("ZeroFirst");
			var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
			var finalized = await FinalizeAsync(fixture.Application, true, "zero-first-readiness-test");
			var assignment = new StudentFeeStructure(Guid.NewGuid(), TestTenantId, finalized.StudentId,
				Guid.NewGuid(), cycle.AcademicYearId);
			var first = new DueFee(Guid.NewGuid(), TestTenantId, finalized.StudentId, Guid.NewGuid(),
				(MonthEnum)5, 2026, false, 0m, new DateTime(2026, 6, 10));
			first.BindAssignment(assignment.Id);
			assignment.CaptureAssignment("zero-first-readiness-test", JsonSerializer.Serialize(new
			{
				StudentId = finalized.StudentId, AcademicYearId = cycle.AcademicYearId,
				ProratedTotal = 0m, Lines = new[] { new { DueFeeId = first.Id } }
			}));
			await WithUnitOfWorkAsync(async () =>
			{
				await GetRequiredService<IRepository<StudentFeeStructure, Guid>>().InsertAsync(assignment, true);
				await GetRequiredService<IRepository<DueFee, Guid>>().InsertAsync(first, true);
			});
			var readiness = await WithUnitOfWorkAsync(() => GetRequiredService<OnboardingFeeReadinessReader>()
				.GetAsync(finalized.StudentId, cycle.AcademicYearId));
			readiness.Assigned.ShouldBeTrue();
			readiness.FirstInstallmentSettled.ShouldBeFalse();
		});

	[Fact]
	public Task Onboarding_portal_password_setup_does_not_confirm_an_unlinked_identity() =>
		RunCertificationScenarioAsync(async () =>
		{
			var users = GetRequiredService<IdentityUserManager>();
			var user = new IdentityUser(Guid.NewGuid(), $"unlinked-{Guid.NewGuid():N}", "unlinked@example.test", TestTenantId);
			await WithUnitOfWorkAsync(async () => (await users.CreateAsync(user)).Succeeded.ShouldBeTrue());
			var token = await WithUnitOfWorkAsync(() => users.GeneratePasswordResetTokenAsync(user));
			await GetRequiredService<IAccountAppService>().ResetPasswordAsync(new ResetPasswordDto
			{
				UserId = user.Id, ResetToken = token, Password = "Unlinked-Test-Only-29!"
			});
			(await WithUnitOfWorkAsync(() => users.GetByIdAsync(user.Id))).EmailConfirmed.ShouldBeFalse();
		});

	[Theory]
	[InlineData("student", "self", "Student")]
	[InlineData("guardian", "father", "Parent")]
	public Task Onboarding_portal_accounts_are_persisted_idempotent_scoped_and_passwordless(string type, string relationship, string role) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("JayPortal", currentWorkflow: true);
			var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
			var service = GetRequiredService<IStudentOnboardingAppService>();
			await service.CreatePolicyAsync(new() { AcademicYearId = cycle.AcademicYearId, Name = "Portal test policy",
				RequireDocuments = false, RequireFamilyDetails = false, RequireCommunicationDetails = false,
				RequireStudentAccount = type == "student", RequireGuardianAccount = type == "guardian" });
			await ConfigureOfferingReadinessAsync(fixture.Offering, cycle.AcademicYearId, prepareAccess: type == "student");
			var settings = (await service.GetSettingsAsync(cycle.AcademicYearId)).Single();
			var configuration = System.Text.Json.Nodes.JsonNode.Parse(settings.ConfigurationJson)!.AsObject();
			configuration["readinessAccess"]!["guardianAccount"] = type == "guardian";
			configuration["onboardingProfiles"]![0]!["rules"]!["guardianAccount"] = type == "guardian";
			await service.SaveSettingsAsync(new() { AcademicYearId = cycle.AcademicYearId,
				ConfigurationJson = configuration.ToJsonString(), ConcurrencyStamp = settings.ConcurrencyStamp });
			var structure = await CreateFeeSelectionReferenceAsync(fixture.Offering, cycle.AcademicYearId);
			var finalized = await FinalizeAsync(fixture.Application, true, "portal-test");
			var email = $"portal-{Guid.NewGuid():N}@example.test";
			var users = GetRequiredService<IdentityUserManager>();
			await WithUnitOfWorkAsync(async () =>
			{
				var roles = GetRequiredService<IdentityRoleManager>();
				if (!await roles.RoleExistsAsync(role))
					(await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), role, TestTenantId))).Succeeded.ShouldBeTrue();
				var student = await _studentRepository.GetRegistrationAggregateAsync(finalized.StudentId);
				student!.ApplyVerifiedRegistrationContact("9000000001", null, email, "Test address", null, null, null, null, null, null);
				var capturedApplicant = await _applicantRepository.GetAsync(fixture.Applicant.Id);
				capturedApplicant.ExtraProperties["AdmissionStudentField.Applicant.Email"] = email;
				await _applicantRepository.UpdateAsync(capturedApplicant, true);
				await _studentRepository.UpdateAsync(student, true);
				var familyRepo = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>();
				var family = await familyRepo.FindAsync(student.Id);
				var newFamily = family == null;
				family ??= new StudentFamilyDetail(student.Id, TestTenantId);
				family.UpdateFatherDetails(new ParentGuardianDetails("Test Father", null, null, email, "9000000002", null, null, null, null, null, null, null, null));
				family.UpdateMotherDetails(null);
				family.UpdateGuardianDetails(new ParentGuardianDetails("Other Guardian", null, null, "other-owner@example.test", null, null, null, null, null, null, null, null, null));
				if (newFamily) await familyRepo.InsertAsync(family, true); else await familyRepo.UpdateAsync(family, true);
			});
			var division = await WithUnitOfWorkAsync(async () =>
			{
				var value = await GetRequiredService<DivisionManager>().CreateAsync(fixture.Offering.StandardId, "PortalA", 40);
				return await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(value, true);
			});
			var item = await service.GetAsync(finalized.OnboardingCaseId);
			var input = new ProvisionStudentPortalAccountDto { AccountType = type, Relationship = relationship, Email = email,
				RelationshipVerified = true, ConcurrencyStamp = item.ConcurrencyStamp };
			(await Should.ThrowAsync<BusinessException>(() => service.ProvisionPortalAccountAsync(item.Id, input)))
				.Code.ShouldBe(StudentOnboardingErrorCodes.CaseNotEditable);
			(await Should.ThrowAsync<BusinessException>(() => service.EnableGuardianCredentialEmailAsync(item.Id,
				new() { Reason = "Case-specific delivery correction", ConcurrencyStamp = item.ConcurrencyStamp })))
				.Code.ShouldBe(StudentOnboardingErrorCodes.CaseNotEditable);
			item = await service.SetPlacementAsync(item.Id, new() { DivisionId = division.Id, ConcurrencyStamp = item.ConcurrencyStamp });
			var series = (await service.GetAvailableIdentifierSeriesAsync(item.Id)).Single();
			item = await service.AllocateIdentifierAsync(item.Id, new() { NumberSeriesId = series.Id, RollNumber = 1, ConcurrencyStamp = item.ConcurrencyStamp });
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			await service.SaveFeeStructureAsync(item.Id, new() { StructureId = structure.Id, ConcurrencyStamp = item.ConcurrencyStamp });
			item = await service.GetAsync(item.Id);
			(await service.PreviewActivationAsync(item.Id)).LastError.ShouldBeNullOrEmpty();
			item = await service.ActivateAsync(item.Id, new() { IdempotencyKey = "portal-activation", ConcurrencyStamp = item.ConcurrencyStamp });
			item.StudentEnrollmentId.ShouldNotBeNull();
			item.Status.ShouldBe(StudentOnboardingStatus.Completed);
			item.StudentAccountRequired.ShouldBeFalse();
			item.GuardianAccountRequired.ShouldBeFalse();
			input.ConcurrencyStamp = item.ConcurrencyStamp;
			item.PortalAccountOwners.ShouldNotBeNull();
			item.PortalAccountOwners!.Single(owner => owner.AccountType == "guardian" && owner.Relationship == "father").Email.ShouldBe(email);
			item.PortalAccountOwners.Single(owner => owner.Relationship == "self").Email.ShouldBe(email);
			item.PortalAccountOwners.ShouldNotContain(owner => owner.Relationship == "mother");
			item.PortalAccountOwners.ShouldContain(owner => owner.Relationship == "guardian" && owner.Email == "other-owner@example.test");
			input.Email = "not-the-saved-owner@example.test";
			(await Should.ThrowAsync<BusinessException>(() => service.ProvisionPortalAccountAsync(item.Id, input)))
				.Code.ShouldBe(StudentOnboardingErrorCodes.PortalAccountInvalid);
			input.Email = email;
			input.RelationshipVerified = false;
			var validation = await Should.ThrowAsync<AbpValidationException>(() => service.ProvisionPortalAccountAsync(item.Id, input));
			validation.ValidationErrors.ShouldContain(error => error.MemberNames.Contains(nameof(input.RelationshipVerified)));
			input.RelationshipVerified = true;
			// Start the successful command with the current stamp after the rejected requests.
			input.ConcurrencyStamp = (await service.GetAsync(item.Id)).ConcurrencyStamp;
			var result = await service.ProvisionPortalAccountAsync(item.Id, input);
			(await service.ProvisionPortalAccountAsync(item.Id, input)).UserId.ShouldBe(result.UserId);
			var user = await WithUnitOfWorkAsync(() => users.GetByIdAsync(result.UserId));
			user.PasswordHash.ShouldBeNull();
			user.EmailConfirmed.ShouldBeFalse();
			(await WithUnitOfWorkAsync(() => users.GetRolesAsync(user))).ShouldBe([role]);
			(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>()
				.CountAsync(x => x.StudentId == finalized.StudentId))).ShouldBe(1);
			using (_principalAccessor.Change(EligibilityPrincipal(user.Id, email, role)))
			{
				await Should.ThrowAsync<AbpAuthorizationException>(() => service.GetMyPortalStudentsAsync());
				await Should.ThrowAsync<EntityNotFoundException>(() => service.GetPortalAccountsAsync(item.Id));
			}
			// Exercise the existing owner password-setup service, not a staff email-confirmation bypass.
			await WithUnitOfWorkAsync(async () =>
			{
				var pending = await users.GetByIdAsync(user.Id);
				(await users.AddPasswordAsync(pending, "Initial-Portal-Test-29!")).Succeeded.ShouldBeTrue();
				pending.SetShouldChangePasswordOnNextLogin(true);
				(await users.UpdateAsync(pending)).Succeeded.ShouldBeTrue();
			});
			var accountService = GetRequiredService<IAccountAppService>();
			await Should.ThrowAsync<Exception>(() => accountService.ResetPasswordAsync(new ResetPasswordDto
			{
				UserId = user.Id, ResetToken = "invalid-owner-token", Password = "Portal-Test-Only-29!"
			}));
			(await WithUnitOfWorkAsync(() => users.GetByIdAsync(user.Id))).EmailConfirmed.ShouldBeFalse();
			(await WithUnitOfWorkAsync(() => users.GetByIdAsync(user.Id))).ShouldChangePasswordOnNextLogin.ShouldBeTrue();
			var resetToken = await WithUnitOfWorkAsync(async () => await users.GeneratePasswordResetTokenAsync(await users.GetByIdAsync(user.Id)));
			await accountService.ResetPasswordAsync(new ResetPasswordDto
			{
				UserId = user.Id, ResetToken = resetToken, Password = "Portal-Test-Only-29!"
			});
			(await WithUnitOfWorkAsync(() => users.GetByIdAsync(user.Id))).EmailConfirmed.ShouldBeTrue();
			(await WithUnitOfWorkAsync(() => users.GetByIdAsync(user.Id))).ShouldChangePasswordOnNextLogin.ShouldBeFalse();
			await Should.ThrowAsync<Exception>(() => accountService.ResetPasswordAsync(new ResetPasswordDto
			{
				UserId = user.Id, ResetToken = resetToken, Password = "Portal-Test-Replay-29!"
			}));
			using (_principalAccessor.Change(EligibilityPrincipal(user.Id, email, role)))
				(await service.GetMyPortalStudentsAsync()).Single().StudentId.ShouldBe(finalized.StudentId);
			using (_currentTenant.Change(Guid.NewGuid()))
				await Should.ThrowAsync<EntityNotFoundException>(() => service.GetPortalAccountsAsync(item.Id));
			item = await service.GetAsync(item.Id);
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			item.Status.ShouldBe(StudentOnboardingStatus.Completed);
			// These summary fields describe required handoffs; optional logins are verified below.
			(type == "student" ? item.StudentAccountStatus : item.GuardianAccountStatus)
				.ShouldBeNull();
			(await service.GetServiceHandoffsAsync(item.Id)).Single(x => x.ServiceKey == type + "-account")
				.Status.ShouldBe(StudentOnboardingServiceHandoffStatus.Acknowledged);
		});
}
