using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Theory]
	[InlineData(1)]
	[InlineData(4)]
	public Task Assessment_batch_booking_does_not_rebuild_discarded_single_item_workspaces(int bookingCount) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixtures = new List<(AdmissionApplicationDto Application, Guid StageId, AdmissionOffering Offering)>();
			for (var index = 0; index < bookingCount; index++) fixtures.Add(await CreateManagedAssessmentApplicationAsync(scheduling: true));
			var service = GetRequiredService<IAdmissionAssessmentAppService>();
			var room = await CreateAssessmentPlanningRoomAsync();
			var scopes = await service.GetScopesAsync();
			var session = new AdmissionAssessmentSessionInput
			{
				Name = "Bounded batch booking", RoomId = room.Id, Capacity = bookingCount,
				StartsAt = DateTimeOffset.UtcNow.AddDays(30).Date.AddHours(3), EndsAt = DateTimeOffset.UtcNow.AddDays(30).Date.AddHours(4),
				Scopes = fixtures.Select(fixture => new AdmissionAssessmentSessionScopeInput
				{
					AdmissionOfferingId = fixture.Offering.Id,
					WorkflowStageDefinitionId = scopes.Single(item => item.AdmissionOfferingId == fixture.Offering.Id).WorkflowStageDefinitionId
				}).ToList()
			};
			var preview = await service.PreviewSessionAsync(new() { Session = session });
			var input = new AdmissionAssessmentSessionConfirmInput
			{
				Session = session, IdempotencyKey = Guid.NewGuid(), PreviewFingerprint = preview.PreviewFingerprint,
				Bookings = preview.ProposedBookings.Select(item => new AdmissionAssessmentSelectedBookingInput
				{
					ApplicationId = item.ApplicationId, StageInstanceId = item.StageInstanceId, ConcurrencyStamp = item.ConcurrencyStamp
				}).ToList()
			};
			var result = await WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
				var confirmation = await service.ConfirmSessionAsync(input);
				confirmation.BookedCount.ShouldBe(bookingCount);
				var stageReads = capture.ReadCommands.Where(sql => sql.Contains("FROM \"AppAdmissionApplicationStageInstances\"", StringComparison.Ordinal) &&
					!sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase)).ToList();
				Console.WriteLine($"SQLite assessment batch={bookingCount}; direct stage-context reads={stageReads.Count}; all reads={capture.ReadCount}.");
				stageReads.Count.ShouldBeLessThanOrEqualTo(bookingCount,
					"Each mutation retains its stage authorization; constructing a discarded full response must not perform a second context read.");
				return confirmation;
			});
			var replay = await service.ConfirmSessionAsync(input);
			replay.Session.Id.ShouldBe(result.Session.Id);
			replay.BookedCount.ShouldBe(bookingCount);
			await WithUnitOfWorkAsync(async () => (await GetRequiredService<IRepository<AdmissionAssessmentAttempt, Guid>>()
				.GetListAsync(item => item.SessionId == result.Session.Id)).Count.ShouldBe(bookingCount));
		});

	[Theory]
	[InlineData(4, true)]
	[InlineData(30, true)]
	[InlineData(4, false)]
	public Task Finalization_match_candidates_use_bounded_scalar_identity_reads(int candidateCount, bool hasBirthDate) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("SharedCandidate");
			var candidates = new List<Guid>();
			for (var index = 0; index < candidateCount; index++)
				candidates.Add((await CreateExistingStudentAsync(fixture.Offering, fixture.Applicant.FirstName, fixture.Applicant.LastName!)).Id);
			if (!hasBirthDate)
				await WithUnitOfWorkAsync(async () =>
				{
					var applicant = await _applicantRepository.GetAsync(fixture.Applicant.Id);
					applicant.UpdateProfile(applicant.FirstName, applicant.MiddleName, applicant.LastName, null, applicant.Gender,
						applicant.AddressLine1, applicant.AddressLine2, applicant.City, applicant.State, applicant.Country, applicant.PostalCode);
					await _applicantRepository.UpdateAsync(applicant, true);
				});
			await WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
				var preview = await _applicationAppService.GetFinalizationPreviewAsync(fixture.Application.Id);
				preview.PossibleMatches.Count.ShouldBe(Math.Min(10, candidateCount));
				preview.PossibleMatches.ShouldAllBe(match => candidates.Contains(match.StudentId) && match.StudentName == "SharedCandidate Patel");
				preview.PossibleMatches.ShouldAllBe(match => match.DateOfBirth == (hasBirthDate ? CertificationBirthDate : (DateTime?)null));
				preview.Items.Single(item => item.Key == "duplicate_resolution").Passed.ShouldBeFalse();
				db.ChangeTracker.Entries<Student>().ShouldBeEmpty(
					"Matching identities must not materialize complete Student graphs solely to compare birth dates.");
				var studentReads = capture.ReadCommands.Where(sql => sql.Contains("FROM \"AppStudents\"", StringComparison.Ordinal)).ToList();
				studentReads.Count.ShouldBeLessThanOrEqualTo(1);
				studentReads.ShouldAllBe(sql => !sql.Contains("StudentNotes", StringComparison.Ordinal) && !sql.Contains("StatusHistories", StringComparison.Ordinal));
			});
		});

	[Fact]
	public Task Assessment_roster_keeps_off_page_queue_totals_without_hydrating_off_page_context_entities() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixtures = new List<(AdmissionApplicationDto Application, Guid StageId, AdmissionOffering Offering)>();
			for (var index = 0; index < 4; index++) fixtures.Add(await CreateManagedAssessmentApplicationAsync());
			var service = GetRequiredService<IAdmissionAssessmentAppService>();
			await service.WaiveAsync(fixtures[0].Application.Id, fixtures[0].StageId,
				new() { ConcurrencyStamp = fixtures[0].Application.ConcurrencyStamp, Reason = "Prior accepted assessment" });
			await WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				var result = await service.GetApplicantsAsync(new() { MaxResultCount = 1, Sorting = "applicationNumber desc" });
				result.TotalCount.ShouldBe(4);
				result.Items.Count.ShouldBe(1);
				result.Counts.All.ShouldBe(4);
				result.Counts.Ready.ShouldBe(1, "A completed off-page waiver must still contribute to exact route-ready totals.");
				result.Counts.ToBook.ShouldBe(3);
				db.ChangeTracker.Entries<AdmissionApplication>().Count().ShouldBeLessThanOrEqualTo(1,
					"Only page rows need full application snapshots for the detailed workspace projection.");
				db.ChangeTracker.Entries<AdmissionApplicationStageInstance>().Count().ShouldBeLessThanOrEqualTo(1);
				db.ChangeTracker.Entries<AdmissionOffering>().ShouldBeEmpty();
			});
		});

	[Fact]
	public Task Onboarding_document_stages_batch_context_evidence_and_type_reads_with_identical_dtos() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateCertificationFixtureAsync("DocumentBatch");
			var finalized = await FinalizeAsync(fixture.Application, true, "document-batch-read");
			var stageIds = await WithUnitOfWorkAsync(async () =>
			{
				var workflow = await GetRequiredService<IRepository<AdmissionApplicationWorkflowInstance, Guid>>().GetAsync(item => item.ApplicationId == fixture.Application.Id);
				var snapshots = new List<OnboardingDocumentPolicySnapshot>();
				for (var stageIndex = 0; stageIndex < 3; stageIndex++)
				{
					var requirements = new List<AdmissionDocumentRequirement>();
					for (var index = 0; index < 3; index++)
					{
						var type = await GetRequiredService<IRepository<DocumentType, Guid>>().InsertAsync(new DocumentType(Guid.NewGuid(), TestTenantId,
							$"Batch type {stageIndex}-{index}", 3, [AcceptedFileFormat.Pdf]), true);
						requirements.Add(new() { Code = $"DOCUMENT_{index}", Name = $"Document {index}", DocumentTypeId = type.Id,
							AllowedExtensions = ["pdf", "png"], MaxFileSizeMb = 10 });
					}
					requirements.Add(new() { Code = "UNAVAILABLE", Name = "Unavailable mapping", DocumentTypeId = Guid.NewGuid() });
					var policy = new AdmissionDocumentPolicy { Version = 1, Enabled = true, Requirements = requirements };
					var definition = new AdmissionWorkflowStageDefinition(Guid.NewGuid(), workflow.WorkflowDefinitionId, $"BATCH_DOC_{stageIndex}",
						$"Document stage {stageIndex}", AdmissionWorkflowStageType.DocumentVerification, stageIndex + 10, true, false, null,
						JsonSerializer.Serialize(new { documentVerification = policy }, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
					await GetRequiredService<IRepository<AdmissionWorkflowStageDefinition, Guid>>().InsertAsync(definition, true);
					var stage = new AdmissionApplicationStageInstance(Guid.NewGuid(), TestTenantId, workflow.Id, definition.Id, definition.Code,
						definition.Name, definition.StageType, definition.DisplayOrder, false);
					await GetRequiredService<IRepository<AdmissionApplicationStageInstance, Guid>>().InsertAsync(stage, true);
					snapshots.Insert(0, new(stage.Id, policy));
				}
				var item = await _onboardingCaseRepository.GetAsync(finalized.OnboardingCaseId);
				item.ExtraProperties["DocumentPoliciesSnapshot"] = JsonSerializer.Serialize(snapshots);
				await _onboardingCaseRepository.UpdateAsync(item, true);
				return snapshots.Select(snapshot => snapshot.StageId).ToArray();
			});
			var expected = await WithUnitOfWorkAsync(async () =>
			{
				var result = new List<AdmissionDocumentVerificationDto>();
				foreach (var stageId in stageIds) result.Add(await GetRequiredService<AdmissionDocumentReadService>().GetAsync(fixture.Application.Id, stageId, true));
				return JsonSerializer.Serialize(result);
			});
			await WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
				var result = await GetRequiredService<IOnboardingWorkflowDocumentsAppService>().GetAsync(finalized.OnboardingCaseId);
				JsonSerializer.Serialize(result).ShouldBe(expected);
				result.Select(item => item.StageInstanceId).ShouldBe(stageIds);
				result.ShouldAllBe(stage => stage.Requirements.Single(item => item.Code == "UNAVAILABLE").CanUpload == false &&
					stage.Requirements.Single(item => item.Code == "DOCUMENT_0").AllowedExtensions.SequenceEqual(new[] { "pdf" }) &&
					stage.Requirements.Single(item => item.Code == "DOCUMENT_0").MaxFileSizeMb == 3);
				foreach (var table in new[] { "AppAdmissionApplicationStageInstances", "AppDocumentTypes", "AppAdmissionDocumentEvidence", "AppAdmissionDocumentReviews" })
				{
					var reads = capture.ReadCommands.Count(sql => sql.Contains($"FROM \"{table}\"", StringComparison.Ordinal) && !sql.Contains("JOIN", StringComparison.OrdinalIgnoreCase));
					reads.ShouldBe(1, $"All pinned document stages must share one bounded {table} read.");
				}
			});
		});
}
