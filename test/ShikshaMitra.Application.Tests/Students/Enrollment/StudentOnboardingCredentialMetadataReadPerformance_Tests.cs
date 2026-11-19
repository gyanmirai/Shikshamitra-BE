using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.EntityFrameworkCore;
using ShikshaMitra.Students.Portal;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

public partial class AdmissionWorkflowRuntimeIntegration_Tests
{
	[Fact]
	public Task Onboarding_normal_list_batches_credential_metadata_after_complete_command_payload_parity() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateNormalCredentialMetadataFixtureAsync();
			var service = GetRequiredService<IStudentOnboardingAppService>();
			var page = await service.GetListAsync(new() { StandardId = fixture.Offering.StandardId, MaxResultCount = 4 });
			page.TotalCount.ShouldBe(4);
			page.Items.Count.ShouldBe(4);
			foreach (var row in page.Items)
			{
				row.StudentEnrollmentId.ShouldNotBeNull();
				row.CredentialsPending.ShouldBe(row.Id == fixture.Cases[1].Id,
					"The still-pending required account handoff remains pending even after delivery was sent.");
				row.RequiredCredentialDeliveryConfirmed.ShouldBe(true);
				await AssertNormalCredentialCommandParityAsync(row);
			}

			var single = await MeasureNormalCredentialMetadataAsync(fixture.Offering.StandardId, 1);
			var four = await MeasureNormalCredentialMetadataAsync(fixture.Offering.StandardId, 4);
			single.Page.Items.Count.ShouldBe(1);
			four.Page.Items.Count.ShouldBe(4);
			single.Page.TotalCount.ShouldBe(4);
			four.Page.TotalCount.ShouldBe(4);
			var evidence = $"SQLite normal list page1/page4: applicants={single.Applicants}/{four.Applicants}, requests={single.Requests}/{four.Requests}, handoffs={single.Handoffs}/{four.Handoffs}, cases={single.Cases}/{four.Cases}, all={single.Reads}/{four.Reads}.";
			Console.WriteLine(evidence);
			four.Applicants.ShouldBeLessThanOrEqualTo(single.Applicants, evidence);
			four.Requests.ShouldBeLessThanOrEqualTo(single.Requests, evidence);
			four.Handoffs.ShouldBeLessThanOrEqualTo(single.Handoffs, evidence);
			four.Cases.ShouldBeLessThanOrEqualTo(single.Cases, evidence);
			four.Reads.ShouldBeLessThanOrEqualTo(single.Reads + 2, evidence);
		});

	[Theory]
	[InlineData("manual-parent", false, null)]
	[InlineData("confirmed-reuse", false, null)]
	[InlineData("unconfirmed-reuse", true, false)]
	[InlineData("manual-reused", false, null)]
	[InlineData("manual-owner-validation", true, false)]
	[InlineData("required-unmarked", false, null)]
	[InlineData("optional-unmarked", true, null)]
	[InlineData("historical-any-sent", false, true)]
	[InlineData("historical-explicit-pending", true, false)]
	[InlineData("unused-invalid-confirmation", true, false)]
	public Task Onboarding_normal_list_preserves_credential_owner_markers_and_parent_reuse(
		string scenario, bool expectedPending, bool? expectedDelivery) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateNormalCredentialMetadataFixtureAsync(allEnrolled: false);
			await WithUnitOfWorkAsync(async () =>
			{
				var item = await _onboardingCaseRepository.GetAsync(fixture.Cases[0].Id);
				var applicant = await _applicantRepository.GetAsync(item.ApplicantId);
				var requests = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
				await requests.DeleteAsync(request => request.CaseId == item.Id, true);
				var manual = scenario is "manual-parent" or "manual-reused" or "manual-owner-validation";
				if (manual) applicant.ExtraProperties["ParentAccountFlowVersion"] = 1;
				if (scenario is "confirmed-reuse" or "unconfirmed-reuse" or "manual-reused")
					item.ExtraProperties["SiblingParentAccountReused"] = true;
				if (scenario == "confirmed-reuse") item.ExtraProperties["OnboardingExistingParentConfirmedBy"] = TestUserId;
				if (scenario == "unused-invalid-confirmation") item.ExtraProperties["OnboardingExistingParentConfirmedBy"] = "not-a-guid";
				var send = scenario is not ("manual-owner-validation" or "required-unmarked" or "optional-unmarked" or "historical-explicit-pending");
				PinNormalCredentialMetadataRules(item, scenario == "optional-unmarked" ? StudentPortalAccountRules.Optional : StudentPortalAccountRules.Required, send);
				var ownerValidation = scenario is "confirmed-reuse" or "unconfirmed-reuse" or "manual-reused" or "manual-owner-validation" or "historical-explicit-pending" or "unused-invalid-confirmation";
				if (scenario is "historical-any-sent" or "historical-explicit-pending")
				{
					var sent = NewNormalCredentialRequest(item);
					if (ownerValidation) sent.RequireSavedOwnerValidation(TestUserId);
					sent.Sent(DateTime.UtcNow.AddMinutes(-1));
					await requests.InsertAsync(sent, true);
				}
				var pending = NewNormalCredentialRequest(item);
				if (ownerValidation)
					pending.ExtraProperties["SavedOwnerCredentialsRequested"] = "true";
				await requests.InsertAsync(pending, true);
				await _applicantRepository.UpdateAsync(applicant, true);
				await _onboardingCaseRepository.UpdateAsync(item, true);
			});
			var page = await GetRequiredService<IStudentOnboardingAppService>().GetListAsync(new()
			{
				StandardId = fixture.Offering.StandardId, MaxResultCount = 4
			});
			var actual = page.Items.Single(row => row.Id == fixture.Cases[0].Id);
			actual.CredentialsPending.ShouldBe(expectedPending, scenario);
			actual.RequiredCredentialDeliveryConfirmed.ShouldBe(expectedDelivery, scenario);
			foreach (var row in page.Items) await AssertNormalCredentialCommandParityAsync(row);
			if (scenario == "confirmed-reuse")
			{
				var measured = await MeasureNormalCredentialMetadataAsync(fixture.Offering.StandardId, 4);
				measured.Cases.ShouldBe(2, "Completed/cancelled rows are stable and must exercise the batched reuse path.");
				measured.Handoffs.ShouldBe(1, "Readiness must reuse the raw required-handoff page set.");
				measured.Applicants.ShouldBe(1);
				measured.Requests.ShouldBe(1);
			}
		});

	[Theory]
	[InlineData("cancelled", false)]
	[InlineData("no-rules", false)]
	[InlineData("pre-enrollment", true)]
	public Task Onboarding_normal_list_keeps_credential_short_circuits_before_unused_bad_parent_markers(string scenario, bool expectedPending) =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateNormalCredentialMetadataFixtureAsync(allEnrolled: false, firstEnrolled: scenario == "no-rules");
			await WithUnitOfWorkAsync(async () =>
			{
				var item = await _onboardingCaseRepository.GetAsync(fixture.Cases[0].Id);
				var applicant = await _applicantRepository.GetAsync(item.ApplicantId);
				applicant.ExtraProperties["ParentAccountFlowVersion"] = "unused-invalid-integer";
				if (scenario == "cancelled") item.Cancel("Fixture cancelled before credential evaluation", DateTime.UtcNow);
				if (scenario == "no-rules")
				{
					item.ExtraProperties.Remove("ReadinessRulesSnapshot");
					await GetRequiredService<IRepository<StudentOnboardingSettings, Guid>>().DeleteAsync(
						settings => settings.AcademicYearId == item.AcademicYearId || settings.AcademicYearId == null, true);
				}
				await _applicantRepository.UpdateAsync(applicant, true);
				await _onboardingCaseRepository.UpdateAsync(item, true);
			});
			// Pre-adopt first: SQLite reloads the same audit ticks with Unspecified Kind,
			// while the tracked first-adoption DTO still contains a local DateTime.
			var service = GetRequiredService<IStudentOnboardingAppService>();
			await service.GetListAsync(new() { StandardId = fixture.Offering.StandardId, MaxResultCount = 4 });
			PagedResultDto<StudentOnboardingCaseDto> warmed;
			using (var read = GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true, isTransactional: false))
			{
				warmed = await service.GetListAsync(new() { StandardId = fixture.Offering.StandardId, MaxResultCount = 4 });
				await read.CompleteAsync();
			}
			var target = warmed.Items.Single(row => row.Id == fixture.Cases[0].Id);
			target.CredentialsPending.ShouldBe(expectedPending, scenario);
			target.RequiredCredentialDeliveryConfirmed.ShouldBeNull(scenario);
			foreach (var row in warmed.Items) await AssertNormalCredentialCommandParityAsync(row);
			var measured = await MeasureNormalCredentialMetadataAsync(fixture.Offering.StandardId, 4);
			measured.Applicants.ShouldBe(0, "No row needs parent-flow metadata after the original early-return guards.");
			measured.Requests.ShouldBe(0, "Unused email metadata must remain lazy for all-skipped pages.");
			measured.Cases.ShouldBe(2, "Only the scoped count and page need a case query when readiness returns early.");
		});

	[Fact]
	public Task Onboarding_normal_list_keeps_rule_parse_failure_before_unused_later_parent_metadata() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateNormalCredentialMetadataFixtureAsync(allEnrolled: false);
			await WithUnitOfWorkAsync(async () =>
			{
				var item = await _onboardingCaseRepository.GetAsync(fixture.Cases[0].Id);
				item.ExtraProperties["ReadinessRulesSnapshot"] = "{";
				await _onboardingCaseRepository.UpdateAsync(item, true);
			});
			await WithUnitOfWorkAsync(async () =>
			{
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.ChangeTracker.Clear();
				using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
				await Should.ThrowAsync<JsonException>(() => GetRequiredService<IStudentOnboardingAppService>()
					.GetListAsync(new() { StandardId = fixture.Offering.StandardId, MaxResultCount = 4 }));
				capture.ReadCommands.ShouldAllBe(sql => !sql.Contains("FROM \"AppAdmissionApplicants\"", StringComparison.Ordinal) &&
					!sql.Contains("FROM \"AppStudentCredentialEmailRequests\"", StringComparison.Ordinal),
					"A raw readiness-rule parse failure occurs before any credential metadata is needed.");
			});
		});

	[Fact]
	public Task Onboarding_normal_list_observes_unsaved_intent_flushed_by_later_case_adoption() =>
		RunCertificationScenarioAsync(async () =>
		{
			var fixture = await CreateNormalCredentialMetadataFixtureAsync(adoptionIndex: 2);
			await WithUnitOfWorkAsync(async () =>
			{
				var later = await _onboardingCaseRepository.GetAsync(fixture.Cases[1].Id);
				PinNormalCredentialMetadataRules(later, StudentPortalAccountRules.Required, false);
				await _onboardingCaseRepository.UpdateAsync(later, true);
				await GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>().DeleteAsync(request => request.CaseId == later.Id, true);
				var handoffs = GetRequiredService<IRepository<StudentOnboardingServiceHandoff, Guid>>();
				foreach (var handoff in await handoffs.GetListAsync(value => value.OnboardingCaseId == later.Id))
				{
					handoff.Acknowledge(DateTime.UtcNow);
					await handoffs.UpdateAsync(handoff, true);
				}
				var adopting = await _onboardingCaseRepository.GetAsync(fixture.Cases[2].Id);
				AutomaticOnboardingReadinessRules.CanAdopt(adopting).ShouldBeTrue();
				adopting.ExtraProperties.Remove(AutomaticOnboardingReadinessRules.SnapshotVersionKey);
				await _onboardingCaseRepository.UpdateAsync(adopting, true);
			});
			StudentOnboardingCaseDto? observedLater = null;
			await WithUnitOfWorkAsync(async () =>
			{
				var requests = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
				var later = await _onboardingCaseRepository.GetAsync(fixture.Cases[1].Id);
				var pending = NewNormalCredentialRequest(later);
				pending.RequireSavedOwnerValidation(TestUserId);
				await requests.InsertAsync(pending, autoSave: false);
				var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
				db.Entry(pending).State.ShouldBe(EntityState.Added);
				var page = await GetRequiredService<IStudentOnboardingAppService>().GetListAsync(new()
				{
					StandardId = fixture.Offering.StandardId, Sorting = "RollNumber asc", MaxResultCount = 4
				});
				page.Items.Take(3).Select(row => row.Id).ShouldBe(new[] { fixture.Cases[0].Id, fixture.Cases[2].Id, fixture.Cases[1].Id });
				db.Entry(pending).State.ShouldBe(EntityState.Unchanged,
					"The middle case's existing adoption SaveChanges must flush the caller's new durable intent.");
				(await requests.AnyAsync(request => request.Id == pending.Id)).ShouldBeTrue();
				observedLater = page.Items.Single(row => row.Id == later.Id);
				observedLater.CredentialsPending.ShouldBeTrue(
					"The later stable case must observe the request flushed by the earlier adoption, as the canonical per-case read did.");
				observedLater.RequiredCredentialDeliveryConfirmed.ShouldBe(false);
			});
			observedLater.ShouldNotBeNull();
			await AssertNormalCredentialCommandParityAsync(observedLater);
		});

	private async Task<OnboardingWorklistFixture> CreateNormalCredentialMetadataFixtureAsync(bool allEnrolled = true, bool firstEnrolled = true, int? adoptionIndex = null)
	{
		var fixture = await CreateOnboardingWorklistFixtureAsync();
		await WithUnitOfWorkAsync(async () =>
		{
			var enrollments = GetRequiredService<IRepository<StudentEnrollment, Guid>>();
			var handoffs = GetRequiredService<IRepository<StudentOnboardingServiceHandoff, Guid>>();
			var requests = GetRequiredService<IRepository<StudentCredentialEmailRequest, Guid>>();
			for (var index = 0; index < fixture.Cases.Length; index++)
			{
				var item = await _onboardingCaseRepository.GetAsync(fixture.Cases[index].Id);
				var student = await _studentRepository.GetAsync(item.StudentId);
				if (string.IsNullOrWhiteSpace(student.GrNumber) && !student.GrNo.HasValue)
					student.AssignGrNumber($"META-{student.Id:N}"[..24], null, "Existing permanent fixture identifier");
				await _studentRepository.UpdateAsync(student, true);
				if ((allEnrolled || index == 0 && firstEnrolled) && (!adoptionIndex.HasValue || index != adoptionIndex.Value))
				{
					var enrollment = await enrollments.FindAsync(value => value.StudentId == item.StudentId && value.AcademicYearId == item.AcademicYearId);
					enrollment ??= await enrollments.InsertAsync(StudentEnrollment.Create(Guid.NewGuid(), TestTenantId,
						item.StudentId, item.AcademicYearId, item.IntendedStandardId, item.DivisionId, item.RollNumber, item.Id), true);
					item.CompleteHistoricalImport(enrollment.Id, item.DivisionId, item.RollNumber, DateTime.UtcNow);
				}
				else if (index > 0 && (!adoptionIndex.HasValue || index != adoptionIndex.Value)) item.Cancel("Unused metadata fixture", DateTime.UtcNow);
				if (!allEnrolled && index > 0)
				{
					var unused = await _applicantRepository.GetAsync(item.ApplicantId);
					unused.ExtraProperties["ParentAccountFlowVersion"] = "unused-invalid-integer";
					await _applicantRepository.UpdateAsync(unused, true);
				}
				PinNormalCredentialMetadataRules(item, StudentPortalAccountRules.Required, true);
				await _onboardingCaseRepository.UpdateAsync(item, true);
				foreach (var type in new[] { "student", "guardian" })
				{
					var handoff = new StudentOnboardingServiceHandoff(Guid.NewGuid(), TestTenantId, item.Id,
						type + "-account", true, $"metadata-{type}-{item.Id:N}");
					if (!allEnrolled || index != 1 || type != "guardian") handoff.Acknowledge(DateTime.UtcNow);
					await handoffs.InsertAsync(handoff, true);
				}
				var sent = NewNormalCredentialRequest(item);
				sent.Sent(DateTime.UtcNow);
				await requests.InsertAsync(sent, true);
			}
		});
		return fixture;
	}

	private static void PinNormalCredentialMetadataRules(StudentOnboardingCase item, string guardianAccess, bool sendGuardian) =>
		item.ExtraProperties["ReadinessRulesSnapshot"] = JsonSerializer.Serialize(new
		{
			studentAccount = false, studentAccountAccess = StudentPortalAccountRules.NotNeeded,
			guardianAccount = guardianAccess == StudentPortalAccountRules.Required, guardianAccountAccess = guardianAccess,
			sendStudentCredentialEmail = false, sendGuardianCredentialEmail = sendGuardian,
			credentialSendingTime = "activation", credentialProvisioningMode = "manual"
		});

	private static StudentCredentialEmailRequest NewNormalCredentialRequest(StudentOnboardingCase item) =>
		new(Guid.NewGuid(), TestTenantId, item.Id, item.StudentId, Guid.NewGuid(), Guid.NewGuid(), "guardian",
			"metadata-owner@example.test", TestUserId, DateTime.UtcNow);

	private async Task AssertNormalCredentialCommandParityAsync(StudentOnboardingCaseDto row)
	{
		var canonical = await GetRequiredService<IStudentOnboardingAppService>().AssignGrNumberAsync(row.Id,
			new() { ConcurrencyStamp = row.ConcurrencyStamp, NumberSeriesId = Guid.NewGuid() });
		canonical.Events = [];
		canonical.CapturedStudentFields = [];
		canonical.PortalAccountOwners = null;
		var actualJson = JsonSerializer.Serialize(row);
		var expectedJson = JsonSerializer.Serialize(canonical);
		using var actual = JsonDocument.Parse(actualJson);
		using var expected = JsonDocument.Parse(expectedJson);
		actualJson.ShouldBe(expectedJson,
			"Every serialized list field must match the unchanged idempotent command mapper, except the three existing detail-only omissions. " +
			string.Join("; ", NormalCredentialJsonDifferences(expected.RootElement, actual.RootElement, "$")));
	}

	private static IEnumerable<string> NormalCredentialJsonDifferences(JsonElement expected, JsonElement actual, string path)
	{
		if (expected.ValueKind == JsonValueKind.Object && actual.ValueKind == JsonValueKind.Object)
		{
			foreach (var property in expected.EnumerateObject())
				foreach (var difference in NormalCredentialJsonDifferences(property.Value, actual.GetProperty(property.Name), path + "." + property.Name))
					yield return difference;
		}
		else if (expected.ValueKind == JsonValueKind.Array && actual.ValueKind == JsonValueKind.Array && expected.GetArrayLength() == actual.GetArrayLength())
		{
			for (var index = 0; index < expected.GetArrayLength(); index++)
				foreach (var difference in NormalCredentialJsonDifferences(expected[index], actual[index], path + "[" + index + "]"))
					yield return difference;
		}
		else if (expected.GetRawText() != actual.GetRawText())
			yield return path + ": canonical=" + expected.GetRawText() + ", list=" + actual.GetRawText();
	}

	private async Task<NormalCredentialMetadataRead> MeasureNormalCredentialMetadataAsync(Guid standardId, int pageSize) =>
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			db.ChangeTracker.Clear();
			using var capture = new AcademicQueryCounter(db.Database.GetDbConnection());
			var page = await GetRequiredService<IStudentOnboardingAppService>().GetListAsync(new()
			{
				StandardId = standardId, Sorting = "StudentName asc", MaxResultCount = pageSize
			});
			int Count(string table) => capture.ReadCommands.Count(sql => sql.Contains("FROM \"" + table + "\"", StringComparison.Ordinal));
			return new NormalCredentialMetadataRead(page, Count("AppAdmissionApplicants"), Count("AppStudentCredentialEmailRequests"),
				Count("AppStudentOnboardingServiceHandoffs"), Count("AppStudentOnboardingCases"), capture.ReadCount);
		});

	private sealed record NormalCredentialMetadataRead(PagedResultDto<StudentOnboardingCaseDto> Page,
		int Applicants, int Requests, int Handoffs, int Cases, int Reads);
}
