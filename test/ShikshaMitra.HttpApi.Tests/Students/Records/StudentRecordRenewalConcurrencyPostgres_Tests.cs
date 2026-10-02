using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.Records;

public sealed partial class StudentRecordsApplicationPostgres_Tests
{
	[StudentRecordsApplicationPostgresFact]
	public async Task Record_postgres_concurrent_renewal_activation_preserves_one_enrollment_and_returns_refreshable_conflict()
	{
		Client.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		var fixture = await SeedHistoricalStudentsAndTargetAsync(); var studentId = fixture.Students[0];
		using var reviewer = CreateClientAs(fixture.Reviewer); reviewer.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		var legacy = await PostResultAsync<StudentLegacyOverrideDto>(Client, $"/api/students/records/directory/{studentId}/legacy-overrides",
			new RequestStudentLegacyOverrideDto { Reason = "Verified synthetic historical register for concurrent annual renewal" });
		await PostResultAsync<StudentLegacyOverrideDto>(reviewer, $"/api/students/records/directory/legacy-overrides/{legacy.Id}/approve",
			new DecideStudentLegacyOverrideDto { ConcurrencyStamp = legacy.ConcurrencyStamp, Remarks = "Independent historical review" });
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Placement"].Mode = "Direct"; policy.Transitions.AnnualRenewalMode = "ShortChecklist";
		using var save = await Client.PutAsJsonAsync("/api/students/records/settings/draft", new SaveStudentRecordsSettingsDto { Policy = policy }, JsonOptions);
		save.StatusCode.ShouldBe(HttpStatusCode.OK, await save.Content.ReadAsStringAsync());
		var draft = (await save.Content.ReadFromJsonAsync<Result<StudentRecordsSettingsDto>>(JsonOptions))!.Data!;
		await PostResultAsync<StudentRecordsSettingsDto>(Client, "/api/students/records/settings/publish",
			new PublishStudentRecordsSettingsDto { ConcurrencyStamp = draft.ConcurrencyStamp! });
		// Configure optional returning-student evidence through its real owner API;
		// permanent GR, placement and Finance completion gates remain mandatory.
		using var owner = await Client.PutAsJsonAsync("/api/student/onboarding/settings/configurations", new SaveStudentOnboardingSettingsDto
		{
			AcademicYearId = fixture.TargetYear,
			ConfigurationJson = JsonSerializer.Serialize(new
			{
				readinessAccess = new { requireDocuments = false, checks = new[] { new { key = "identity", required = false }, new { key = "family", required = false }, new { key = "communication", required = false } } },
				onboardingProfiles = Array.Empty<object>(), grNumber = new { configurations = Array.Empty<object>() },
				divisionChange = new { configurations = Array.Empty<object>() }, rollNumber = new { assignmentMode = "manual", configurations = Array.Empty<object>() }
			})
		}, JsonOptions);
		owner.StatusCode.ShouldBe(HttpStatusCode.OK, await owner.Content.ReadAsStringAsync());
		var request = new StudentRecordTransitionBatchInput
		{
			AcademicYearId = fixture.SourceYear, IdempotencyKey = "pg-renewal-race-" + studentId.ToString("N"),
			Items = [new() { StudentId = studentId, Outcome = "Promote", TargetAcademicYearId = fixture.TargetYear,
				TargetStandardId = fixture.TargetStandard, TargetDivisionId = fixture.TargetDivision, RollNumber = 40,
				RollAssignmentMode = "manual", EffectiveDate = AdmissionCycleWindowEvaluator.GetInstitutionDate(DateTime.UtcNow), Reason = "Reviewed annual renewal" }]
		};
		var preview = (await PostResultAsync<StudentRecordTransitionPreviewDto>(Client, Transitions + "/preview", request)).Items.Single();
		preview.Ready.ShouldBeTrue(string.Join("; ", preview.Issues.Select(x => x.Code)));
		request.Items[0].StudentConcurrencyStamp = preview.StudentConcurrencyStamp; request.Items[0].EnrollmentConcurrencyStamp = preview.EnrollmentConcurrencyStamp;
		var transition = (await PostResultAsync<StudentRecordTransitionBatchResultDto>(Client, Transitions + "/batches", request)).Items.Single();
		transition.Status.ShouldBe("AwaitingRenewal");
		var renewalId = transition.RenewalCaseId!.Value;
		using var read = await Client.GetAsync(Transitions + $"/renewals/{renewalId}");
		read.StatusCode.ShouldBe(HttpStatusCode.OK, await read.Content.ReadAsStringAsync());
		var renewal = (await read.Content.ReadFromJsonAsync<Result<StudentRecordRenewalDto>>(JsonOptions))!.Data!;
		renewal.Status.ShouldBe("PendingChecklist"); renewal.Issues.ShouldBeEmpty();
		var activation = new StudentRecordRenewalDecisionDto { ConcurrencyStamp = renewal.ConcurrencyStamp, Remarks = "Verified annual enrollment activation" };
		factory.Barrier.Arm("INSERT INTO \"AppStudentEnrollments\"");
		var requests = Enumerable.Range(0, 2).Select(_ => Client.PostAsJsonAsync(Transitions + $"/renewals/{renewalId}/activate", activation, JsonOptions)).ToArray();
		try { await factory.Barrier.Reached.WaitAsync(TimeSpan.FromSeconds(30)); }
		finally { factory.Barrier.Release(); }
		var responses = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(60));
		try
		{
			factory.Barrier.Connections.Count.ShouldBe(2);
			factory.Barrier.ProviderFailures.Keys.ShouldContain("40001", "The provider conflict must arise naturally from real concurrent writes.");
			responses.Count(x => x.StatusCode == HttpStatusCode.OK).ShouldBe(1);
			var rejected = responses.Single(x => x.StatusCode != HttpStatusCode.OK);
			rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await rejected.Content.ReadAsStringAsync());
			(await rejected.Content.ReadAsStringAsync()).ShouldContain("Students:Records:ConcurrencyConflict");
			var winning = (await responses.Single(x => x.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<Result<StudentRecordRenewalDto>>(JsonOptions))!.Data!;
			winning.Status.ShouldBe("Provisioning");
			await InTenantAsync(async services =>
			{
				var enrollments = services.GetRequiredService<IRepository<StudentEnrollment, Guid>>();
				(await enrollments.CountAsync(x => x.StudentId == studentId)).ShouldBe(2);
				(await enrollments.CountAsync(x => x.StudentId == studentId && x.IsActive)).ShouldBe(1);
				(await enrollments.GetAsync(x => x.StudentId == studentId && x.AcademicYearId == fixture.SourceYear)).IsActive.ShouldBeFalse();
				var target = await enrollments.GetAsync(x => x.StudentId == studentId && x.AcademicYearId == fixture.TargetYear);
				target.RollNumber.ShouldBe(40); target.DivisionId.ShouldBe(fixture.TargetDivision);
				(await services.GetRequiredService<IRepository<StudentRecordRenewalCase, Guid>>().CountAsync(x => x.StudentId == studentId)).ShouldBe(1);
				(await services.GetRequiredService<IRepository<StudentRecordTransitionEvent, Guid>>().CountAsync(x => x.TransitionId == transition.Id && x.Action == "RenewalActivated")).ShouldBe(1);
				var current = await services.GetRequiredService<IStudentRepository>().GetAsync(studentId);
				current.GrNumber.ShouldBe("PG-RECORDS-0"); current.AcademicYearId.ShouldBe(fixture.TargetYear);
				return true;
			});
			using var stale = await Client.PostAsJsonAsync(Transitions + $"/renewals/{renewalId}/activate", activation, JsonOptions);
			stale.StatusCode.ShouldBe(HttpStatusCode.BadRequest); (await stale.Content.ReadAsStringAsync()).ShouldContain("Students:Records:ConcurrencyConflict");
			using var fresh = await Client.PostAsJsonAsync(Transitions + $"/renewals/{renewalId}/activate",
				new StudentRecordRenewalDecisionDto { ConcurrencyStamp = winning.ConcurrencyStamp, Remarks = "Finance completion remains owned by Finance" }, JsonOptions);
			fresh.StatusCode.ShouldBe(HttpStatusCode.BadRequest); (await fresh.Content.ReadAsStringAsync()).ShouldContain("Students:Records:RenewalProvisioningRequired");
			output.WriteLine("Two real PostgreSQL SERIALIZABLE renewal activations: one target enrollment and activation event, natural 40001 mapped to refreshable conflict, no duplicate history; fresh retry still requires Finance.");
		}
		finally { foreach (var response in responses) response.Dispose(); }
	}
}
