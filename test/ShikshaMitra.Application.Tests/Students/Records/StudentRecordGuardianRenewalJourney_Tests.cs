using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.Students.ValueObjects;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData("SuspendUntilReview")]
	[InlineData("KeepUntilReview")]
	public Task Record_approved_guardian_change_requires_resolved_access_review_before_paid_renewal_and_preserves_sibling(string mode) => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordRenewalFixtureAsync(requirePortals: true);
		var renewal = await fixture.Service.ActivateRenewalAsync(fixture.Renewal.Id, new()
		{
			ConcurrencyStamp = fixture.Renewal.ConcurrencyStamp, Remarks = "Annual checks completed before owner portal provisioning"
		});
		renewal.Status.ShouldBe("Provisioning");
		await WithUnitOfWorkAsync(async () =>
		{
			var roles = GetRequiredService<IdentityRoleManager>();
			foreach (var name in new[] { "Student", "Parent" })
				if (!await roles.RoleExistsAsync(name)) (await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), name, TestTenantId))).Succeeded.ShouldBeTrue();
		});
		var options = await fixture.Service.GetRenewalPortalOptionsAsync(renewal.Id);
		var father = options.Guardians.Single(g => g.Relationship == "father");
		var studentAccount = await fixture.Service.ProvisionRenewalPortalAsync(renewal.Id, new ProvisionStudentPortalAccountDto
		{
			AccountType = "student", Relationship = "self", Email = options.StudentEmail!, RelationshipVerified = true, ConcurrencyStamp = renewal.ConcurrencyStamp
		});
		var parentAccount = await fixture.Service.ProvisionRenewalPortalAsync(renewal.Id, new ProvisionStudentPortalAccountDto
		{
			AccountType = "guardian", Relationship = father.Relationship, Email = father.Email, RelationshipVerified = true, ConcurrencyStamp = renewal.ConcurrencyStamp
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var users = GetRequiredService<IdentityUserManager>();
			var parent = (await users.FindByIdAsync(parentAccount.UserId.ToString()))!;
			parent.SetEmailConfirmed(true); (await users.UpdateAsync(parent)).Succeeded.ShouldBeTrue();
		});
		var finance = await SeedRecordRenewalFinanceAsync(fixture.StudentId, renewal.AcademicYearId);
		await CollectRecordRenewalCashAsync(fixture.StudentId, renewal.AcademicYearId, finance.FeeHeadId, finance.DueId, 6000);
		(await fixture.Service.GetRenewalAsync(renewal.Id)).Issues.ShouldBeEmpty();

		var siblingOffering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var sibling = await CreateExistingStudentAsync(siblingOffering, "GuardianJourneySibling", "Student");
		await GrantCompletedRecordAsync(sibling);
		await WithUnitOfWorkAsync(async () =>
		{
			var families = GetRequiredService<IRepository<StudentFamilyDetail, Guid>>();
			var family = await families.FindAsync(sibling.Id) ?? await families.InsertAsync(new StudentFamilyDetail(sibling.Id, TestTenantId), true);
			family.UpdateFatherDetails(new ParentGuardianDetails("Renewal Father", null, null, father.Email, "9000000022", null, null, null, null, null, null, null, null));
			await families.UpdateAsync(family, true);
			await GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().InsertAsync(new(Guid.NewGuid(), TestTenantId,
				sibling.Id, parentAccount.UserId, "guardian", "father", TestUserId), true);
		});
		var guardians = GetRequiredService<IStudentGuardianAccessAppService>();
		var previousLink = (await guardians.GetLinksAsync(fixture.StudentId)).Single();
		var siblingLink = (await guardians.GetLinksAsync(sibling.Id)).Single();
		previousLink.UserId.ShouldBe(parentAccount.UserId); siblingLink.UserId.ShouldBe(parentAccount.UserId);
		(await guardians.GetReviewsAsync(fixture.StudentId)).ShouldBeEmpty();

		// Use the real settings owner to update the existing school configuration, rather than inserting a second school row.
		var settings = GetRequiredService<IStudentRecordsSettingsAppService>();
		var configuration = await settings.GetAsync(null);
		var policy = StudentRecordsPolicy.FromJson(configuration.EffectivePolicy.ToJson());
		policy.GuardianAccess.OnApprovedChange = mode;
		policy.Changes["Guardian"].Mode = "Approval"; policy.Changes["Guardian"].Stages = [new() { RoleNames = ["admin"] }];
		configuration = await settings.SaveDraftAsync(new() { ConcurrencyStamp = configuration.ConcurrencyStamp, Policy = policy });
		configuration = await settings.PublishAsync(new() { ConcurrencyStamp = configuration.ConcurrencyStamp! });

		var changes = GetRequiredService<IStudentRecordChangeAppService>();
		var before = await changes.GetSnapshotAsync(fixture.StudentId, "family");
		var payload = JsonNode.Parse(before.Data.GetRawText())!.AsObject();
		payload["fatherDetails"]!.AsObject()["contactNo"] = "9000000033";
		var request = await changes.CreateAsync(new()
		{
			StudentId = fixture.StudentId, Section = "family", ClientKey = Guid.NewGuid().ToString("N"),
			Reason = "Verified guardian phone correction requires an access review", ExpectedSnapshot = before.Snapshot, Payload = JsonSerializer.SerializeToElement(payload)
		});
		request.Status.ShouldBe("Pending"); request.PolicyVersionId.ShouldBe(configuration.PublishedVersionId);
		(await changes.GetSnapshotAsync(fixture.StudentId, "family")).Snapshot.ShouldBe(before.Snapshot);
		(await guardians.GetReviewsAsync(fixture.StudentId)).ShouldBeEmpty();
		(await guardians.GetLinksAsync(fixture.StudentId)).Single().LinkId.ShouldBe(previousLink.LinkId);

		// A later school setting cannot change the access action already captured by the submitted request.
		policy.GuardianAccess.OnApprovedChange = mode == "SuspendUntilReview" ? "KeepUntilReview" : "SuspendUntilReview";
		configuration = await settings.SaveDraftAsync(new() { ConcurrencyStamp = configuration.ConcurrencyStamp, Policy = policy });
		await settings.PublishAsync(new() { ConcurrencyStamp = configuration.ConcurrencyStamp! });
		var reviewerId = Guid.NewGuid();
		await EnsureTestIdentityUserAsync(reviewerId, $"guardian-journey-reviewer-{reviewerId:N}");
		await WithUnitOfWorkAsync(async () =>
		{
			await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: reviewerId), true);
			var users = GetRequiredService<IdentityUserManager>();
			(await users.AddToRoleAsync((await users.FindByIdAsync(reviewerId.ToString()))!, "admin")).Succeeded.ShouldBeTrue();
		});
		using (_principalAccessor.Change(ProfilePrincipal(reviewerId)))
			request = await changes.ApproveAsync(request.Id, new() { ConcurrencyStamp = request.ConcurrencyStamp, Reason = "Registrar verified the guardian and corrected phone" });
		request.Status.ShouldBe("Applied");
		(await changes.GetSnapshotAsync(fixture.StudentId, "family")).Data.GetProperty("fatherDetails").GetProperty("contactNo").GetString().ShouldBe("9000000033");

		var review = (await guardians.GetReviewsAsync(fixture.StudentId)).Single();
		review.ChangeRequestId.ShouldBe(request.Id); review.Status.ShouldBe("Pending"); review.OnApprovedChangeMode.ShouldBe(mode);
		review.PreviousLinks.Single().LinkId.ShouldBe(previousLink.LinkId);
		var expectedHeldLinkCount = mode == "SuspendUntilReview" ? 0 : 1;
		(await guardians.GetLinksAsync(fixture.StudentId)).Count.ShouldBe(expectedHeldLinkCount);
		(await guardians.GetLinksAsync(sibling.Id)).Single().LinkId.ShouldBe(siblingLink.LinkId);
		var held = await fixture.Service.GetRenewalAsync(renewal.Id);
		held.PortalReviewRequired.ShouldBeTrue(); held.Status.ShouldBe("Provisioning");
		held.Issues.ShouldContain(i => i.Code == "Students:Records:RenewalProvisioningRequired" && i.Message.Contains("pending guardian access review"));
		(await Should.ThrowAsync<BusinessException>(() => fixture.Service.ActivateRenewalAsync(renewal.Id, new()
		{
			ConcurrencyStamp = held.ConcurrencyStamp, Remarks = "Paid fees and retained links cannot bypass required access review"
		}))).Code.ShouldBe("Students:Records:RenewalProvisioningRequired");

		// An accepted guardian email cannot be used to associate an unrelated existing identity.
		// The common fixture disables transactions; use a copied database and a normal UOW manager to prove rollback.
		using var transactionalDatabase = await UseRealSerializationTransactionsAsync();
		(await Should.ThrowAsync<BusinessException>(() => ResolveGuardianJourneyInTransactionAsync(review.Id, new()
		{
			ConcurrencyStamp = review.ConcurrencyStamp, Reason = "Attempted wrong existing identity must roll back link revocation",
			RevokeLinkIds = [previousLink.LinkId], NewGuardian = new()
			{
				Relationship = "father", Email = father.Email, ExistingUserId = studentAccount.UserId, ConfirmIdentityVerified = true
			}
		}))).Code.ShouldBe("Students:Records:GuardianIdentityMismatch");
		(await guardians.GetReviewsAsync(fixture.StudentId)).Single().Status.ShouldBe("Pending");
		(await guardians.GetLinksAsync(fixture.StudentId)).Count.ShouldBe(expectedHeldLinkCount);
		(await guardians.GetLinksAsync(sibling.Id)).Single().LinkId.ShouldBe(siblingLink.LinkId);
		(await fixture.Service.GetRenewalAsync(renewal.Id)).Issues.ShouldContain(i => i.Message.Contains("pending guardian access review"));

		(await Should.ThrowAsync<BusinessException>(() => ResolveGuardianJourneyInTransactionAsync(review.Id, new()
		{
			ConcurrencyStamp = review.ConcurrencyStamp, Reason = "Retaining the existing guardian still requires explicit identity verification", KeepLinkIds = [previousLink.LinkId]
		}))).Code.ShouldBe("Students:Records:GuardianIdentityVerificationRequired");
		var resolved = await ResolveGuardianJourneyInTransactionAsync(review.Id, new()
		{
			ConcurrencyStamp = review.ConcurrencyStamp, Reason = "Verified the same guardian identity against the approved family details",
			KeepLinkIds = [previousLink.LinkId], ConfirmRetainedGuardiansVerified = true
		});
		resolved.Status.ShouldBe("Completed"); resolved.ChangeRequestId.ShouldBe(request.Id);
		var restoredLink = (await guardians.GetLinksAsync(fixture.StudentId)).Single();
		restoredLink.UserId.ShouldBe(parentAccount.UserId); restoredLink.Email.ShouldBe(father.Email); restoredLink.IsActive.ShouldBeTrue();
		if (mode == "KeepUntilReview") restoredLink.LinkId.ShouldBe(previousLink.LinkId); else restoredLink.LinkId.ShouldNotBe(previousLink.LinkId);
		var unchangedSibling = (await guardians.GetLinksAsync(sibling.Id)).Single();
		unchangedSibling.LinkId.ShouldBe(siblingLink.LinkId); unchangedSibling.UserId.ShouldBe(parentAccount.UserId); unchangedSibling.IsActive.ShouldBeTrue();
		(await GetRequiredService<IdentityUserManager>().FindByIdAsync(parentAccount.UserId.ToString()))!.IsActive.ShouldBeTrue();
		var finalOptions = await fixture.Service.GetRenewalPortalOptionsAsync(renewal.Id);
		finalOptions.Accounts.Count.ShouldBe(2); finalOptions.Accounts.ShouldContain(a => a.UserId == studentAccount.UserId && a.AccountType == "student");
		renewal = await fixture.Service.GetRenewalAsync(renewal.Id); renewal.Issues.ShouldBeEmpty();
		(await fixture.Service.ActivateRenewalAsync(renewal.Id, new()
		{
			ConcurrencyStamp = renewal.ConcurrencyStamp, Remarks = "Finance settled and approved guardian access review completed"
		})).Status.ShouldBe("Completed");
		(await WithUnitOfWorkAsync(() => _studentEnrollmentRepository.CountAsync(e => e.StudentId == fixture.StudentId))).ShouldBe(2);
		(await guardians.GetLinksAsync(sibling.Id)).Single().LinkId.ShouldBe(siblingLink.LinkId);
	});

	private async Task<StudentGuardianAccessReviewDto> ResolveGuardianJourneyInTransactionAsync(Guid id, ResolveStudentGuardianAccessDto input)
	{
		var manager = ActivatorUtilities.CreateInstance<UnitOfWorkManager>(ServiceProvider);
		using var work = manager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true);
		var database = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
		work.Options.IsTransactional.ShouldBeTrue(); database.Database.CurrentTransaction.ShouldNotBeNull();
		database.Database.GetDbConnection().GetType().ShouldBe(typeof(SqliteConnection));
		var result = await GetRequiredService<IStudentGuardianAccessAppService>().ResolveAsync(id, input);
		await work.CompleteAsync(); return result;
	}
}
