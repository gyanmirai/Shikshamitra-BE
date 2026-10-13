using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Reserved_assessment_facts_require_evidence_only_in_managed_context(bool managed)
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var values = new Dictionary<string, string> { ["ASSESSMENT.OUTCOME"] = "legacy-form-value" };
		await WithUnitOfWorkAsync(() => GetRequiredService<AdmissionAssessmentRuntimeService>()
			.AddFactsAsync(Guid.NewGuid(), values, managed ? Guid.NewGuid() : null));
		values.ContainsKey("ASSESSMENT.OUTCOME").ShouldBe(!managed);
	}

	[Fact]
	public async Task Scheduled_assessment_checks_capacity_future_actions_cancellation_and_stale_stamps()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		using var principal = _principalAccessor.Change(BuildAdminPrincipal());
		var (application, stageId, offering) = await CreateManagedAssessmentApplicationAsync(scheduling: true);
		var service = GetRequiredService<IAdmissionAssessmentAppService>();
		var runtime = await _applicationAppService.GetWorkflowAsync(application.Id);
		var definitionId = runtime.Stages.Single(item => item.Id == stageId).DefinitionStageId;
		var session = await service.CreateSessionAsync(new()
		{
			AdmissionOfferingId = offering.Id, WorkflowStageDefinitionId = definitionId, Name = "Entrance exam",
			StartsAt = DateTime.UtcNow.AddDays(2), EndsAt = DateTime.UtcNow.AddDays(2).AddHours(1), Location = "Room A", Capacity = 1
		});
		var summary = await service.GetAsync(application.Id, stageId);
		var booked = await service.BookAsync(application.Id, stageId, new() { SessionId = session.Id, ConcurrencyStamp = summary.ConcurrencyStamp });
		booked.CanRecordAttendance.ShouldBeFalse();
		booked.CanRecordResult.ShouldBeFalse();
		var stale = await Should.ThrowAsync<BusinessException>(() => service.BookAsync(application.Id, stageId,
			new() { SessionId = session.Id, ConcurrencyStamp = summary.ConcurrencyStamp }));
		stale.Code.ShouldBe(AdmissionAssessmentErrorCodes.ConcurrencyConflict);
		var currentSession = (await service.GetSessionsAsync(offering.Id, definitionId)).Single();
		currentSession.BookedCount.ShouldBe(1);
		await Should.ThrowAsync<BusinessException>(() => service.CancelSessionAsync(session.Id, new() { ConcurrencyStamp = currentSession.ConcurrencyStamp }));
		var other = await CreateApplicantAsync("Second assessment applicant");
			var otherApplication = await StartFixtureApplicationAsync(other.Id, offering.Id);
		var otherRuntime = await _applicationAppService.GetWorkflowAsync(otherApplication.Id);
		var otherSummary = await service.GetAsync(otherApplication.Id, otherRuntime.CurrentStageInstanceId!.Value);
		var full = await Should.ThrowAsync<BusinessException>(() => service.BookAsync(otherApplication.Id, otherSummary.StageInstanceId,
			new() { SessionId = session.Id, ConcurrencyStamp = otherSummary.ConcurrencyStamp }));
		full.Code.ShouldBe(AdmissionAssessmentErrorCodes.CapacityExceeded);
		var attempt = booked.Attempts.Single();
		await service.CancelAttemptAsync(attempt.Id, new() { ConcurrencyStamp = attempt.ConcurrencyStamp, Notes = "Reschedule" });
		(await service.GetSessionsAsync(offering.Id, definitionId)).Single().BookedCount.ShouldBe(0);
		var otherBooking = await service.BookAsync(otherApplication.Id, otherSummary.StageInstanceId,
			new() { SessionId = session.Id, ConcurrencyStamp = otherSummary.ConcurrencyStamp });
		otherBooking.Attempts.Single().SessionId.ShouldBe(session.Id);
	}
}
