using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public async Task Selected_booking_preview_returns_only_requested_authorized_students_and_preserves_confirmation()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var first = await CreateManagedAssessmentApplicationAsync(scheduling: true);
		var second = await CreateManagedAssessmentApplicationAsync(scheduling: true);
		// The same pupil can apply to a second Offering. The explicitly selected application must win over a global suggestion.
		var selectedApplication = await StartFixtureApplicationAsync(first.Application.ApplicantId, second.Offering.Id);
		var selectedWorkflow = await _applicationAppService.GetWorkflowAsync(selectedApplication.Id);
		var selectedStageId = selectedWorkflow.CurrentStageInstanceId!.Value;
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var scopes = await service.GetScopesAsync();
		var room = await CreateAssessmentPlanningRoomAsync();
		var input = new AdmissionAssessmentSessionInput {
			Name = "Selected students", RoomId = room.Id, Capacity = 24,
			StartsAt = DateTimeOffset.UtcNow.AddDays(30), EndsAt = DateTimeOffset.UtcNow.AddDays(30).AddHours(1),
			Scopes = [new() { AdmissionOfferingId = first.Offering.Id, WorkflowStageDefinitionId = scopes.First(item => item.AdmissionOfferingId == first.Offering.Id).WorkflowStageDefinitionId },
				new() { AdmissionOfferingId = second.Offering.Id, WorkflowStageDefinitionId = scopes.First(item => item.AdmissionOfferingId == second.Offering.Id).WorkflowStageDefinitionId }]
		};
		// JSON exercises the additive wire contract while allowing the pre-change server to show the failure.
		var request = JsonSerializer.Deserialize<AdmissionAssessmentSessionPreviewInput>(JsonSerializer.Serialize(new { Session = input, StageInstanceIds = new[] { selectedStageId }, MaxResultCount = 100 }))!;
		var preview = await service.PreviewSessionAsync(request);
		preview.Items.Count.ShouldBe(1);
		preview.Items.Single().ApplicationId.ShouldBe(selectedApplication.Id);
		preview.ProposedBookings.Single().ApplicationId.ShouldBe(selectedApplication.Id);
		preview.AvailableCount.ShouldBe(24);
		var unrelated = JsonSerializer.Deserialize<AdmissionAssessmentSessionPreviewInput>(JsonSerializer.Serialize(new { Session = input, StageInstanceIds = new[] { Guid.NewGuid() }, MaxResultCount = 100 }))!;
		(await service.PreviewSessionAsync(unrelated)).Items.ShouldBeEmpty();
		var saved = await service.ConfirmSessionAsync(new() { Session = input, PreviewFingerprint = preview.PreviewFingerprint, IdempotencyKey = Guid.NewGuid(), Bookings = preview.Items.Select(item => new AdmissionAssessmentSelectedBookingInput { ApplicationId = item.ApplicationId, StageInstanceId = item.StageInstanceId, ConcurrencyStamp = item.ConcurrencyStamp }).ToList() });
		saved.BookedCount.ShouldBe(1);
		(await service.GetAsync(first.Application.Id, first.StageId)).Attempts.ShouldBeEmpty();
		(await service.GetAsync(second.Application.Id, second.StageId)).Attempts.ShouldBeEmpty();
		(await service.GetAsync(selectedApplication.Id, selectedStageId)).Attempts.Count.ShouldBe(1);
	}
}

