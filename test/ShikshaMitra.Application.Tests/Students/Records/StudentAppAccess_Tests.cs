using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task App_access_changes_only_the_linked_recipient_and_audits_each_change() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateCertificateStudentOfferingAsync();
		var student = await CreateExistingStudentAsync(offering, "AppAccess", "Student");
		await GrantCompletedRecordAsync(student);
		var service = GetRequiredService<IStudentRecordAppAccessAppService>();
		var initial = await service.GetAsync(student.Id);
		initial.Recipients.Single().Status.ShouldBe("not-set-up");
		initial.Recipients.Single().CanUpdate.ShouldBeFalse();
		initial.History.ShouldBeEmpty();

		var fatherId = await AddAppAccessRecipientAsync(student.Id, "guardian", "father");
		await AddAppAccessRecipientAsync(student.Id, "student", "self");
		await WithUnitOfWorkAsync(async () =>
		{
			var users = GetRequiredService<IRepository<IdentityUser, Guid>>();
			var actor = await users.GetAsync(TestUserId);
			actor.Name = "Records Officer";
			await users.UpdateAsync(actor, autoSave: true);
		});
		var ready = await service.GetAsync(student.Id);
		var father = ready.Recipients.Single(r => r.Relationship == "father");
		father.Status.ShouldBe("on");
		father.CanUpdate.ShouldBeTrue();
		var unchanged = await service.UpdateAsync(student.Id, father.RecipientKey, new()
			{ Enabled = true, Reason = "Confirm existing access", ConcurrencyStamp = father.ConcurrencyStamp });
		unchanged.History.ShouldBeEmpty();
		unchanged.Recipients.Single(r => r.Relationship == "father").ConcurrencyStamp.ShouldBe(father.ConcurrencyStamp);

		var off = await service.UpdateAsync(student.Id, father.RecipientKey, new()
			{ Enabled = false, Reason = "  Parent requested a pause  ", ConcurrencyStamp = father.ConcurrencyStamp });
		off.Recipients.Single(r => r.Relationship == "father").Status.ShouldBe("off");
		off.Recipients.Single(r => r.Relationship == "self").Status.ShouldBe("on");
		var audit = off.History.Single();
		audit.Reason.ShouldBe("Parent requested a pause");
		audit.Relationship.ShouldBe("father");
		audit.Enabled.ShouldBeFalse();
		audit.ChangedByName.ShouldBe("Records Officer");
		(await Should.ThrowAsync<BusinessException>(() => service.UpdateAsync(student.Id, father.RecipientKey, new()
			{ Enabled = true, Reason = "Stale state", ConcurrencyStamp = father.ConcurrencyStamp }))).Code.ShouldBe("Students:Records:AppAccessConflict");

		var paused = off.Recipients.Single(r => r.Relationship == "father");
		var on = await service.UpdateAsync(student.Id, paused.RecipientKey, new()
			{ Enabled = true, Reason = "Resume access", ConcurrencyStamp = paused.ConcurrencyStamp });
		on.Recipients.Single(r => r.Relationship == "father").Status.ShouldBe("on");
		on.History.Count.ShouldBe(2);
		on.History.Count(entry => entry.Enabled).ShouldBe(1);
		await WithUnitOfWorkAsync(async () =>
		{
			var links = GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>();
			(await links.FindAsync(link => link.StudentId == student.Id && link.UserId == fatherId)).ShouldNotBeNull();
		});
	});

	[Fact]
	public Task App_access_rejects_bad_reasons_unlinked_accounts_and_ineligible_records() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateCertificateStudentOfferingAsync();
		var student = await CreateExistingStudentAsync(offering, "AccessValidation", "Student");
		var service = GetRequiredService<IStudentRecordAppAccessAppService>();
		(await Should.ThrowAsync<BusinessException>(() => service.GetAsync(student.Id))).Code.ShouldBe("Students:Records:OnboardingIncomplete");
		await GrantCompletedRecordAsync(student);
		await AddAppAccessRecipientAsync(student.Id, "guardian", "guardian");
		var initial = await service.GetAsync(student.Id);
		var self = initial.Recipients.Single();
		(await Should.ThrowAsync<BusinessException>(() => service.UpdateAsync(student.Id, self.RecipientKey, new()
			{ Enabled = false, Reason = "Valid reason", ConcurrencyStamp = self.ConcurrencyStamp }))).Code.ShouldBe("Students:Records:AppAccessRecipientUnavailable");

		var selfId = await AddAppAccessRecipientAsync(student.Id, "student", "self");
		var ready = (await service.GetAsync(student.Id)).Recipients.Single();
		foreach (var reason in new[] { "  a ", new string('a', 401) })
			(await Should.ThrowAsync<BusinessException>(() => service.UpdateAsync(student.Id, ready.RecipientKey, new()
				{ Enabled = false, Reason = reason, ConcurrencyStamp = ready.ConcurrencyStamp }))).Code.ShouldBe("Students:Records:AppAccessReasonInvalid");
		await WithUnitOfWorkAsync(async () =>
		{
			var users = GetRequiredService<IRepository<IdentityUser, Guid>>();
			var identity = await users.GetAsync(selfId);
			identity.SetIsActive(false);
			await users.UpdateAsync(identity, autoSave: true);
		});
		var inactive = (await service.GetAsync(student.Id)).Recipients.Single();
		inactive.Status.ShouldBe("not-set-up");
		inactive.CanUpdate.ShouldBeFalse();
		(await Should.ThrowAsync<BusinessException>(() => service.UpdateAsync(student.Id, inactive.RecipientKey, new()
			{ Enabled = true, Reason = "Restore access", ConcurrencyStamp = inactive.ConcurrencyStamp }))).Code.ShouldBe("Students:Records:AppAccessRecipientUnavailable");
		(await service.GetAsync(student.Id)).History.ShouldBeEmpty();
	});

	[Fact]
	public Task App_access_self_restriction_survives_relink_without_restoring_revoked_legacy_identity() => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateCertificateStudentOfferingAsync();
		var student = await CreateExistingStudentAsync(offering, "AccessRelink", "Student");
		await GrantCompletedRecordAsync(student);
		var legacyId = Guid.NewGuid();
		await EnsureTestIdentityUserAsync(legacyId, $"legacy-{legacyId:N}");
		await WithUnitOfWorkAsync(async () =>
		{
			var row = await _studentRepository.GetAsync(student.Id);
			row.LinkUser(legacyId);
			await _studentRepository.UpdateAsync(row, autoSave: true);
		});
		var service = GetRequiredService<IStudentRecordAppAccessAppService>();
		var ready = (await service.GetAsync(student.Id)).Recipients.Single();
		ready.Status.ShouldBe("on");
		await service.UpdateAsync(student.Id, ready.RecipientKey, new()
			{ Enabled = false, Reason = "Pause student access", ConcurrencyStamp = ready.ConcurrencyStamp });
		await WithUnitOfWorkAsync(async () =>
		{
			var links = GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>();
			var link = await links.InsertAsync(new StudentPortalAccountLink(Guid.NewGuid(), TestTenantId, student.Id,
				legacyId, "student", "self", TestUserId), autoSave: true);
			await links.DeleteAsync(link, autoSave: true);
		});
		var unavailable = (await service.GetAsync(student.Id)).Recipients.Single();
		unavailable.Status.ShouldBe("not-set-up");
		unavailable.CanUpdate.ShouldBeFalse();
		await AddAppAccessRecipientAsync(student.Id, "student", "self");
		var relinked = await service.GetAsync(student.Id);
		relinked.Recipients.Single().Status.ShouldBe("off");
		relinked.Recipients.Single().ConcurrencyStamp.ShouldNotBe(unavailable.ConcurrencyStamp);
		relinked.History.Count.ShouldBe(1);
	});

	[Theory]
	[InlineData("Identity")]
	[InlineData("Guardian")]
	public Task App_access_enforces_record_masks(string mask) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateCertificateStudentOfferingAsync();
		var student = await CreateExistingStudentAsync(offering, "MaskedAccess", "Student");
		await GrantCompletedRecordAsync(student);
		await AddAppAccessRecipientAsync(student.Id, "guardian", "mother");
		var service = GetRequiredService<IStudentRecordAppAccessAppService>();
		var parent = (await service.GetAsync(student.Id)).Recipients.Single(r => r.Relationship == "mother");
		var paused = await service.UpdateAsync(student.Id, parent.RecipientKey, new()
			{ Enabled = false, Reason = "Pause requested", ConcurrencyStamp = parent.ConcurrencyStamp });
		var freshParent = paused.Recipients.Single(r => r.Relationship == "mother");
		var policy = StudentRecordsPolicy.Starter();
		policy.Access.Masks.Add(new() { FieldGroup = mask, AllowedRoleNames = [] });
		await PublishDirectoryPolicyAsync(policy);
		var denied = await Should.ThrowAsync<BusinessException>(() => service.UpdateAsync(student.Id, freshParent.RecipientKey, new()
			{ Enabled = true, Reason = "Restore requested", ConcurrencyStamp = freshParent.ConcurrencyStamp }));
		if (mask == "Identity")
		{
			denied.Code.ShouldBe(StudentRecordsErrorCodes.AppAccessIdentityRestricted);
			(await Should.ThrowAsync<BusinessException>(() => service.GetAsync(student.Id))).Code.ShouldBe(StudentRecordsErrorCodes.AppAccessIdentityRestricted);
		}
		else
		{
			denied.Code.ShouldBe(StudentRecordsErrorCodes.AppAccessConflict);
			var visible = await service.GetAsync(student.Id);
			visible.Recipients.Single().Relationship.ShouldBe("self");
			visible.History.ShouldBeEmpty();
		}
	});

	private async Task<Guid> AddAppAccessRecipientAsync(Guid studentId, string accountType, string relationship)
	{
		var userId = Guid.NewGuid();
		await EnsureTestIdentityUserAsync(userId, $"access-{relationship}-{userId:N}");
		await WithUnitOfWorkAsync(() => GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>().InsertAsync(
			new StudentPortalAccountLink(Guid.NewGuid(), TestTenantId, studentId, userId, accountType, relationship, TestUserId), autoSave: true));
		return userId;
	}
}
