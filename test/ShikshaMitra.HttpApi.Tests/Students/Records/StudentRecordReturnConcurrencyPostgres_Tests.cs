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
	public async Task Record_postgres_concurrent_same_year_return_replays_one_review_and_reopens_original_enrollment()
	{
		Client.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		var fixture = await SeedHistoricalStudentsAndTargetAsync(); var studentId = fixture.Students[0];
		using var reviewer = CreateClientAs(fixture.Reviewer); reviewer.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		var legacy = await PostResultAsync<StudentLegacyOverrideDto>(Client, $"/api/students/records/directory/{studentId}/legacy-overrides",
			new RequestStudentLegacyOverrideDto { Reason = "Verified synthetic historical register for same-year return" });
		await PostResultAsync<StudentLegacyOverrideDto>(reviewer, $"/api/students/records/directory/legacy-overrides/{legacy.Id}/approve",
			new DecideStudentLegacyOverrideDto { ConcurrencyStamp = legacy.ConcurrencyStamp, Remarks = "Independent historical review" });
		var source = await InTenantAsync(async services => await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().GetAsync(x => x.StudentId == studentId));
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Status"].Mode = "Direct"; policy.Transitions.RosterRemovalTiming = "EffectiveDate";
		using var save = await Client.PutAsJsonAsync("/api/students/records/settings/draft", new SaveStudentRecordsSettingsDto { Policy = policy }, JsonOptions);
		save.StatusCode.ShouldBe(HttpStatusCode.OK, await save.Content.ReadAsStringAsync());
		var draft = (await save.Content.ReadFromJsonAsync<Result<StudentRecordsSettingsDto>>(JsonOptions))!.Data!;
		await PostResultAsync<StudentRecordsSettingsDto>(Client, "/api/students/records/settings/publish",
			new PublishStudentRecordsSettingsDto { ConcurrencyStamp = draft.ConcurrencyStamp! });
		using var owner = await Client.PutAsJsonAsync("/api/student/onboarding/settings/configurations", new SaveStudentOnboardingSettingsDto
		{
			AcademicYearId = fixture.SourceYear,
			ConfigurationJson = JsonSerializer.Serialize(new
			{
				readinessAccess = new { requireDocuments = false, checks = new[] { new { key = "identity", required = false }, new { key = "family", required = false }, new { key = "communication", required = false } } },
				onboardingProfiles = Array.Empty<object>(), grNumber = new { configurations = Array.Empty<object>() },
				divisionChange = new { configurations = Array.Empty<object>() }, rollNumber = new { assignmentMode = "manual", configurations = Array.Empty<object>() }
			})
		}, JsonOptions);
		owner.StatusCode.ShouldBe(HttpStatusCode.OK, await owner.Content.ReadAsStringAsync());
		var today = AdmissionCycleWindowEvaluator.GetInstitutionDate(DateTime.UtcNow);
		var exit = new StudentRecordTransitionBatchInput { AcademicYearId = fixture.SourceYear, IdempotencyKey = "pg-before-return-" + studentId.ToString("N"),
			Items = [new() { StudentId = studentId, Outcome = "Withdraw", EffectiveDate = today, Reason = "Reviewed temporary school withdrawal" }] };
		var before = (await PostResultAsync<StudentRecordTransitionPreviewDto>(Client, Transitions + "/preview", exit)).Items.Single();
		before.Ready.ShouldBeTrue(); exit.Items[0].StudentConcurrencyStamp = before.StudentConcurrencyStamp; exit.Items[0].EnrollmentConcurrencyStamp = before.EnrollmentConcurrencyStamp;
		(await PostResultAsync<StudentRecordTransitionBatchResultDto>(Client, Transitions + "/batches", exit)).Items.Single().Status.ShouldBe("Applied");
		var request = new StudentRecordReturnInput
		{
			StudentId = studentId, Route = "ReuseProfile", TargetAcademicYearId = fixture.SourceYear,
			TargetStandardId = source.StandardId!.Value, TargetDivisionId = source.DivisionId!.Value, RollNumber = source.RollNumber,
			RollAssignmentMode = "manual", EffectiveDate = today, Reason = "Reviewed same-year school return", IdempotencyKey = "pg-return-race-" + studentId.ToString("N")
		};
		var preview = await PostResultAsync<StudentRecordTransitionPreviewRowDto>(Client, Transitions + "/returns/preview", request);
		preview.Ready.ShouldBeTrue(string.Join("; ", preview.Issues.Select(x => x.Code)));
		request.StudentConcurrencyStamp = preview.StudentConcurrencyStamp; request.EnrollmentConcurrencyStamp = preview.EnrollmentConcurrencyStamp;
		factory.Barrier.Arm();
		var requests = Enumerable.Range(0, 2).Select(_ => Client.PostAsJsonAsync(Transitions + "/returns", request, JsonOptions)).ToArray();
		try { await factory.Barrier.Reached.WaitAsync(TimeSpan.FromSeconds(30)); }
		finally { factory.Barrier.Release(); }
		var responses = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(60));
		try
		{
			factory.Barrier.Connections.Count.ShouldBe(2);
			factory.Barrier.ProviderFailures.Keys.ShouldContain("40001", "The repeated request conflict must arise naturally from PostgreSQL.");
			responses.Count(x => x.StatusCode == HttpStatusCode.OK).ShouldBe(1);
			var rejected = responses.Single(x => x.StatusCode != HttpStatusCode.OK);
			rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await rejected.Content.ReadAsStringAsync());
			var accepted = (await responses.Single(x => x.StatusCode == HttpStatusCode.OK).Content.ReadFromJsonAsync<Result<StudentRecordReturnDto>>(JsonOptions))!.Data!;
			accepted.Status.ShouldBe("AwaitingRenewal"); accepted.RenewalCaseId.ShouldNotBeNull();
			var replay = await PostResultAsync<StudentRecordReturnDto>(Client, Transitions + "/returns", request);
			replay.TransitionId.ShouldBe(accepted.TransitionId); replay.RenewalCaseId.ShouldBe(accepted.RenewalCaseId);
			await InTenantAsync(async services =>
			{
				(await services.GetRequiredService<IRepository<StudentRecordTransitionBatch, Guid>>().CountAsync(x => x.IdempotencyKey == request.IdempotencyKey)).ShouldBe(1);
				(await services.GetRequiredService<IRepository<StudentRecordTransition, Guid>>().CountAsync(x => x.StudentId == studentId && x.Outcome == "Return")).ShouldBe(1);
				(await services.GetRequiredService<IRepository<StudentRecordRenewalCase, Guid>>().CountAsync(x => x.StudentId == studentId)).ShouldBe(1);
				(await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().GetAsync(source.Id)).IsActive.ShouldBeFalse();
				(await services.GetRequiredService<IStudentRepository>().GetAsync(studentId)).Status.ShouldBe(StudentLifeCycleStatusEnum.Left);
				return true;
			});
			using var read = await Client.GetAsync(Transitions + $"/renewals/{accepted.RenewalCaseId}");
			read.StatusCode.ShouldBe(HttpStatusCode.OK);
			var review = (await read.Content.ReadFromJsonAsync<Result<StudentRecordRenewalDto>>(JsonOptions))!.Data!;
			review.Status.ShouldBe("PendingChecklist"); review.Issues.ShouldBeEmpty();
			var activated = await PostResultAsync<StudentRecordRenewalDto>(Client, Transitions + $"/renewals/{review.Id}/activate",
				new StudentRecordRenewalDecisionDto { ConcurrencyStamp = review.ConcurrencyStamp, Remarks = "Verified existing enrollment reopening" });
			activated.Status.ShouldBe("Provisioning");
			await InTenantAsync(async services =>
			{
				var enrollment = await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().GetAsync(source.Id);
				enrollment.IsActive.ShouldBeTrue(); enrollment.StandardId.ShouldBe(source.StandardId); enrollment.DivisionId.ShouldBe(source.DivisionId); enrollment.RollNumber.ShouldBe(source.RollNumber);
				(await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().CountAsync(x => x.StudentId == studentId)).ShouldBe(1);
				(await services.GetRequiredService<IRepository<StudentRecordRenewalCase, Guid>>().GetAsync(review.Id)).StudentEnrollmentId.ShouldBe(source.Id);
				(await services.GetRequiredService<IRepository<StudentRecordTransitionEvent, Guid>>().CountAsync(x => x.TransitionId == accepted.TransitionId && x.Action == "RenewalActivated")).ShouldBe(1);
				var student = await services.GetRequiredService<IStudentRepository>().GetAsync(studentId);
				student.Status.ShouldBe(StudentLifeCycleStatusEnum.Enrolled); student.GrNumber.ShouldBe("PG-RECORDS-0");
				return true;
			});
			var afterActivation = await PostResultAsync<StudentRecordReturnDto>(Client, Transitions + "/returns", request);
			afterActivation.RenewalCaseId.ShouldBe(review.Id); afterActivation.TransitionId.ShouldBe(accepted.TransitionId);
			output.WriteLine("Concurrent same-key PostgreSQL same-year returns: natural 40001, one review/transition/batch, stable replay before and after activation, original enrollment reopened once, permanent GR unchanged.");
		}
		finally { foreach (var response in responses) response.Dispose(); }
	}
}
