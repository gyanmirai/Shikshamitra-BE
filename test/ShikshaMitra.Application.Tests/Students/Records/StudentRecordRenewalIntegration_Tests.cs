using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Accounts.Settings;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.Students.ValueObjects;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	private async Task<(IStudentRecordTransitionAppService Service, StudentRecordRenewalDto Renewal, Guid StudentId, Guid SourceYear, Guid SourceEnrollment)> RecordRenewalFixtureAsync(bool requirePortals, bool directStatus = false)
	{
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Placement"].Mode = "Direct";
		if (directStatus) policy.Changes["Status"].Mode = "Direct";
		var fixture = await TransitionFixtureAsync(policy);
		var target = await NextRecordYearAsync(fixture.Input.AcademicYearId);
		await GetRequiredService<IStudentOnboardingAppService>().CreatePolicyAsync(new()
		{
			AcademicYearId = target.Year, Name = "Returning student owner readiness",
			RequireDocuments = false, RequireFamilyDetails = false, RequireCommunicationDetails = false,
			RequireMedicalDetails = false, RequireStudentAccount = requirePortals, RequireGuardianAccount = requirePortals
		});
		var sourceEnrollment = await WithUnitOfWorkAsync(async () =>
		{
			var current = (await _studentRepository.GetRegistrationAggregateAsync(fixture.Student.Id))!;
			current.AssignGrNumber("GR-RENEWAL-001", 1);
			if (requirePortals)
			{
				current.ApplyVerifiedRegistrationContact("9000000011", null, $"renewal-student-{Guid.NewGuid():N}@example.test", "Renewal home", null, null, null, null, null, null);
				var family = new StudentFamilyDetail(current.Id, TestTenantId);
				family.UpdateFatherDetails(new ParentGuardianDetails("Renewal Father", null, null, $"renewal-father-{Guid.NewGuid():N}@example.test", "9000000022", null, null, null, null, null, null, null, null));
				await GetRequiredService<IRepository<StudentFamilyDetail, Guid>>().InsertAsync(family, true);
			}
			await _studentRepository.UpdateAsync(current, true);
			return (await _studentEnrollmentRepository.GetAsync(x => x.StudentId == current.Id && x.AcademicYearId == fixture.Input.AcademicYearId)).Id;
		});
		var command = fixture.Input.Items.Single(); command.Outcome = "Promote";
		command.TargetAcademicYearId = target.Year; command.TargetStandardId = target.Standard;
		command.TargetDivisionId = target.Division; command.RollNumber = 9;
		var row = (await fixture.Service.PreviewAsync(fixture.Input)).Items.Single(); row.Ready.ShouldBeTrue();
		command.StudentConcurrencyStamp = row.StudentConcurrencyStamp; command.EnrollmentConcurrencyStamp = row.EnrollmentConcurrencyStamp;
		var transition = (await fixture.Service.SubmitBatchAsync(fixture.Input)).Items.Single(); transition.Status.ShouldBe("AwaitingRenewal");
		var renewal = await fixture.Service.GetRenewalAsync(transition.RenewalCaseId!.Value);
		renewal.Status.ShouldBe("PendingChecklist"); renewal.Issues.ShouldBeEmpty();
		return (fixture.Service, renewal, fixture.Student.Id, fixture.Input.AcademicYearId, sourceEnrollment);
	}

	private Task<(Guid FeeHeadId, Guid DueId)> SeedRecordRenewalFinanceAsync(Guid studentId, Guid yearId) => WithUnitOfWorkAsync(async () =>
	{
		var category = new FeeCategory(Guid.NewGuid(), TestTenantId, yearId, "Renewal tuition", "Annual tuition");
		await GetRequiredService<IRepository<FeeCategory, Guid>>().InsertAsync(category, true);
		var incomeId = await FeeHeadAccountingTestData.CreateIncomeCategoryAsync(
			GetRequiredService<IRepository<AccountHead, Guid>>(), GetRequiredService<IRepository<TransactionCategory, Guid>>(),
			yearId, TestTenantId, "Renewal Tuition");
		var head = new FeeHead(Guid.NewGuid(), TestTenantId, yearId, category.Id, "Renewal installment", FeeHeadModule.Finance, false, "First tuition installment", incomeId);
		await GetRequiredService<IRepository<FeeHead, Guid>>().InsertAsync(head, true);
		var snapshot = new CollectionPolicySnapshot { PolicyName = "Renewal collection", PolicyCode = "RENEWAL-COLLECT", Status = CollectionPolicyStatus.Active, IsDefault = true, AllowOnlineCollection = true };
		var collectionPolicy = new CollectionPolicy(Guid.NewGuid(), TestTenantId, yearId, snapshot.ToUpdateModel());
		await GetRequiredService<IRepository<CollectionPolicy, Guid>>().InsertAsync(collectionPolicy, true);
		var version = new FinancePolicyVersion(Guid.NewGuid(), TestTenantId, "collection", collectionPolicy.Id, yearId,
			collectionPolicy.Version, collectionPolicy.PolicyName, "Active", snapshot.Serialize(), "created");
		await GetRequiredService<IRepository<FinancePolicyVersion, Guid>>().InsertAsync(version, true);
		var assignment = new StudentFeeStructure(Guid.NewGuid(), TestTenantId, studentId, Guid.NewGuid(), yearId,
			null, null, ConcessionFeeHeadScope.EntireFeeStructure, [], collectionPolicy.Id, version.Id);
		var dueDate = (await GetRequiredService<IRepository<TenantSettings.AcademicYears.AcademicYear, Guid>>().GetAsync(yearId)).StartDate.AddDays(5);
		var due = new DueFee(Guid.NewGuid(), TestTenantId, studentId, head.Id, (MonthEnum)(dueDate.Month - 1), dueDate.Year, false, 6000m, dueDate);
		due.BindAssignment(assignment.Id);
		assignment.CaptureAssignment("renewal-finance-owner-evidence", JsonSerializer.Serialize(new
		{
			StudentId = studentId, AcademicYearId = yearId, ProratedTotal = 6000m, Lines = new[] { new { DueFeeId = due.Id } }
		}));
		await GetRequiredService<IRepository<StudentFeeStructure, Guid>>().InsertAsync(assignment, true);
		await GetRequiredService<IRepository<DueFee, Guid>>().InsertAsync(due, true);
		return (head.Id, due.Id);
	});

	private Task<CollectFeeOutput> CollectRecordRenewalCashAsync(Guid studentId, Guid yearId, Guid headId, Guid dueId, double amount) =>
		GetRequiredService<IFeeCollectionAppService>().CollectFeeAsync(new CollectFeeInput
		{
			StudentId = studentId, AcademicYearId = yearId, IdempotencyKey = Guid.NewGuid().ToString("N"),
			DueFeePayments = [new() { DueFeeId = dueId, FeeHeadId = headId, Amount = amount }],
			Payments = [new() { PaymentMethod = PaymentMethodEnum.Cash, Amount = amount }]
		});

	[Fact]
	public Task Record_renewal_completes_only_after_real_finance_collection_and_preserves_both_enrollments() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordRenewalFixtureAsync(requirePortals: false);
		var renewal = await fixture.Service.ActivateRenewalAsync(fixture.Renewal.Id, new() { ConcurrencyStamp = fixture.Renewal.ConcurrencyStamp, Remarks = "Verified annual owner checks" });
		renewal.Status.ShouldBe("Provisioning"); renewal.StudentId.ShouldBe(fixture.StudentId);
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.GetAsync(fixture.SourceEnrollment))).IsActive.ShouldBeFalse();
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(x => x.StudentId == fixture.StudentId))).ShouldBe(2);
		(await Should.ThrowAsync<BusinessException>(() => fixture.Service.ActivateRenewalAsync(renewal.Id,
			new() { ConcurrencyStamp = renewal.ConcurrencyStamp, Remarks = "Finance missing" }))).Code.ShouldBe("Students:Records:RenewalProvisioningRequired");
		var finance = await SeedRecordRenewalFinanceAsync(fixture.StudentId, renewal.AcademicYearId);
		var first = await CollectRecordRenewalCashAsync(fixture.StudentId, renewal.AcademicYearId, finance.FeeHeadId, finance.DueId, 3000);
		first.Status.ShouldBe(FeeCollectionStatus.Partial);
		renewal = await fixture.Service.GetRenewalAsync(renewal.Id);
		(await Should.ThrowAsync<BusinessException>(() => fixture.Service.ActivateRenewalAsync(renewal.Id,
			new() { ConcurrencyStamp = renewal.ConcurrencyStamp, Remarks = "Only half the installment paid" }))).Code.ShouldBe("Students:Records:RenewalProvisioningRequired");
		var final = await CollectRecordRenewalCashAsync(fixture.StudentId, renewal.AcademicYearId, finance.FeeHeadId, finance.DueId, 3000);
		final.Status.ShouldBe(FeeCollectionStatus.Paid);
		renewal = await fixture.Service.GetRenewalAsync(renewal.Id);
		renewal.Issues.ShouldBeEmpty();
		renewal = await fixture.Service.ActivateRenewalAsync(renewal.Id, new() { ConcurrencyStamp = renewal.ConcurrencyStamp, Remarks = "Finance confirmed the full first installment" });
		renewal.Status.ShouldBe("Completed");
		var stored = await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentRecordRenewalCase, Guid>>().GetAsync(renewal.Id));
		(await fixture.Service.GetAsync(stored.TransitionId!.Value)).Status.ShouldBe("Applied");
		(await WithUnitOfWorkAsync(() => _studentRepository.GetAsync(fixture.StudentId))).GrNumber.ShouldBe("GR-RENEWAL-001");
		(await GetRequiredService<IStudentRecordProfileAppService>().GetAsync(fixture.StudentId)).RecordsAccess.ShouldBe("Full");
		(await fixture.Service.ActivateRenewalAsync(renewal.Id, new() { ConcurrencyStamp = renewal.ConcurrencyStamp, Remarks = "Replay completed renewal" })).Status.ShouldBe("Completed");
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(x => x.StudentId == fixture.StudentId))).ShouldBe(2);
	});

	[Fact]
	public Task Record_renewal_required_portals_allow_enrollment_but_block_completion_after_finance_payment() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordRenewalFixtureAsync(requirePortals: true);
		var renewal = await fixture.Service.ActivateRenewalAsync(fixture.Renewal.Id, new() { ConcurrencyStamp = fixture.Renewal.ConcurrencyStamp, Remarks = "Owner portal provisioning follows enrollment" });
		renewal.Status.ShouldBe("Provisioning");
		var finance = await SeedRecordRenewalFinanceAsync(fixture.StudentId, renewal.AcademicYearId);
		await CollectRecordRenewalCashAsync(fixture.StudentId, renewal.AcademicYearId, finance.FeeHeadId, finance.DueId, 6000);
		renewal = await fixture.Service.GetRenewalAsync(renewal.Id);
		renewal.PortalReviewRequired.ShouldBeTrue(); renewal.Issues.Count.ShouldBe(2);
		renewal.Issues.ShouldAllBe(x => x.Code == "Students:Records:RenewalProvisioningRequired" && x.Message.Contains("portal link"));
		(await Should.ThrowAsync<BusinessException>(() => fixture.Service.ActivateRenewalAsync(renewal.Id,
			new() { ConcurrencyStamp = renewal.ConcurrencyStamp, Remarks = "Portal links still missing" }))).Code.ShouldBe("Students:Records:RenewalProvisioningRequired");
		(await fixture.Service.GetRenewalAsync(renewal.Id)).Status.ShouldBe("Provisioning");
		await WithUnitOfWorkAsync(async () =>
		{
			var roleManager = GetRequiredService<IdentityRoleManager>();
			foreach (var roleName in new[] { "Student", "Parent" })
				if (!await roleManager.RoleExistsAsync(roleName)) (await roleManager.CreateAsync(new IdentityRole(Guid.NewGuid(), roleName, TestTenantId))).Succeeded.ShouldBeTrue();
		});
		var portalOptions = await fixture.Service.GetRenewalPortalOptionsAsync(renewal.Id);
		portalOptions.CanProvision.ShouldBeTrue(); portalOptions.StudentRequired.ShouldBeTrue(); portalOptions.GuardianRequired.ShouldBeTrue();
		(await Should.ThrowAsync<BusinessException>(() => fixture.Service.ProvisionRenewalPortalAsync(renewal.Id, new()
		{
			AccountType = "student", Relationship = "self", Email = "wrong-student@example.test", RelationshipVerified = true, ConcurrencyStamp = renewal.ConcurrencyStamp
		}))).Code.ShouldBe(StudentOnboardingErrorCodes.PortalAccountInvalid);
		var studentInput = new ProvisionStudentPortalAccountDto { AccountType = "student", Relationship = "self", Email = portalOptions.StudentEmail!, RelationshipVerified = true, ConcurrencyStamp = renewal.ConcurrencyStamp };
		var studentAccount = await fixture.Service.ProvisionRenewalPortalAsync(renewal.Id, studentInput);
		studentAccount.EmailConfirmed.ShouldBeFalse(); studentAccount.IsActive.ShouldBeTrue();
		(await fixture.Service.ProvisionRenewalPortalAsync(renewal.Id, studentInput)).UserId.ShouldBe(studentAccount.UserId);
		(await fixture.Service.GetRenewalAsync(renewal.Id)).Issues.Count.ShouldBe(1);
		var guardian = portalOptions.Guardians.Single(x => x.Relationship == "father");
		var guardianAccount = await fixture.Service.ProvisionRenewalPortalAsync(renewal.Id, new()
		{
			AccountType = "guardian", Relationship = guardian.Relationship, Email = guardian.Email, RelationshipVerified = true, ConcurrencyStamp = renewal.ConcurrencyStamp
		});
		guardianAccount.EmailConfirmed.ShouldBeFalse(); guardianAccount.IsActive.ShouldBeTrue();
		(await fixture.Service.GetRenewalPortalOptionsAsync(renewal.Id)).Accounts.Count.ShouldBe(2);
		renewal = await fixture.Service.GetRenewalAsync(renewal.Id); renewal.Issues.ShouldBeEmpty();
		var pendingReviewId = Guid.NewGuid();
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IRepository<StudentGuardianAccessReview, Guid>>().InsertAsync(new(pendingReviewId, TestTenantId, fixture.StudentId, Guid.NewGuid(), JsonSerializer.Serialize(new[] { guardianAccount }), "KeepUntilReview"), true);
		});
		var held = await fixture.Service.GetRenewalAsync(renewal.Id);
		held.Issues.ShouldContain(x => x.Code == "Students:Records:RenewalProvisioningRequired" && x.Message.Contains("pending guardian access review"));
		(await fixture.Service.GetRenewalPortalOptionsAsync(renewal.Id)).Accounts.Count.ShouldBe(2);
		(await Should.ThrowAsync<BusinessException>(() => fixture.Service.ActivateRenewalAsync(renewal.Id, new() { ConcurrencyStamp = held.ConcurrencyStamp, Remarks = "Retained links do not settle a pending review" }))).Code.ShouldBe("Students:Records:RenewalProvisioningRequired");
		await WithUnitOfWorkAsync(async () =>
		{
			var reviews = GetRequiredService<IRepository<StudentGuardianAccessReview, Guid>>(); var review = await reviews.GetAsync(pendingReviewId);
			review.Resolve(TestUserId, DateTime.UtcNow, "{\"reviewed\":true}"); await reviews.UpdateAsync(review, true);
		});
		renewal = await fixture.Service.GetRenewalAsync(renewal.Id); renewal.Issues.ShouldBeEmpty();
		await WithUnitOfWorkAsync(async () =>
		{
			var manager = GetRequiredService<IdentityUserManager>(); var user = (await manager.FindByIdAsync(studentAccount.UserId.ToString()))!;
			(await manager.RemoveFromRoleAsync(user, "Student")).Succeeded.ShouldBeTrue();
		});
		(await fixture.Service.GetRenewalAsync(renewal.Id)).Issues.ShouldContain(x => x.Message.Contains("student portal link"));
		await WithUnitOfWorkAsync(async () =>
		{
			var manager = GetRequiredService<IdentityUserManager>(); var user = (await manager.FindByIdAsync(studentAccount.UserId.ToString()))!;
			(await manager.AddToRoleAsync(user, "Student")).Succeeded.ShouldBeTrue();
			var parent = (await manager.FindByIdAsync(guardianAccount.UserId.ToString()))!;
			(await manager.SetEmailAsync(parent, "changed-renewal-guardian@example.test")).Succeeded.ShouldBeTrue();
		});
		(await fixture.Service.GetRenewalAsync(renewal.Id)).Issues.ShouldContain(x => x.Message.Contains("guardian portal link"));
		await WithUnitOfWorkAsync(async () =>
		{
			var manager = GetRequiredService<IdentityUserManager>(); var parent = (await manager.FindByIdAsync(guardianAccount.UserId.ToString()))!;
			(await manager.SetEmailAsync(parent, guardian.Email)).Succeeded.ShouldBeTrue();
		});
		renewal = await fixture.Service.GetRenewalAsync(renewal.Id); renewal.Issues.ShouldBeEmpty();
		(await fixture.Service.ActivateRenewalAsync(renewal.Id, new() { ConcurrencyStamp = renewal.ConcurrencyStamp, Remarks = "Finance and both owner portal handoffs are complete" })).Status.ShouldBe("Completed");
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(x => x.StudentId == fixture.StudentId))).ShouldBe(2);
	});
}
