using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentRecords;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Threading;

namespace ShikshaMitra.Students.Records;

public sealed partial class StudentRecordMidnightSchedule_Tests
{
	[StudentRecordsApplicationPostgresFact]
	public async Task Record_postgres_two_transition_workers_preserve_one_effective_withdrawal_and_its_history()
	{
		factory.UtcNow = new DateTime(2026, 10, 1, 18, 29, 59, DateTimeKind.Utc);
		var fixture = await SeedAsync();
		using var actor = CreateClientAs(fixture.Actor); actor.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		using var reviewer = CreateClientAs(fixture.Reviewer); reviewer.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		var legacy = await PostAsync<StudentLegacyOverrideDto>(actor, $"/api/students/records/directory/{fixture.Student}/legacy-overrides",
			new RequestStudentLegacyOverrideDto { Reason = "Verified synthetic register for overlapping transition workers" });
		await PostAsync<StudentLegacyOverrideDto>(reviewer, $"/api/students/records/directory/legacy-overrides/{legacy.Id}/approve",
			new DecideStudentLegacyOverrideDto { ConcurrencyStamp = legacy.ConcurrencyStamp, Remarks = "Independent historical entry review" });
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Status"].Mode = "Direct"; policy.Transitions.RosterRemovalTiming = "EffectiveDate";
		var settings = await GetAsync<StudentRecordsSettingsDto>(actor, "/api/students/records/settings");
		using var save = await actor.PutAsJsonAsync("/api/students/records/settings/draft",
			new SaveStudentRecordsSettingsDto { Policy = policy, ConcurrencyStamp = settings.ConcurrencyStamp }, JsonOptions);
		save.StatusCode.ShouldBe(HttpStatusCode.OK, await save.Content.ReadAsStringAsync());
		var draft = (await save.Content.ReadFromJsonAsync<Result<StudentRecordsSettingsDto>>(JsonOptions))!.Data!;
		await PostAsync<StudentRecordsSettingsDto>(actor, "/api/students/records/settings/publish", new PublishStudentRecordsSettingsDto { ConcurrencyStamp = draft.ConcurrencyStamp! });
		var command = new StudentRecordTransitionBatchInput
		{
			AcademicYearId = fixture.Year, IdempotencyKey = Guid.NewGuid().ToString("N"),
			Items = [new() { StudentId = fixture.Student, Outcome = "Withdraw", EffectiveDate = new DateTime(2026, 10, 2), Reason = "Reviewed departure effective at institution midnight" }]
		};
		var row = (await PostAsync<StudentRecordTransitionPreviewDto>(actor, Transitions + "/preview", command)).Items.Single();
		row.Ready.ShouldBeTrue(string.Join("; ", row.Issues.Select(x => x.Code)));
		command.Items[0].StudentConcurrencyStamp = row.StudentConcurrencyStamp; command.Items[0].EnrollmentConcurrencyStamp = row.EnrollmentConcurrencyStamp;
		var transition = (await PostAsync<StudentRecordTransitionBatchResultDto>(actor, Transitions + "/batches", command)).Items.Single();
		transition.Status.ShouldBe("Scheduled");
		var baseline = await ReadTransitionRaceAsync(fixture, transition.Id);
		baseline.Active.ShouldBeTrue(); baseline.StudentStatus.ShouldBe(StudentLifeCycleStatusEnum.Enrolled); baseline.AppliedEvents.ShouldBe(0);
		await TickTransitionOnlyAsync();
		(await ReadTransitionRaceAsync(fixture, transition.Id)).ShouldBe(baseline, "An exit must not remove the roster before its effective institution date.");
		factory.UtcNow = factory.UtcNow.AddSeconds(1);
		factory.Barrier.Arm("UPDATE \"AppStudentEnrollments\"");
		var workers = new[] { TickTransitionOnlyAsync(), TickTransitionOnlyAsync() };
		try { await factory.Barrier.Reached.WaitAsync(TimeSpan.FromSeconds(30)); }
		finally { factory.Barrier.Release(); }
		await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(60));
		factory.Barrier.Connections.Count.ShouldBe(2);
		factory.Barrier.ProviderFailures.Keys.ShouldContain("40001", "Overlapping real application writes must produce a natural provider conflict.");
		var applied = await ReadTransitionRaceAsync(fixture, transition.Id);
		applied.Status.ShouldBe("Applied"); applied.Error.ShouldBeNull(); applied.AppliedAt.ShouldBe(factory.UtcNow);
		applied.Active.ShouldBeFalse(); applied.StudentStatus.ShouldBe(StudentLifeCycleStatusEnum.Left);
		applied.IdentityAndPlacement.ShouldBe(baseline.IdentityAndPlacement); applied.EnrollmentData.ShouldBe(baseline.EnrollmentData);
		applied.SourceEnrollment.ShouldBe(baseline.SourceEnrollment); applied.TargetEnrollment.ShouldBeNull();
		applied.EnrollmentCount.ShouldBe(1); applied.TransitionCount.ShouldBe(1); applied.AppliedEvents.ShouldBe(1); applied.AppliedActor.ShouldBe(fixture.Actor);
		applied.StudentStamp.ShouldNotBe(baseline.StudentStamp); applied.EnrollmentStamp.ShouldNotBe(baseline.EnrollmentStamp); applied.TransitionStamp.ShouldNotBe(baseline.TransitionStamp);
		applied.AfterJson.ShouldNotBeNullOrWhiteSpace();
		await TickTransitionOnlyAsync();
		(await ReadTransitionRaceAsync(fixture, transition.Id)).ShouldBe(applied, "A later worker tick must preserve the winning state, audit history and source stamps.");
	}
	private async Task TickTransitionOnlyAsync()
	{
		using var scope = factory.Services.CreateScope(); var provider = scope.ServiceProvider;
		await new TransitionWorker(provider.GetRequiredService<AbpAsyncTimer>(), provider.GetRequiredService<IServiceScopeFactory>()).TickAsync(provider);
	}
	private Task<TransitionRaceState> ReadTransitionRaceAsync(Fixture fixture, Guid transitionId) => InTenantAsync(async services =>
	{
		var student = await (await services.GetRequiredService<IStudentRepository>().GetQueryableAsync()).Where(x => x.Id == fixture.Student)
			.Select(x => new { x.ConcurrencyStamp, x.Status, x.AcademicYearId, x.StandardId, x.DivisionId, x.GrNumber, x.GrNo, x.RollNo, x.FirstName, x.MiddleName, x.LastName }).SingleAsync();
		var enrollments = services.GetRequiredService<IRepository<StudentEnrollment, Guid>>();
		var enrollment = await (await enrollments.GetQueryableAsync()).Where(x => x.StudentId == fixture.Student && x.AcademicYearId == fixture.Year)
			.Select(x => new { x.Id, x.ConcurrencyStamp, x.IsActive, x.AcademicYearId, x.StandardId, x.DivisionId, x.RollNumber }).SingleAsync();
		var transitions = services.GetRequiredService<IRepository<StudentRecordTransition, Guid>>(); var transition = await transitions.GetAsync(transitionId);
		var history = await (await services.GetRequiredService<IRepository<StudentRecordTransitionEvent, Guid>>().GetQueryableAsync())
			.Where(x => x.TransitionId == transitionId).OrderBy(x => x.Id).Select(x => new { x.Id, x.ActorId, x.Action, x.Stage, x.Remarks, x.CreationTime }).ToListAsync();
		var applied = history.SingleOrDefault(x => x.Action == "Applied");
		return new TransitionRaceState(transition.Status, transition.LastError, transition.AppliedAt, transition.ConcurrencyStamp, student.ConcurrencyStamp, student.Status,
			JsonSerializer.Serialize(new { student.AcademicYearId, student.StandardId, student.DivisionId, student.GrNumber, student.GrNo, student.RollNo, student.FirstName, student.MiddleName, student.LastName }),
			enrollment.ConcurrencyStamp, enrollment.IsActive, JsonSerializer.Serialize(new { enrollment.Id, enrollment.AcademicYearId, enrollment.StandardId, enrollment.DivisionId, enrollment.RollNumber }),
			transition.SourceEnrollmentId, transition.TargetEnrollmentId, transition.AfterJson, JsonSerializer.Serialize(history), history.LongCount(x => x.Action == "Applied"), applied?.ActorId,
			await enrollments.CountAsync(x => x.StudentId == fixture.Student), await transitions.CountAsync(x => x.StudentId == fixture.Student));
	});
	private sealed record TransitionRaceState(string Status, string? Error, DateTime? AppliedAt, string TransitionStamp, string StudentStamp, StudentLifeCycleStatusEnum StudentStatus,
		string IdentityAndPlacement, string EnrollmentStamp, bool Active, string EnrollmentData, Guid? SourceEnrollment, Guid? TargetEnrollment, string? AfterJson,
		string History, long AppliedEvents, Guid? AppliedActor, long EnrollmentCount, long TransitionCount);
}
