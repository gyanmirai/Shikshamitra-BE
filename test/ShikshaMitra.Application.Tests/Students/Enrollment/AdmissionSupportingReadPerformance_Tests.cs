using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Inquiry_capture_context_shares_one_fresh_capacity_and_configuration_result() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			await WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
				var result = await GetRequiredService<IAdmissionInquiryCaptureAppService>().GetContextAsync(offering.Id);
				result.Offering.Id.ShouldBe(offering.Id);
				result.Availability.CanCreateInquiry.ShouldBeTrue();
				result.Form!.Id.ShouldBe(offering.InquiryFormDefinitionId!.Value);
				result.Offering.ClassCapacity.ShouldNotBeNull();
				JsonSerializer.Serialize(result.Availability.ClassCapacity).ShouldBe(JsonSerializer.Serialize(result.Offering.ClassCapacity));
				var reads = DirectReadCount(capture, "AppAdmissionClassRoomAllocations");
				Console.WriteLine($"SQLite capture context capacity reads={reads}; all reads={capture.ReadCount}.");
				reads.ShouldBe(1, "One scoped capture request must share its fresh offering validation/capacity instead of rebuilding it three times.");
			});
		});

	[Fact]
	public Task Public_inquiry_selected_form_reuses_its_already_evaluated_choice_availability() =>
		RunCertificationScenarioAsync(async () =>
		{
			var offering = await CreateOptionalIntakeOfferingAsync();
			var service = GetRequiredService<IAdmissionPublicInquiryAppService>();
			var link = await service.CreateLinkAsync(new()
			{
				AdmissionCycleId = offering.AdmissionCycleId, InquiryFormDefinitionId = offering.InquiryFormDefinitionId,
				Name = "Public selected read", Source = AdmissionInquirySource.Website
			});
			using (_currentTenant.Change(null))
			using (_principalAccessor.Change(new ClaimsPrincipal(new ClaimsIdentity())))
			await WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
				var result = await service.GetPublicAsync(link.Token, offering.Id);
				result.Offerings.Single().Id.ShouldBe(offering.Id);
				result.Offerings.Single().CanCreateInquiry.ShouldBeTrue();
				result.Form!.Id.ShouldBe(offering.InquiryFormDefinitionId!.Value);
				var reads = DirectReadCount(capture, "AppAdmissionClassRoomAllocations");
				Console.WriteLine($"SQLite public selected choice capacity reads={reads}; all reads={capture.ReadCount}.");
				reads.ShouldBe(1, "Selecting the one already evaluated public choice must not validate/count its live availability twice.");
			});
		});

	[Fact]
	public Task Assessment_bulk_cancellation_preserves_each_mutation_without_discarded_workspace_reads() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixtures = new List<(AdmissionApplicationDto Application, Guid StageId, AdmissionOffering Offering)>();
			for (var index = 0; index < 2; index++) fixtures.Add(await CreateManagedAssessmentApplicationAsync(scheduling: true));
			var service = GetRequiredService<IAdmissionAssessmentAppService>();
			var room = await CreateAssessmentPlanningRoomAsync();
			var scopes = await service.GetScopesAsync();
			var session = new AdmissionAssessmentSessionInput
			{
				Name = "Batch cancel read", RoomId = room.Id, Capacity = 2,
				StartsAt = DateTimeOffset.UtcNow.AddDays(30).Date.AddHours(3), EndsAt = DateTimeOffset.UtcNow.AddDays(30).Date.AddHours(4),
				Scopes = fixtures.Select(fixture => new AdmissionAssessmentSessionScopeInput { AdmissionOfferingId = fixture.Offering.Id,
					WorkflowStageDefinitionId = scopes.Single(item => item.AdmissionOfferingId == fixture.Offering.Id).WorkflowStageDefinitionId }).ToList()
			};
			var preview = await service.PreviewSessionAsync(new() { Session = session });
			var confirmed = await service.ConfirmSessionAsync(new()
			{
				Session = session, IdempotencyKey = Guid.NewGuid(), PreviewFingerprint = preview.PreviewFingerprint,
				Bookings = preview.ProposedBookings.Select(item => new AdmissionAssessmentSelectedBookingInput { ApplicationId = item.ApplicationId,
					StageInstanceId = item.StageInstanceId, ConcurrencyStamp = item.ConcurrencyStamp }).ToList()
			});
			var current = await service.GetSessionAsync(confirmed.Session.Id);
			await WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
				var result = await service.CancelSessionAsync(current.Id, new() { ConcurrencyStamp = current.ConcurrencyStamp,
					CancelPendingBookings = true, Notes = "Read regression cancellation" });
				result.IsCancelled.ShouldBeTrue();
				DirectReadCount(capture, "AppAdmissionApplicationStageInstances").ShouldBe(2,
					"Each cancellation needs its original fresh stage authorization, but no discarded response context.");
			});
			await WithUnitOfWorkAsync(async () =>
			{
				(await GetRequiredService<IRepository<AdmissionAssessmentAttempt, Guid>>().GetListAsync(item => item.SessionId == current.Id))
					.ShouldAllBe(attempt => attempt.Status == "cancelled");
				(await GetRequiredService<IRepository<AdmissionActivity, Guid>>().GetListAsync(item => item.ActivityType == "AssessmentAttemptCancelled"))
					.Count.ShouldBe(2);
			});
		});

	[Theory]
	[InlineData("finalization")]
	[InlineData("snapshot")]
	[InlineData("readiness")]
	public Task Document_supporting_reads_batch_visited_stage_policies_and_evaluations(string operation) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync($"DocumentSupport{operation}");
			var finalized = await FinalizeAsync(fixture.Application, true, $"document-support-{operation}");
			var snapshots = await SeedSupportingDocumentStagesAsync(fixture.Application.Id);
			await WithUnitOfWorkAsync(async () =>
			{
				var application = await _applicationRepository.GetAsync(fixture.Application.Id);
				var item = await _onboardingCaseRepository.GetAsync(finalized.OnboardingCaseId);
				item.ExtraProperties["DocumentPoliciesSnapshot"] = JsonSerializer.Serialize(snapshots);
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
				var reader = GetRequiredService<OnboardingRequirementSnapshotService>();
				if (operation == "finalization")
					await GetRequiredService<AdmissionDocumentRuntimeService>().EnsureApplicationCompleteAsync(application);
				else if (operation == "snapshot")
				{
					var result = JsonSerializer.Deserialize<List<OnboardingDocumentPolicySnapshot>>(await reader.PrepareDocumentPoliciesAsync(application))!;
					result.Select(snapshot => snapshot.StageId).ShouldBe(snapshots.Select(snapshot => snapshot.StageId));
					result.ShouldAllBe(snapshot => !snapshot.Policy.AllowWaivers && snapshot.Policy.Requirements.Single().Requirement == "optional" &&
						snapshot.Policy.Requirements.Single().ConditionJson == null && snapshot.Policy.Requirements.Single().StandardIds == null);
				}
				else (await reader.DocumentsReadyAsync(item, application)).ShouldBeTrue();
				foreach (var table in new[] { "AppAdmissionDocumentEvidence", "AppAdmissionDocumentReviews" })
					DirectReadCount(capture, table).ShouldBe(1, $"{operation} must share one same-application evidence evaluation across all visited stages.");
				if (operation != "readiness") DirectReadCount(capture, "AppAdmissionWorkflowStages").ShouldBe(1);
			});
		});

	[Fact]
	public Task Seat_finalization_reads_batch_pinned_definitions_and_exact_stage_reservations() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("SeatSupport");
			var reservationIds = await WithUnitOfWorkAsync(async () =>
			{
				var workflow = await GetRequiredService<IRepository<AdmissionApplicationWorkflowInstance, Guid>>().GetAsync(item => item.ApplicationId == fixture.Application.Id);
				var pool = new AdmissionSeatPool(Guid.NewGuid(), TestTenantId, fixture.Offering.Id, 10);
				await GetRequiredService<IRepository<AdmissionSeatPool, Guid>>().InsertAsync(pool, true);
				var ids = new List<Guid>();
				for (var index = 0; index < 3; index++)
				{
					var policy = new AdmissionSeatStagePolicy { Version = 1, Enabled = true };
					var definition = new AdmissionWorkflowStageDefinition(Guid.NewGuid(), workflow.WorkflowDefinitionId, $"SUPPORT_SEAT_{index}",
						$"Supporting seat {index}", AdmissionWorkflowStageType.Seat, index + 10, true, false, null, JsonSerializer.Serialize(new { seat = policy }));
					await GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>().InsertAsync(definition, true);
					var stage = new AdmissionApplicationStageInstance(Guid.NewGuid(), TestTenantId, workflow.Id, definition.Id, definition.Code,
						definition.Name, definition.StageType, definition.DisplayOrder, true);
					stage.Complete(TestUserId);
					await GetRequiredService<IRepository<AdmissionApplicationStageInstance, Guid>>().InsertAsync(stage, true);
					var now = DateTime.UtcNow;
					var reservation = new AdmissionSeatReservation(Guid.NewGuid(), TestTenantId, pool.Id, fixture.Application.Id, stage.Id,
						AdmissionSeatReservationStatus.Reserved, now, now.AddDays(1), TestUserId, "Supporting regression");
					reservation.Confirm(now, TestUserId, "Confirmed");
					await GetRequiredService<IRepository<AdmissionSeatReservation, Guid>>().InsertAsync(reservation, true);
					ids.Add(reservation.Id);
				}
				return ids;
			});
			await WithUnitOfWorkAsync(async () =>
			{
				var application = await _applicationRepository.GetAsync(fixture.Application.Id);
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
				await GetRequiredService<AdmissionSeatRuntimeService>().EnsureApplicationCompleteAsync(application);
				DirectReadCount(capture, "AppAdmissionWorkflowStages").ShouldBe(1);
				DirectReadCount(capture, "AppAdmissionSeatReservations").ShouldBe(1);
			});
			await WithUnitOfWorkAsync(async () =>
			{
				await GetRequiredService<IRepository<AdmissionSeatReservation, Guid>>().DeleteAsync(reservationIds[1], true);
				var application = await _applicationRepository.GetAsync(fixture.Application.Id);
				var error = await Should.ThrowAsync<BusinessException>(() => GetRequiredService<AdmissionSeatRuntimeService>()
					.EnsureApplicationCompleteAsync(application));
				error.Code.ShouldBe(AdmissionSeatStageErrorCodes.CompletionRequired);
			});
		});

	[Theory]
	[InlineData("finalization")]
	[InlineData("snapshot")]
	[InlineData("readiness")]
	[InlineData("readinessNull")]
	public Task Document_supporting_batches_preserve_first_blocker_before_later_invalid_policy(string operation) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync($"DocumentOrder{operation}");
			var finalized = await FinalizeAsync(fixture.Application, true, $"document-order-{operation}");
			var snapshots = await SeedSupportingDocumentStagesAsync(fixture.Application.Id);
			await WithUnitOfWorkAsync(async () =>
			{
				var first = snapshots[0];
				var blocked = first.Policy with { Requirements = [first.Policy.Requirements.Single() with {
					Requirement = operation == "finalization" ? "required" : "conditional",
					ConditionJson = operation == "finalization" ? null : "{\"operator\":\"Equals\",\"field\":\"application.missing_supporting_fact\",\"value\":\"yes\"}" }] };
				snapshots[0] = first with { Policy = blocked };
				var stageRepository = GetRequiredService<IRepository<AdmissionApplicationStageInstance, Guid>>();
				var definitions = GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>();
				var firstStage = await stageRepository.GetAsync(first.StageId);
				var definition = await definitions.GetAsync(firstStage.DefinitionStageId);
				definition.Update(definition.Code, definition.Name, definition.StageType, definition.DisplayOrder, definition.IsRequired,
					definition.AllowManualSkip, definition.Description, JsonSerializer.Serialize(new { documentVerification = blocked }), false, false);
				await definitions.UpdateAsync(definition, true);
				var lastStage = await stageRepository.GetAsync(snapshots[^1].StageId);
				var lastDefinition = await definitions.GetAsync(lastStage.DefinitionStageId);
				lastDefinition.Update(lastDefinition.Code, lastDefinition.Name, lastDefinition.StageType, lastDefinition.DisplayOrder,
					lastDefinition.IsRequired, lastDefinition.AllowManualSkip, lastDefinition.Description, "{", false, false);
				await definitions.UpdateAsync(lastDefinition, true);
				var last = snapshots[^1];
				snapshots[^1] = operation == "readinessNull" ? null! : last with { Policy = last.Policy with {
					Requirements = [last.Policy.Requirements.Single() with { Requirement = "conditional", ConditionJson = "{" }] } };
				var item = await _onboardingCaseRepository.GetAsync(finalized.OnboardingCaseId);
				item.ExtraProperties["DocumentPoliciesSnapshot"] = JsonSerializer.Serialize(snapshots);
				await _onboardingCaseRepository.UpdateAsync(item, true);
			});
			await WithUnitOfWorkAsync(async () =>
			{
				var application = await _applicationRepository.GetAsync(fixture.Application.Id);
				var reader = GetRequiredService<OnboardingRequirementSnapshotService>();
				if (operation.StartsWith("readiness", StringComparison.Ordinal))
					(await reader.DocumentsReadyAsync(await _onboardingCaseRepository.GetAsync(finalized.OnboardingCaseId), application)).ShouldBeFalse();
				else
				{
					var error = await Should.ThrowAsync<BusinessException>(async () =>
					{
						if (operation == "snapshot") await reader.PrepareDocumentPoliciesAsync(application);
						else await GetRequiredService<AdmissionDocumentRuntimeService>().EnsureApplicationCompleteAsync(application);
					});
					error.Code.ShouldBe(AdmissionDocumentErrorCodes.EvidenceRequired);
				}
			});
		});

	private Task<List<OnboardingDocumentPolicySnapshot>> SeedSupportingDocumentStagesAsync(Guid applicationId) => WithUnitOfWorkAsync(async () =>
	{
		var workflow = await GetRequiredService<IRepository<AdmissionApplicationWorkflowInstance, Guid>>().GetAsync(item => item.ApplicationId == applicationId);
		var policies = new Dictionary<Guid, AdmissionDocumentPolicy>();
		for (var index = 0; index < 3; index++)
		{
			var policy = new AdmissionDocumentPolicy { Version = 1, Enabled = true, AllowWaivers = true,
				Requirements = [new() { Code = $"OPTIONAL_{index}", Name = $"Optional document {index}", Requirement = "optional" }] };
			var definition = new AdmissionWorkflowStageDefinition(Guid.NewGuid(), workflow.WorkflowDefinitionId, $"SUPPORT_DOC_{index}",
				$"Supporting document {index}", AdmissionWorkflowStageType.DocumentVerification, index + 10, true, false, null,
				JsonSerializer.Serialize(new { documentVerification = policy }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
			await GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>().InsertAsync(definition, true);
			var stage = new AdmissionApplicationStageInstance(Guid.NewGuid(), TestTenantId, workflow.Id, definition.Id, definition.Code,
				definition.Name, definition.StageType, definition.DisplayOrder, true);
			stage.Complete(TestUserId);
			await GetRequiredService<IRepository<AdmissionApplicationStageInstance, Guid>>().InsertAsync(stage, true);
			policies[stage.Id] = policy;
		}
		return (await GetRequiredService<IRepository<AdmissionApplicationStageInstance, Guid>>().GetListAsync(stage => stage.WorkflowInstanceId == workflow.Id &&
			stage.StageType == AdmissionWorkflowStageType.DocumentVerification && stage.Status != AdmissionRuntimeStageStatus.Pending))
			.Select(stage => new OnboardingDocumentPolicySnapshot(stage.Id, policies[stage.Id])).ToList();
	});

	private static int DirectReadCount(AcademicQueryCounter capture, string table) => capture.ReadCommands.Count(sql =>
		sql.Contains($"FROM \"{table}\"", StringComparison.Ordinal) && !sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
}
