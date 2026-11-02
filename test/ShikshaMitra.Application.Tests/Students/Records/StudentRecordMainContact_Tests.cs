using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.Students.ValueObjects;
using ShikshaMitra.Students.Enrollments.Registrations;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(StudentLifeCycleStatusEnum.RegistrationFormIssued)]
	[InlineData(StudentLifeCycleStatusEnum.Registration)]
	[InlineData(StudentLifeCycleStatusEnum.EntranceExam)]
	[InlineData(StudentLifeCycleStatusEnum.Admission)]
	public Task Registration_family_write_requires_guardian_acknowledgment_and_preserves_saved_guardian(StudentLifeCycleStatusEnum status) => RunCertificationScenarioAsync(async () =>
	{
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "FamilyGuardian", "Student");
		await WithUnitOfWorkAsync(async () =>
		{
			var current = await _studentRepository.GetAsync(student.Id);
			current.ChangeStatus(status);
			await _studentRepository.UpdateAsync(current, true);
		});
		var service = GetRequiredService<IStudentFamilyInfoAppService>();
		var input = new UpdateFamilyInfoDto { GuardianRelationship = "Aunt", GuardianDetails = new() { FullName = "Saved Aunt", Email = "aunt@example.test" } };
		(await Should.ThrowAsync<BusinessException>(() => service.UpdateFamilyInfoAsync(student.Id, input)))
			.Code.ShouldBe("Students:Family:GuardianResponsibilityConfirmationRequired");
		input.MainContactRelationship = "guardian";
		(await Should.ThrowAsync<BusinessException>(() => service.UpdateFamilyInfoAsync(student.Id, input)))
			.Code.ShouldBe("Students:Family:GuardianResponsibilityConfirmationRequired");
		input.GuardianResponsibilityConfirmed = true;
		(await service.UpdateFamilyInfoAsync(student.Id, input)).MainContactRelationship.ShouldBe("guardian");
		input.GuardianResponsibilityConfirmed = null;
		input.GuardianDetails.FullName = "Corrected Aunt";
		(await service.UpdateFamilyInfoAsync(student.Id, input)).GuardianDetails!.FullName.ShouldBe("Corrected Aunt");
	});

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public Task Record_main_contact_and_optional_guardian_preserve_parent_account_password_and_sibling_links(bool requiresApproval) => RunCertificationScenarioAsync(async () =>
	{
		await EnsureTestIdentityUserAsync(TestUserId, "main-contact-requester");
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "MainContact", "Student");
		var sibling = await CreateExistingStudentAsync(offering, "MainContactSibling", "Student");
		await GrantCompletedRecordAsync(student);
		await GrantCompletedRecordAsync(sibling);
		var parentId = Guid.NewGuid();
		await EnsureTestIdentityUserAsync(parentId, "preserved-parent");
		var links = GetRequiredService<IRepository<StudentPortalAccountLink, Guid>>();
		string? originalPasswordHash = null;
		await WithUnitOfWorkAsync(async () =>
		{
			var parent = await GetRequiredService<IRepository<IdentityUser, Guid>>().GetAsync(parentId);
			(await GetRequiredService<IdentityUserManager>().AddPasswordAsync(parent, "PreservedParent1!")).Succeeded.ShouldBeTrue();
			originalPasswordHash = parent.PasswordHash.ShouldNotBeNull();
			var current = (await _studentRepository.GetWithDetailsAsync(student.Id))!;
			current.UpdateFamilyInfo(new ParentGuardianDetails("Rajesh Sharma", null, "Teacher", parent.Email, "9876543210", null, null, null, null, null, null, null, null), null, null, [sibling.Id]);
			StudentFamilyContactSelection.Select(current, "father");
			await _studentRepository.UpdateAsync(current, true);
			await links.InsertAsync(new(Guid.NewGuid(), TestTenantId, student.Id, parentId, "guardian", "father", TestUserId), true);
			await links.InsertAsync(new(Guid.NewGuid(), TestTenantId, sibling.Id, parentId, "guardian", "father", TestUserId), true);
		});
		var originalLinks = await WithUnitOfWorkAsync(() => links.GetListAsync(link => link.UserId == parentId));
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Guardian"].Mode = requiresApproval ? "Approval" : "Direct";
		if (requiresApproval) policy.Changes["Guardian"].Stages = [new() { RoleNames = ["GuardianContactReviewer"] }];
		await PublishDirectoryPolicyAsync(policy);
		var changes = GetRequiredService<IStudentRecordChangeAppService>();
		var snapshot = await changes.GetSnapshotAsync(student.Id, "family");
		var payload = JsonNode.Parse(snapshot.Data.GetRawText())!.AsObject();
		payload["guardianDetails"] = new JsonObject { ["fullName"] = "Anita Sharma", ["email"] = "aunt@example.com", ["contactNo"] = "9876543212" };
		payload["guardianRelationship"] = "Aunt";
		payload["mainContactRelationship"] = "guardian";
		// The legacy snapshot omits the sibling IDs; a family contact edit must preserve them.
		payload.Remove("siblingStudentIds");
		var input = new CreateStudentRecordChangeDto
		{
			StudentId = student.Id, Section = "family", ClientKey = Guid.NewGuid().ToString("N"),
			Reason = "Family confirmed the aunt as the main school contact; existing parent app access remains", ExpectedSnapshot = snapshot.Snapshot,
			Payload = JsonSerializer.SerializeToElement(payload)
		};
		(await Should.ThrowAsync<BusinessException>(() => changes.CreateAsync(input)))
			.Code.ShouldBe("Students:Family:GuardianResponsibilityConfirmationRequired");
		payload["guardianResponsibilityConfirmed"] = true;
		input.Payload = JsonSerializer.SerializeToElement(payload);
		var result = await changes.CreateAsync(input);
		result.Proposed!.Value.GetProperty("guardianResponsibilityConfirmed").GetBoolean().ShouldBeTrue();
		var appliedBy = TestUserId;
		if (requiresApproval)
		{
			result.Status.ShouldBe("Pending");
			var reviewer = Guid.NewGuid();
			await EnsureTestIdentityUserAsync(reviewer, "guardian-responsibility-reviewer");
			await WithUnitOfWorkAsync(async () =>
			{
				await _scopeRepository.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TestTenantId, userId: reviewer), true);
				var roles = GetRequiredService<IdentityRoleManager>();
				if (!await roles.RoleExistsAsync("GuardianContactReviewer"))
					(await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), "GuardianContactReviewer", TestTenantId))).Succeeded.ShouldBeTrue();
				var users = GetRequiredService<IdentityUserManager>();
				(await users.AddToRoleAsync((await users.FindByIdAsync(reviewer.ToString()))!, "GuardianContactReviewer")).Succeeded.ShouldBeTrue();
			});
			using (_principalAccessor.Change(ProfilePrincipal(reviewer)))
				result = await changes.ApproveAsync(result.Id, new() { ConcurrencyStamp = result.ConcurrencyStamp, Reason = "Both parents unavailable for school contact; guardian responsibility verified" });
			appliedBy = reviewer;
		}
		result.Status.ShouldBe("Applied");
		var saved = await changes.GetSnapshotAsync(student.Id, "family");
		saved.Data.GetProperty("mainContactRelationship").GetString().ShouldBe("guardian");
		saved.Data.GetProperty("guardianRelationship").GetString().ShouldBe("Aunt");
		(await GetRequiredService<IStudentGuardianAccessAppService>().GetReviewsAsync(student.Id)).ShouldBeEmpty();
		await WithUnitOfWorkAsync(async () =>
		{
			var current = (await _studentRepository.GetWithDetailsAsync(student.Id))!;
			current.FamilyDetail!.SiblingStudentIds.ShouldBe([sibling.Id]);
			current.GetProperty<string>("SchoolContactGuardianResponsibilityConfirmedBy").ShouldBe(appliedBy.ToString("D"));
			current.GetProperty<string>("SchoolContactGuardianResponsibilityConfirmedAt").ShouldNotBeNullOrWhiteSpace();
			current.FamilyDetail.FatherDetails!.Occupation.ShouldBe("Teacher");
			var remaining = await links.GetListAsync(link => link.UserId == parentId);
			remaining.Select(link => link.Id).OrderBy(id => id).ShouldBe(originalLinks.Select(link => link.Id).OrderBy(id => id));
			(await GetRequiredService<IRepository<IdentityUser, Guid>>().GetAsync(parentId)).PasswordHash.ShouldBe(originalPasswordHash);
		});
	});
}
