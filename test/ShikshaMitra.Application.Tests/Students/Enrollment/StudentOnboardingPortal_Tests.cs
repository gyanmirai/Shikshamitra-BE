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
	public Task Onboarding_optional_document_exception_requires_approval_only_while_incomplete() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("OptionalDocs");
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
			item.Steps.Single(step => step.Key == StudentOnboardingStepKeys.Documents).Required.ShouldBeFalse();
			item.Steps.Single(step => step.Key == StudentOnboardingStepKeys.ApprovalReview).Required.ShouldBeTrue();

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
			item.Steps.Single(step => step.Key == StudentOnboardingStepKeys.ApprovalReview).Required.ShouldBeFalse();
		});

	[Fact]
	public Task Onboarding_completes_only_after_a_confirmed_first_installment() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("JayFees");
			var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
			var service = GetRequiredService<IStudentOnboardingAppService>();
			await service.CreatePolicyAsync(new() { AcademicYearId = cycle.AcademicYearId, Name = "Fee completion test",
				RequireDocuments = false, RequireFamilyDetails = false, RequireCommunicationDetails = false,
				RequireStudentAccount = false, RequireGuardianAccount = false });
			var finalized = await FinalizeAsync(fixture.Application, true, "fee-completion-test");
			var division = await WithUnitOfWorkAsync(async () =>
			{
				var value = await GetRequiredService<DivisionManager>().CreateAsync(fixture.Offering.StandardId, "FeeA", 40);
				return await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(value, true);
			});
			var item = await service.GetAsync(finalized.OnboardingCaseId);
			item = await service.SetPlacementAsync(item.Id, new() { DivisionId = division.Id, ConcurrencyStamp = item.ConcurrencyStamp });
			var series = await service.CreateIdentifierSeriesAsync(new() { AcademicYearId = cycle.AcademicYearId,
				Code = "FEECOMP", Name = "Fee completion test series", EffectiveFrom = DateTime.UtcNow.AddDays(-1) });
			item = await service.AllocateIdentifierAsync(item.Id, new() { NumberSeriesId = series.Id, RollNumber = 1, ConcurrencyStamp = item.ConcurrencyStamp });
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			item = await service.ActivateAsync(item.Id, new() { IdempotencyKey = "fee-completion-activation", ConcurrencyStamp = item.ConcurrencyStamp });
			item.Status.ShouldBe(StudentOnboardingStatus.Provisioning);
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			item.Status.ShouldBe(StudentOnboardingStatus.NeedsAttention);
			item.FeeAssignmentConfirmed.ShouldBe(false);

			var assignment = new StudentFeeStructure(Guid.NewGuid(), TestTenantId, finalized.StudentId,
				Guid.NewGuid(), cycle.AcademicYearId);
			var first = new DueFee(Guid.NewGuid(), TestTenantId, finalized.StudentId, Guid.NewGuid(),
				(MonthEnum)5, 2026, false, 6000m, new DateTime(2026, 6, 10));
			var later = new DueFee(Guid.NewGuid(), TestTenantId, finalized.StudentId, Guid.NewGuid(),
				(MonthEnum)11, 2026, false, 6000m, new DateTime(2026, 12, 10));
			first.BindAssignment(assignment.Id);
			later.BindAssignment(assignment.Id);
			assignment.CaptureAssignment("fee-completion-test", JsonSerializer.Serialize(new
			{
				StudentId = finalized.StudentId, AcademicYearId = cycle.AcademicYearId, ProratedTotal = 12000m,
				Lines = new[] { new { DueFeeId = first.Id }, new { DueFeeId = later.Id } }
			}));
			await WithUnitOfWorkAsync(async () =>
			{
				await GetRequiredService<IRepository<StudentFeeStructure, Guid>>().InsertAsync(assignment, true);
				var dues = GetRequiredService<IRepository<DueFee, Guid>>();
				await dues.InsertAsync(first, true);
				await dues.InsertAsync(later, true);
			});
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			item.Status.ShouldBe(StudentOnboardingStatus.NeedsAttention);
			item.FeeAssignmentConfirmed.ShouldBe(true);
			item.FirstPaymentConfirmed.ShouldBe(false);

			var receipt = new FeeRecordMetadata(Guid.NewGuid(), TestTenantId, finalized.StudentId,
				cycle.AcademicYearId, 6000, "REC-2026-FEE-TEST", FeeCollectionStatus.InReconciliation);
			var allocation = new FeeCollectionDueFeeAllocation(Guid.NewGuid(), TestTenantId, receipt.Id, first.Id, 6000);
			first.ApplyPayment(6000m);
			await WithUnitOfWorkAsync(async () =>
			{
				await GetRequiredService<IRepository<DueFee, Guid>>().UpdateAsync(first, true);
				await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(receipt, true);
				await GetRequiredService<IRepository<FeeCollectionDueFeeAllocation, Guid>>().InsertAsync(allocation, true);
			});
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			item.Status.ShouldBe(StudentOnboardingStatus.NeedsAttention);
			item.FirstPaymentConfirmed.ShouldBe(false);

			receipt.MarkPaid();
			await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().UpdateAsync(receipt, true));
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			item.FirstPaymentConfirmed.ShouldBe(true);
			item.Status.ShouldBe(StudentOnboardingStatus.Completed, item.LastError);
			(await service.GetServiceHandoffsAsync(item.Id)).Single(h => h.ServiceKey == "finance-first-payment")
				.Status.ShouldBe(StudentOnboardingServiceHandoffStatus.Acknowledged);
			(await service.GetAsync(item.Id)).Status.ShouldBe(StudentOnboardingStatus.Completed);
			(await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp)).Status.ShouldBe(StudentOnboardingStatus.Completed);
			(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(e => e.StudentId == finalized.StudentId)))
				.ShouldBe(1);
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
	public Task Onboarding_service_consent_requires_an_audited_confirmation_without_bypassing_first_payment() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("JayConsent");
			var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
			var service = GetRequiredService<IStudentOnboardingAppService>();
			await service.CreatePolicyAsync(new() { AcademicYearId = cycle.AcademicYearId, Name = "Consent completion test",
				RequireDocuments = false, RequireFamilyDetails = false, RequireCommunicationDetails = false,
				RequireStudentAccount = false, RequireGuardianAccount = false, RequireServiceConsent = true });
			var finalized = await FinalizeAsync(fixture.Application, true, "consent-completion-test");
			var division = await WithUnitOfWorkAsync(async () =>
			{
				var value = await GetRequiredService<DivisionManager>().CreateAsync(fixture.Offering.StandardId, "ConsentA", 40);
				return await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(value, true);
			});
			var item = await service.GetAsync(finalized.OnboardingCaseId);
			item = await service.SetPlacementAsync(item.Id, new() { DivisionId = division.Id, ConcurrencyStamp = item.ConcurrencyStamp });
			var series = await service.CreateIdentifierSeriesAsync(new() { AcademicYearId = cycle.AcademicYearId,
				Code = "CONSENT", Name = "Consent completion series", EffectiveFrom = DateTime.UtcNow.AddDays(-1) });
			item = await service.AllocateIdentifierAsync(item.Id, new() { NumberSeriesId = series.Id,
				RollNumber = 1, ConcurrencyStamp = item.ConcurrencyStamp });
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			item = await service.ActivateAsync(item.Id, new() { IdempotencyKey = "consent-activation",
				ConcurrencyStamp = item.ConcurrencyStamp });
			item.Status.ShouldBe(StudentOnboardingStatus.Provisioning);
			(await service.GetServiceHandoffsAsync(item.Id)).Single(h => h.ServiceKey == "service-consent")
				.Status.ShouldBe(StudentOnboardingServiceHandoffStatus.Pending);
			(await Should.ThrowAsync<AbpValidationException>(() => service.ConfirmServiceConsentAsync(item.Id, new()
			{
				ConcurrencyStamp = item.ConcurrencyStamp, EvidenceReference = "  "
			}))).ValidationErrors.ShouldNotBeEmpty();
			(await Should.ThrowAsync<BusinessException>(() => service.ConfirmServiceConsentAsync(item.Id, new()
			{
				ConcurrencyStamp = "stale", EvidenceReference = "Signed form record CONSENT-001"
			}))).Code.ShouldBe(StudentOnboardingErrorCodes.ConcurrencyConflict);
			item = await service.ConfirmServiceConsentAsync(item.Id, new()
			{
				ConcurrencyStamp = item.ConcurrencyStamp, EvidenceReference = "Signed form record CONSENT-001"
			});
			item.Status.ShouldBe(StudentOnboardingStatus.NeedsAttention);
			item.FirstPaymentConfirmed.ShouldBe(false);
			(await service.GetServiceHandoffsAsync(item.Id)).Single(h => h.ServiceKey == "service-consent")
				.Status.ShouldBe(StudentOnboardingServiceHandoffStatus.Acknowledged);

			var assignment = new StudentFeeStructure(Guid.NewGuid(), TestTenantId, finalized.StudentId,
				Guid.NewGuid(), cycle.AcademicYearId);
			var first = new DueFee(Guid.NewGuid(), TestTenantId, finalized.StudentId, Guid.NewGuid(),
				(MonthEnum)5, 2026, false, 6000m, new DateTime(2026, 6, 10));
			first.BindAssignment(assignment.Id);
			first.ApplyPayment(6000m);
			assignment.CaptureAssignment("consent-completion-test", JsonSerializer.Serialize(new
			{
				StudentId = finalized.StudentId, AcademicYearId = cycle.AcademicYearId, ProratedTotal = 6000m,
				Lines = new[] { new { DueFeeId = first.Id } }
			}));
			var receipt = new FeeRecordMetadata(Guid.NewGuid(), TestTenantId, finalized.StudentId,
				cycle.AcademicYearId, 6000, "REC-2026-CONSENT-TEST", FeeCollectionStatus.Paid);
			await WithUnitOfWorkAsync(async () =>
			{
				await GetRequiredService<IRepository<StudentFeeStructure, Guid>>().InsertAsync(assignment, true);
				await GetRequiredService<IRepository<DueFee, Guid>>().InsertAsync(first, true);
				await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(receipt, true);
				await GetRequiredService<IRepository<FeeCollectionDueFeeAllocation, Guid>>().InsertAsync(
					new FeeCollectionDueFeeAllocation(Guid.NewGuid(), TestTenantId, receipt.Id, first.Id, 6000), true);
			});
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			item.Status.ShouldBe(StudentOnboardingStatus.Completed, item.LastError);
			(await service.ConfirmServiceConsentAsync(item.Id, new()
			{
				ConcurrencyStamp = "already-confirmed", EvidenceReference = "Signed form record CONSENT-001"
			})).Status.ShouldBe(StudentOnboardingStatus.Completed);
			item.Events.ShouldContain(e => e.EventType == "ServiceConsentConfirmed" &&
				e.Description.Contains("CONSENT-001"));
			(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(e => e.StudentId == finalized.StudentId)))
				.ShouldBe(1);
		});

	[Fact]
	public Task Onboarding_requires_both_portal_links_and_confirmed_payment_to_complete() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("JayComplete");
			var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
			var service = GetRequiredService<IStudentOnboardingAppService>();
			await service.CreatePolicyAsync(new() { AcademicYearId = cycle.AcademicYearId, Name = "Complete onboarding test",
				RequireDocuments = false, RequireFamilyDetails = false, RequireCommunicationDetails = false,
				RequireStudentAccount = true, RequireGuardianAccount = true });
			var finalized = await FinalizeAsync(fixture.Application, true, "complete-onboarding-test");
			var studentEmail = $"student-{Guid.NewGuid():N}@example.test";
			var guardianEmail = $"guardian-{Guid.NewGuid():N}@example.test";
			await WithUnitOfWorkAsync(async () =>
			{
				var roles = GetRequiredService<IdentityRoleManager>();
				foreach (var role in new[] { "Student", "Parent" })
					if (!await roles.RoleExistsAsync(role))
						(await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), role, TestTenantId))).Succeeded.ShouldBeTrue();
				var student = await _studentRepository.GetRegistrationAggregateAsync(finalized.StudentId);
				student!.ApplyVerifiedRegistrationContact("9000000001", null, studentEmail, "Test address", null, null, null, null, null, null);
				await _studentRepository.UpdateAsync(student, true);
				var familyRepo = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>();
				var family = await familyRepo.FindAsync(student.Id);
				var newFamily = family == null;
				family ??= new StudentFamilyDetail(student.Id, TestTenantId);
				family.UpdateFatherDetails(new ParentGuardianDetails("Test Father", null, null, guardianEmail,
					"9000000002", null, null, null, null, null, null, null, null));
				if (newFamily) await familyRepo.InsertAsync(family, true); else await familyRepo.UpdateAsync(family, true);
			});
			var division = await WithUnitOfWorkAsync(async () =>
			{
				var value = await GetRequiredService<DivisionManager>().CreateAsync(fixture.Offering.StandardId, "CompleteA", 40);
				return await GetRequiredService<IRepository<Division, Guid>>().InsertAsync(value, true);
			});
			var item = await service.GetAsync(finalized.OnboardingCaseId);
			item = await service.SetPlacementAsync(item.Id, new() { DivisionId = division.Id, ConcurrencyStamp = item.ConcurrencyStamp });
			var series = await service.CreateIdentifierSeriesAsync(new() { AcademicYearId = cycle.AcademicYearId,
				Code = "FULLCOMP", Name = "Complete onboarding series", EffectiveFrom = DateTime.UtcNow.AddDays(-1) });
			item = await service.AllocateIdentifierAsync(item.Id, new() { NumberSeriesId = series.Id, RollNumber = 1, ConcurrencyStamp = item.ConcurrencyStamp });
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			item = await service.ActivateAsync(item.Id, new() { IdempotencyKey = "complete-onboarding-activation", ConcurrencyStamp = item.ConcurrencyStamp });
			item.Status.ShouldBe(StudentOnboardingStatus.Provisioning);

			var assignment = new StudentFeeStructure(Guid.NewGuid(), TestTenantId, finalized.StudentId,
				Guid.NewGuid(), cycle.AcademicYearId);
			var first = new DueFee(Guid.NewGuid(), TestTenantId, finalized.StudentId, Guid.NewGuid(),
				(MonthEnum)5, 2026, false, 6000m, new DateTime(2026, 6, 10));
			first.BindAssignment(assignment.Id);
			first.ApplyPayment(6000m);
			assignment.CaptureAssignment("complete-onboarding-test", JsonSerializer.Serialize(new
			{
				StudentId = finalized.StudentId, AcademicYearId = cycle.AcademicYearId, ProratedTotal = 6000m,
				Lines = new[] { new { DueFeeId = first.Id } }
			}));
			var receipt = new FeeRecordMetadata(Guid.NewGuid(), TestTenantId, finalized.StudentId,
				cycle.AcademicYearId, 6000, "REC-2026-FULL-TEST", FeeCollectionStatus.Paid);
			await WithUnitOfWorkAsync(async () =>
			{
				await GetRequiredService<IRepository<StudentFeeStructure, Guid>>().InsertAsync(assignment, true);
				await GetRequiredService<IRepository<DueFee, Guid>>().InsertAsync(first, true);
				await GetRequiredService<IRepository<FeeRecordMetadata, Guid>>().InsertAsync(receipt, true);
				await GetRequiredService<IRepository<FeeCollectionDueFeeAllocation, Guid>>().InsertAsync(
					new FeeCollectionDueFeeAllocation(Guid.NewGuid(), TestTenantId, receipt.Id, first.Id, 6000), true);
			});
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			item.FirstPaymentConfirmed.ShouldBe(true);
			item.Status.ShouldBe(StudentOnboardingStatus.NeedsAttention);

			await service.ProvisionPortalAccountAsync(item.Id, new() { AccountType = "student", Relationship = "self",
				Email = studentEmail, RelationshipVerified = true, ConcurrencyStamp = item.ConcurrencyStamp });
			item = await service.GetAsync(item.Id);
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			item.Status.ShouldBe(StudentOnboardingStatus.NeedsAttention);
			await service.ProvisionPortalAccountAsync(item.Id, new() { AccountType = "guardian", Relationship = "father",
				Email = guardianEmail, RelationshipVerified = true, ConcurrencyStamp = item.ConcurrencyStamp });
			item = await service.GetAsync(item.Id);
			item.Status.ShouldBe(StudentOnboardingStatus.Completed, item.LastError);
			item.StudentAccountStatus.ShouldBe(StudentOnboardingServiceHandoffStatus.Acknowledged);
			item.GuardianAccountStatus.ShouldBe(StudentOnboardingServiceHandoffStatus.Acknowledged);
			(await service.GetServiceHandoffsAsync(item.Id)).Where(h => h.Required)
				.ShouldAllBe(h => h.Status == StudentOnboardingServiceHandoffStatus.Acknowledged);
			(await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>()
				.CountAsync(x => x.StudentId == finalized.StudentId))).ShouldBe(2);
			(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(x => x.StudentId == finalized.StudentId)))
				.ShouldBe(1);
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
			var fixture = await CreateCertificationFixtureAsync("JayPortal");
			var cycle = await WithUnitOfWorkAsync(() => _cycleRepository.GetAsync(fixture.Offering.AdmissionCycleId));
			var service = GetRequiredService<IStudentOnboardingAppService>();
			await service.CreatePolicyAsync(new() { AcademicYearId = cycle.AcademicYearId, Name = "Portal test policy",
				RequireDocuments = false, RequireFamilyDetails = false, RequireCommunicationDetails = false,
				RequireStudentAccount = type == "student", RequireGuardianAccount = type == "guardian" });
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
				await _studentRepository.UpdateAsync(student, true);
				var familyRepo = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>();
				var family = await familyRepo.FindAsync(student.Id);
				var newFamily = family == null;
				family ??= new StudentFamilyDetail(student.Id, TestTenantId);
				family.UpdateFatherDetails(new ParentGuardianDetails("Test Father", null, null, email, "9000000002", null, null, null, null, null, null, null, null));
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
			item = await service.SetPlacementAsync(item.Id, new() { DivisionId = division.Id, ConcurrencyStamp = item.ConcurrencyStamp });
			var series = await service.CreateIdentifierSeriesAsync(new() { AcademicYearId = cycle.AcademicYearId,
				Code = "PORTAL", Name = "Portal test series", EffectiveFrom = DateTime.UtcNow.AddDays(-1) });
			item = await service.AllocateIdentifierAsync(item.Id, new() { NumberSeriesId = series.Id, RollNumber = 1, ConcurrencyStamp = item.ConcurrencyStamp });
			item = await service.RefreshReadinessAsync(item.Id, item.ConcurrencyStamp);
			item = await service.ActivateAsync(item.Id, new() { IdempotencyKey = "portal-activation", ConcurrencyStamp = item.ConcurrencyStamp });
			item.Status.ShouldBe(StudentOnboardingStatus.Provisioning);
			item.StudentAccountRequired.ShouldBe(type == "student");
			item.GuardianAccountRequired.ShouldBe(type == "guardian");
			input.ConcurrencyStamp = item.ConcurrencyStamp;
			input.Email = "not-the-saved-owner@example.test";
			(await Should.ThrowAsync<BusinessException>(() => service.ProvisionPortalAccountAsync(item.Id, input)))
				.Code.ShouldBe(StudentOnboardingErrorCodes.PortalAccountInvalid);
			input.Email = email;
			input.RelationshipVerified = false;
			var validation = await Should.ThrowAsync<AbpValidationException>(() => service.ProvisionPortalAccountAsync(item.Id, input));
			validation.ValidationErrors.ShouldContain(error => error.MemberNames.Contains(nameof(input.RelationshipVerified)));
			input.RelationshipVerified = true;
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
			var accountService = GetRequiredService<IAccountAppService>();
			await Should.ThrowAsync<Exception>(() => accountService.ResetPasswordAsync(new ResetPasswordDto
			{
				UserId = user.Id, ResetToken = "invalid-owner-token", Password = "Portal-Test-Only-29!"
			}));
			(await WithUnitOfWorkAsync(() => users.GetByIdAsync(user.Id))).EmailConfirmed.ShouldBeFalse();
			var resetToken = await WithUnitOfWorkAsync(async () => await users.GeneratePasswordResetTokenAsync(await users.GetByIdAsync(user.Id)));
			await accountService.ResetPasswordAsync(new ResetPasswordDto
			{
				UserId = user.Id, ResetToken = resetToken, Password = "Portal-Test-Only-29!"
			});
			(await WithUnitOfWorkAsync(() => users.GetByIdAsync(user.Id))).EmailConfirmed.ShouldBeTrue();
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
			item.Status.ShouldBe(StudentOnboardingStatus.NeedsAttention); // Fee assignment/payment still missing.
			(type == "student" ? item.StudentAccountStatus : item.GuardianAccountStatus)
				.ShouldBe(StudentOnboardingServiceHandoffStatus.Acknowledged);
			(await service.GetServiceHandoffsAsync(item.Id)).Single(x => x.ServiceKey == type + "-account")
				.Status.ShouldBe(StudentOnboardingServiceHandoffStatus.Acknowledged);
		});
}
