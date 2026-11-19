using System;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.Records;

public sealed partial class StudentRecordsApplicationPostgres_Tests
{
	[StudentRecordsApplicationPostgresFact]
	public async Task Record_postgres_concurrent_reviewers_advance_one_stage_preserve_one_decision_and_reject_stale_review()
	{
		Client.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		var fixture = await SeedHistoricalStudentsAndTargetAsync(); var studentId = fixture.Students[0];
		var otherReviewer = await InTenantAsync(async services =>
		{
			var repository = services.GetRequiredService<IRepository<IdentityUser, Guid>>();
			var other = new IdentityUser(Guid.NewGuid(), "records-pg-second-reviewer", "records-pg-second-reviewer@example.test", TenantId);
			await repository.InsertAsync(other, true);
			await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TenantId, userId: other.Id, description: "Isolated concurrent reviewer"), true);
			var manager = services.GetRequiredService<IdentityUserManager>();
			foreach (var actor in new[] { await repository.GetAsync(fixture.Reviewer), other })
			{
				var result = await manager.AddToRoleAsync(actor, "admin");
				result.Succeeded.ShouldBeTrue(string.Join("; ", result.Errors.Select(x => x.Description)));
			}
			return other.Id;
		});
		using var first = CreateClientAs(fixture.Reviewer); first.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		using var second = CreateClientAs(otherReviewer); second.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		var legacy = await PostResultAsync<StudentLegacyOverrideDto>(Client, $"/api/students/records/directory/{studentId}/legacy-overrides",
			new RequestStudentLegacyOverrideDto { Reason = "Verified synthetic historical register for concurrent approval" });
		await PostResultAsync<StudentLegacyOverrideDto>(first, $"/api/students/records/directory/legacy-overrides/{legacy.Id}/approve",
			new DecideStudentLegacyOverrideDto { ConcurrencyStamp = legacy.ConcurrencyStamp, Remarks = "Independent register review" });
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Status"].Stages = [new(), new()];
		policy.Transitions.RosterRemovalTiming = "EffectiveDate";
		using var save = await Client.PutAsJsonAsync("/api/students/records/settings/draft", new SaveStudentRecordsSettingsDto { Policy = policy }, JsonOptions);
		save.StatusCode.ShouldBe(HttpStatusCode.OK, await save.Content.ReadAsStringAsync());
		var draft = (await save.Content.ReadFromJsonAsync<Result<StudentRecordsSettingsDto>>(JsonOptions))!.Data!;
		await PostResultAsync<StudentRecordsSettingsDto>(Client, "/api/students/records/settings/publish",
			new PublishStudentRecordsSettingsDto { ConcurrencyStamp = draft.ConcurrencyStamp! });
		var request = new StudentRecordTransitionBatchInput
		{
			AcademicYearId = fixture.SourceYear, IdempotencyKey = "pg-review-race-" + studentId.ToString("N"),
			Items = [new() { StudentId = studentId, Outcome = "Withdraw", EffectiveDate = AdmissionCycleWindowEvaluator.GetInstitutionDate(DateTime.UtcNow).AddDays(1), Reason = "Reviewed future withdrawal" }]
		};
		var preview = (await PostResultAsync<StudentRecordTransitionPreviewDto>(Client, Transitions + "/preview", request)).Items.Single();
		preview.Ready.ShouldBeTrue(string.Join("; ", preview.Issues.Select(x => x.Code)));
		request.Items[0].StudentConcurrencyStamp = preview.StudentConcurrencyStamp; request.Items[0].EnrollmentConcurrencyStamp = preview.EnrollmentConcurrencyStamp;
		var transition = (await PostResultAsync<StudentRecordTransitionBatchResultDto>(Client, Transitions + "/batches", request)).Items.Single();
		transition.Status.ShouldBe("Submitted"); transition.CurrentApprovalStage.ShouldBe(0); transition.ApprovalStageCount.ShouldBe(2);
		var decision = new StudentRecordTransitionDecisionDto { ConcurrencyStamp = transition.ConcurrencyStamp, Remarks = "Verified review stage" };
		using var self = await Client.PostAsJsonAsync(Transitions + $"/{transition.Id}/approve", decision, JsonOptions);
		self.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		factory.Barrier.Arm("UPDATE \"AppStudentRecordTransitions\"", IsolationLevel.ReadCommitted);
		var requests = new[] { first.PostAsJsonAsync(Transitions + $"/{transition.Id}/approve", decision, JsonOptions), second.PostAsJsonAsync(Transitions + $"/{transition.Id}/approve", decision, JsonOptions) };
		try { await factory.Barrier.Reached.WaitAsync(TimeSpan.FromSeconds(30)); }
		finally { factory.Barrier.Release(); }
		var responses = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(60));
		try
		{
			factory.Barrier.Connections.Count.ShouldBe(2);
			responses.Count(x => x.StatusCode == HttpStatusCode.OK).ShouldBe(1);
			var rejected = responses.Single(x => x.StatusCode != HttpStatusCode.OK);
			rejected.StatusCode.ShouldBe(HttpStatusCode.Conflict, await rejected.Content.ReadAsStringAsync());
			var winner = Array.FindIndex(responses, x => x.StatusCode == HttpStatusCode.OK);
			var winnerActor = winner == 0 ? fixture.Reviewer : otherReviewer;
			var accepted = (await responses[winner].Content.ReadFromJsonAsync<Result<StudentRecordTransitionDto>>(JsonOptions))!.Data!;
			accepted.Status.ShouldBe("Submitted"); accepted.CurrentApprovalStage.ShouldBe(1);
			var audit = await InTenantAsync(async services =>
			{
				var events = await services.GetRequiredService<IRepository<StudentRecordTransitionEvent, Guid>>().GetListAsync(x => x.TransitionId == transition.Id && x.Action == "Approved");
				events.Count.ShouldBe(1); events.Single().ActorId.ShouldBe(winnerActor); events.Single().Stage.ShouldBe(1);
				var current = await services.GetRequiredService<IRepository<StudentRecordTransition, Guid>>().GetAsync(transition.Id);
				current.Status.ShouldBe("Submitted"); current.CurrentApprovalStage.ShouldBe(1); current.LastApprovedBy.ShouldBe(winnerActor);
				var student = await services.GetRequiredService<IStudentRepository>().GetAsync(studentId);
				student.Status.ShouldBe(StudentLifeCycleStatusEnum.Enrolled); student.GrNumber.ShouldBe("PG-RECORDS-0");
				(await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().GetAsync(x => x.StudentId == studentId)).IsActive.ShouldBeTrue();
				return true;
			});
			audit.ShouldBeTrue();
			using var stale = await second.PostAsJsonAsync(Transitions + $"/{transition.Id}/approve", decision, JsonOptions);
			stale.StatusCode.ShouldBe(HttpStatusCode.BadRequest); (await stale.Content.ReadAsStringAsync()).ShouldContain("Students:Records:ConcurrencyConflict");
			var refreshedReviewer = winner == 0 ? second : first;
			var completed = await PostResultAsync<StudentRecordTransitionDto>(refreshedReviewer, Transitions + $"/{transition.Id}/approve",
				new StudentRecordTransitionDecisionDto { ConcurrencyStamp = accepted.ConcurrencyStamp, Remarks = "Verified remaining school approval stage" });
			completed.Status.ShouldBe("Scheduled"); completed.CurrentApprovalStage.ShouldBe(2);
			completed.History.Count(x => x.Action == "Approved").ShouldBe(2);
			completed.History.Count(x => x.Action == "Applied").ShouldBe(0);
			completed.History.Where(x => x.Action == "Approved").Select(x => x.ActorId).Distinct().Count().ShouldBe(2);
			await InTenantAsync(async services =>
			{
				(await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().GetAsync(x => x.StudentId == studentId)).IsActive.ShouldBeTrue();
				(await services.GetRequiredService<IRepository<StudentRecordTransitionEvent, Guid>>().CountAsync(x => x.TransitionId == transition.Id && x.Action == "Approved")).ShouldBe(2);
				return true;
			});
			output.WriteLine("Two actual PostgreSQL reviewers at the same stage: one committed approval and one natural optimistic 409; stale stamp rejected, source untouched, refreshed second review advances only the remaining stage to Scheduled.");
		}
		finally { foreach (var response in responses) response.Dispose(); }
	}
}
