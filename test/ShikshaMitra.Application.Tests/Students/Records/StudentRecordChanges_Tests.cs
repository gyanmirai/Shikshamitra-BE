using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using ShikshaMitra.Students.Records;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	private async Task<(Student Student, IStudentRecordChangeAppService Service, CreateStudentRecordChangeDto Input)> RecordContactFixtureAsync()
	{
		await EnsureTestIdentityUserAsync(TestUserId, "records-requester");
		var offering = await CreateAvailableOfferingAsync((await CreateAndPublishWorkflowAsync()).Id);
		var student = await CreateExistingStudentAsync(offering, "RecordChange", "Student");
		await GrantCompletedRecordAsync(student);
		var service = GetRequiredService<IStudentRecordChangeAppService>();
		var snapshot = await service.GetSnapshotAsync(student.Id, "communication");
		var payload = JsonNode.Parse(snapshot.Data.GetRawText())!.AsObject(); payload["emergencyContactName"] = "Verified guardian";
		return (student, service, new() { StudentId = student.Id, Section = "communication", Reason = "Guardian verified corrected contact",
			ClientKey = Guid.NewGuid().ToString("N"), ExpectedSnapshot = snapshot.Snapshot, Payload = JsonSerializer.SerializeToElement(payload) });
	}
	[Fact]
	public Task Record_direct_contact_change_applies_once_and_retains_before_after_audit() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordContactFixtureAsync();
		var result = await fixture.Service.CreateAsync(fixture.Input);
		result.Status.ShouldBe("Applied"); result.Before.ShouldNotBeNull(); result.Proposed.ShouldNotBeNull();
		result.Proposed.Value.GetProperty("emergencyContactName").GetString().ShouldBe("Verified guardian");
		(await fixture.Service.CreateAsync(fixture.Input)).Id.ShouldBe(result.Id);
		(await fixture.Service.GetSnapshotAsync(fixture.Student.Id, "communication")).Data.GetProperty("emergencyContactName").GetString().ShouldBe("Verified guardian");
		fixture.Input.Reason = "Another reason with same request key";
		(await Should.ThrowAsync<BusinessException>(() => fixture.Service.CreateAsync(fixture.Input))).Code.ShouldBe("Students:Records:IdempotencyConflict");
	});
	[Fact]
	public Task Record_stale_snapshot_cannot_overwrite_a_newer_contact_change() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordContactFixtureAsync();
		await fixture.Service.CreateAsync(fixture.Input);
		fixture.Input.ClientKey = Guid.NewGuid().ToString("N");
		(await Should.ThrowAsync<BusinessException>(() => fixture.Service.CreateAsync(fixture.Input))).Code.ShouldBe("Students:Records:StaleSnapshot");
	});
	[Fact]
	public Task Record_unknown_payload_fields_are_rejected_before_creating_a_request() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordContactFixtureAsync();
		var payload = JsonNode.Parse(fixture.Input.Payload.GetRawText())!.AsObject(); payload["studentId"] = Guid.NewGuid().ToString();
		fixture.Input.Payload = JsonSerializer.SerializeToElement(payload);
		(await Should.ThrowAsync<BusinessException>(() => fixture.Service.CreateAsync(fixture.Input))).Code.ShouldBe("Students:Records:InvalidPayload");
	});
	[Fact]
	public Task Record_future_change_is_scheduled_and_can_be_cancelled_without_changing_contact() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordContactFixtureAsync(); fixture.Input.EffectiveAt = DateTimeOffset.UtcNow.AddDays(1);
		var result = await fixture.Service.CreateAsync(fixture.Input); result.Status.ShouldBe("Scheduled");
		(await fixture.Service.GetSnapshotAsync(fixture.Student.Id, "communication")).Snapshot.ShouldBe(fixture.Input.ExpectedSnapshot);
		result = await fixture.Service.CancelAsync(result.Id, new() { Reason = "Cancelled future contact", ConcurrencyStamp = result.ConcurrencyStamp });
		result.Status.ShouldBe("Cancelled");
		(await fixture.Service.GetSnapshotAsync(fixture.Student.Id, "communication")).Snapshot.ShouldBe(fixture.Input.ExpectedSnapshot);
	});
	[Fact]
	public Task Record_request_uses_original_approval_policy_and_self_approval_is_denied() => RunCertificationScenarioAsync(async () =>
	{
		var fixture = await RecordContactFixtureAsync();
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Contact"].Mode = "Approval";
		policy.Changes["Contact"].Stages = [new() { RoleNames = ["admin"] }];
		await PublishDirectoryPolicyAsync(policy);
		var result = await fixture.Service.CreateAsync(fixture.Input);
		result.Status.ShouldBe("Pending"); result.RequiredApprovals.ShouldBe(1); result.PolicyVersionId.ShouldNotBeNull();
		(await Should.ThrowAsync<BusinessException>(() => fixture.Service.ApproveAsync(result.Id, new() { ConcurrencyStamp = result.ConcurrencyStamp, Reason = "Self review" })))
			.Code.ShouldBe("Students:Records:ApproverRequired");
		(await fixture.Service.GetSnapshotAsync(fixture.Student.Id, "communication")).Snapshot.ShouldBe(fixture.Input.ExpectedSnapshot);
	});
}
