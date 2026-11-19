using System;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Duplicate_review_normalizes_pair_and_does_not_allow_requester_to_confirm() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordContactFixtureAsync();
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var other = await CreateExistingStudentAsync(offering, "PossibleDuplicate", "Student");
		await GrantCompletedRecordAsync(other);
		var service = GetRequiredService<IStudentRecordDuplicateAppService>();
		var review = await service.CreateAsync(new() { StudentId = fixture.Student.Id, OtherStudentId = other.Id, Reason = "Matching school admission documents" });
		review.Status.ShouldBe("Open"); review.CanReview.ShouldBeFalse();
		(await service.CreateAsync(new() { StudentId = other.Id, OtherStudentId = fixture.Student.Id, Reason = "Same students in reverse order" })).Id.ShouldBe(review.Id);
		(await Should.ThrowAsync<BusinessException>(() => service.ResolveAsync(review.Id, new() { ConcurrencyStamp = review.ConcurrencyStamp,
			ConfirmedDuplicate = true, Reason = "Requester's own confirmation" }))).Code.ShouldBe("Students:Records:SelfApproval");
		(await GetRequiredService<IStudentDirectoryAppService>().GetListAsync(new() { StudentId = fixture.Student.Id })).TotalCount.ShouldBe(1);
		(await GetRequiredService<IStudentDirectoryAppService>().GetListAsync(new() { StudentId = other.Id })).TotalCount.ShouldBe(1);
	});

	[Fact]
	public Task Duplicate_review_rechecks_current_scope_and_masks_existing_review_content() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordContactFixtureAsync();
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var other = await CreateExistingStudentAsync(offering, "RestrictedDuplicate", "Student"); await GrantCompletedRecordAsync(other);
		var service = GetRequiredService<IStudentRecordDuplicateAppService>();
		await service.CreateAsync(new() { StudentId = fixture.Student.Id, OtherStudentId = other.Id, Reason = "Private guardian supporting evidence" });
		var unassignedStaffId = Guid.NewGuid();
		await EnsureTestIdentityUserAsync(unassignedStaffId, $"duplicate-unassigned-{unassignedStaffId:N}");
		using (_principalAccessor.Change(ProfilePrincipal(unassignedStaffId)))
			(await service.GetListAsync(new() { StudentId = fixture.Student.Id })).TotalCount.ShouldBe(0);
		var policy = StudentRecordsPolicy.Starter(); policy.Access.Masks.Add(new() { FieldGroup = "Guardian", AllowedRoleNames = [] });
		await PublishDirectoryPolicyAsync(policy);
		var restricted = (await service.GetListAsync(new() { StudentId = fixture.Student.Id })).Items[0];
		restricted.StudentName.ShouldBe("Restricted student"); restricted.OtherStudentName.ShouldBe("Restricted student");
		restricted.Reason.ShouldBe("Restricted"); restricted.CanReview.ShouldBeFalse();
		(await Should.ThrowAsync<BusinessException>(() => service.ResolveAsync(restricted.Id, new() { ConcurrencyStamp = restricted.ConcurrencyStamp,
			Reason = "Cannot act on restricted evidence", ConfirmedDuplicate = false }))).Code.ShouldBe("Students:Records:RestrictedFields");
	});
}
