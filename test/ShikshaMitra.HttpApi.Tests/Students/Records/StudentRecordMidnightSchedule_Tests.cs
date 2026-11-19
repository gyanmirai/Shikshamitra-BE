using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using ShikshaMitra.BackgroundWorkers;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using Shouldly;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Records;

public sealed class StudentRecordMidnightScheduleTestFactory : StudentRecordsApplicationPostgresTestFactory
{
	public DateTime UtcNow { get; set; } = new(2026, 10, 1, 18, 29, 59, DateTimeKind.Utc);
	protected override void ConfigureWebHost(IWebHostBuilder builder)
	{
		base.ConfigureWebHost(builder);
		builder.ConfigureTestServices(services =>
		{
			var clock = Substitute.For<IClock>();
			clock.Now.Returns(_ => UtcNow); clock.Kind.Returns(DateTimeKind.Utc); clock.SupportsMultipleTimezone.Returns(true);
			clock.Normalize(Arg.Any<DateTime>()).Returns(call => DateTime.SpecifyKind(call.Arg<DateTime>(), DateTimeKind.Utc));
			services.Replace(ServiceDescriptor.Singleton(clock));
		});
	}
}

[Trait("Database", "PostgreSQL")]
public sealed partial class StudentRecordMidnightSchedule_Tests(StudentRecordMidnightScheduleTestFactory factory)
	: ShikshaMitraApiTestBase(factory), IClassFixture<StudentRecordMidnightScheduleTestFactory>
{
	private static readonly Guid TenantId = Guid.Parse(TestAuthHandler.TestTenantId);
	private const string Changes = "/api/students/records/changes";
	private const string Transitions = "/api/students/records/transitions";

	[StudentRecordMidnightPostgresTheory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Record_actual_workers_respect_institution_midnight_recheck_actor_and_do_not_repeat(bool revokeActor)
	{
		factory.UtcNow = new DateTime(2026, 10, 1, 18, 29, 59, DateTimeKind.Utc);
		AdmissionCycleWindowEvaluator.GetInstitutionDate(factory.UtcNow).ShouldBe(new DateTime(2026, 10, 1));
		var fixture = await SeedAsync();
		using var actor = CreateClientAs(fixture.Actor); actor.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		using var reviewer = CreateClientAs(fixture.Reviewer); reviewer.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		var legacy = await PostAsync<StudentLegacyOverrideDto>(actor, $"/api/students/records/directory/{fixture.Student}/legacy-overrides",
			new RequestStudentLegacyOverrideDto { Reason = "Verified historical register for institution midnight test" });
		(await PostAsync<StudentLegacyOverrideDto>(reviewer, $"/api/students/records/directory/legacy-overrides/{legacy.Id}/approve",
			new DecideStudentLegacyOverrideDto { ConcurrencyStamp = legacy.ConcurrencyStamp, Remarks = "Reviewed original historical school register" })).Status.ShouldBe("Approved");
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Status"].Mode = "Direct"; policy.Transitions.RosterRemovalTiming = "EffectiveDate";
		var settings = await GetAsync<StudentRecordsSettingsDto>(actor, "/api/students/records/settings");
		using var save = await actor.PutAsJsonAsync("/api/students/records/settings/draft", new SaveStudentRecordsSettingsDto { Policy = policy, ConcurrencyStamp = settings.ConcurrencyStamp }, JsonOptions);
		save.StatusCode.ShouldBe(HttpStatusCode.OK, await save.Content.ReadAsStringAsync());
		var draft = (await save.Content.ReadFromJsonAsync<Result<StudentRecordsSettingsDto>>(JsonOptions))!.Data!;
		await PostAsync<StudentRecordsSettingsDto>(actor, "/api/students/records/settings/publish", new PublishStudentRecordsSettingsDto { ConcurrencyStamp = draft.ConcurrencyStamp! });
		var snapshotUrl = Changes + $"/students/{fixture.Student}/snapshot/communication";
		var before = await GetAsync<StudentRecordSectionSnapshotDto>(actor, snapshotUrl);
		var payload = JsonNode.Parse(before.Data.GetRawText())!.AsObject(); payload["emergencyContactName"] = "Verified midnight guardian";
		var change = await PostAsync<StudentRecordChangeDto>(actor, Changes, new CreateStudentRecordChangeDto
		{
			StudentId = fixture.Student, Section = "communication", ClientKey = Guid.NewGuid().ToString("N"), Reason = "Scheduled verified contact correction",
			ExpectedSnapshot = before.Snapshot, Payload = JsonSerializer.SerializeToElement(payload), EffectiveAt = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.FromHours(5.5))
		});
		change.Status.ShouldBe("Scheduled");
		var command = new StudentRecordTransitionBatchInput { AcademicYearId = fixture.Year, IdempotencyKey = Guid.NewGuid().ToString("N"),
			Items = [new() { StudentId = fixture.Student, Outcome = "Withdraw", EffectiveDate = new DateTime(2026, 10, 2), Reason = "Verified withdrawal effective at institution midnight" }] };
		var row = (await PostAsync<StudentRecordTransitionPreviewDto>(actor, Transitions + "/preview", command)).Items.Single();
		row.Ready.ShouldBeTrue(string.Join("; ", row.Issues.Select(x => x.Code)));
		command.Items[0].StudentConcurrencyStamp = row.StudentConcurrencyStamp; command.Items[0].EnrollmentConcurrencyStamp = row.EnrollmentConcurrencyStamp;
		var transition = (await PostAsync<StudentRecordTransitionBatchResultDto>(actor, Transitions + "/batches", command)).Items.Single(); transition.Status.ShouldBe("Scheduled");
		var baseline = await ReadAsync(fixture, change.Id, transition.Id);
		await TickAsync();
		(await ReadAsync(fixture, change.Id, transition.Id)).ShouldBe(baseline, "Before 00:00 IST both selectors and real application must leave the student unchanged.");
		if (revokeActor) await InTenantAsync(async services =>
		{
			var users = services.GetRequiredService<IRepository<IdentityUser, Guid>>(); var user = await users.GetAsync(fixture.Actor); user.SetIsActive(false); await users.UpdateAsync(user, true); return true;
		});
		factory.UtcNow = factory.UtcNow.AddSeconds(1);
		AdmissionCycleWindowEvaluator.GetInstitutionDate(factory.UtcNow).ShouldBe(new DateTime(2026, 10, 2));
		await TickAsync();
		var applied = await ReadAsync(fixture, change.Id, transition.Id);
		applied.ChangeStatus.ShouldBe(revokeActor ? "Conflict" : "Applied"); applied.TransitionStatus.ShouldBe(revokeActor ? "Exception" : "Applied");
		if (revokeActor)
		{
			applied.ChangeError.ShouldBe("Students:Records:AuthorityChanged"); applied.TransitionError.ShouldBe("Students:Records:AuthorityChanged");
			applied.StudentStamp.ShouldBe(baseline.StudentStamp); applied.EnrollmentStamp.ShouldBe(baseline.EnrollmentStamp); applied.Active.ShouldBeTrue();
			applied.StudentStatus.ShouldBe(baseline.StudentStatus); applied.AppliedEvents.ShouldBe(0);
			(await GetAsync<StudentRecordSectionSnapshotDto>(reviewer, snapshotUrl)).Snapshot.ShouldBe(before.Snapshot);
		}
		else
		{
			applied.Active.ShouldBeFalse(); applied.StudentStatus.ShouldBe(StudentLifeCycleStatusEnum.Left); applied.AppliedEvents.ShouldBe(1);
			(await GetAsync<StudentRecordSectionSnapshotDto>(actor, snapshotUrl)).Data.GetProperty("emergencyContactName").GetString().ShouldBe("Verified midnight guardian");
		}
		await TickAsync();
		(await ReadAsync(fixture, change.Id, transition.Id)).ShouldBe(applied, "Repeated worker ticks must not mutate completed or authority-conflicted cases or duplicate history.");
	}

	private async Task TickAsync()
	{
		using var scope = factory.Services.CreateScope(); var provider = scope.ServiceProvider;
		await new ChangeWorker(provider.GetRequiredService<AbpAsyncTimer>(), provider.GetRequiredService<IServiceScopeFactory>()).TickAsync(provider);
		await new TransitionWorker(provider.GetRequiredService<AbpAsyncTimer>(), provider.GetRequiredService<IServiceScopeFactory>()).TickAsync(provider);
	}
	private sealed class ChangeWorker(AbpAsyncTimer timer, IServiceScopeFactory scopes) : StudentRecordScheduleWorker(timer, scopes)
	{
		public Task TickAsync(IServiceProvider provider) => DoWorkAsync(new PeriodicBackgroundWorkerContext(provider));
	}
	private sealed class TransitionWorker(AbpAsyncTimer timer, IServiceScopeFactory scopes) : StudentRecordTransitionWorker(timer, scopes)
	{
		public Task TickAsync(IServiceProvider provider) => DoWorkAsync(new PeriodicBackgroundWorkerContext(provider));
	}
	private async Task<Fixture> SeedAsync() => await InTenantAsync(async services =>
	{
		var year = (await services.GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(factory.SeededBoardId)).AcademicYearId;
		var division = await services.GetRequiredService<DivisionManager>().CreateAsync(factory.SeededStandardId, "Midnight " + Guid.NewGuid().ToString("N"), 30);
		await services.GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, true);
		var users = services.GetRequiredService<IRepository<IdentityUser, Guid>>(); var scopes = services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>();
		var actor = Guid.NewGuid(); var reviewer = Guid.NewGuid();
		foreach (var id in new[] { actor, reviewer })
		{
			await users.InsertAsync(new IdentityUser(id, "midnight-" + id.ToString("N"), id.ToString("N") + "@example.test", TenantId), true);
			await scopes.InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TenantId, userId: id, description: "Isolated school midnight reviewer"), true);
		}
		var student = new Student(Guid.NewGuid(), TenantId, "Midnight", null, "Student", year, factory.SeededBoardId, factory.SeededMediumId,
			factory.SeededMediumStageId, factory.SeededStandardId, division.Id, status: StudentLifeCycleStatusEnum.Enrolled);
		student.AssignGrNumber("MIDNIGHT-" + student.Id.ToString("N")); student.AssignRollNo(1);
		await services.GetRequiredService<IStudentRepository>().InsertAsync(student, true);
		await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().InsertAsync(StudentEnrollment.Create(Guid.NewGuid(), TenantId, student.Id, year, student.StandardId, student.DivisionId, 1), true);
		return new Fixture(student.Id, year, actor, reviewer);
	});
	private Task<State> ReadAsync(Fixture fixture, Guid changeId, Guid transitionId) => InTenantAsync(async services =>
	{
		var student = await (await services.GetRequiredService<IStudentRepository>().GetQueryableAsync()).Where(s => s.Id == fixture.Student)
			.Select(s => new { s.ConcurrencyStamp, s.Status }).SingleAsync();
		var enrollment = await (await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().GetQueryableAsync())
			.Where(e => e.StudentId == fixture.Student && e.AcademicYearId == fixture.Year).Select(e => new { e.ConcurrencyStamp, e.IsActive }).SingleAsync();
		var change = await services.GetRequiredService<IRepository<StudentRecordChange, Guid>>().GetAsync(changeId);
		var transition = await services.GetRequiredService<IRepository<StudentRecordTransition, Guid>>().GetAsync(transitionId);
		var history = await services.GetRequiredService<IRepository<StudentRecordTransitionEvent, Guid>>().CountAsync(e => e.TransitionId == transitionId && e.Action == "Applied");
		return new State(change.Status, transition.Status, change.ExceptionCode, transition.LastError, student.ConcurrencyStamp, enrollment.ConcurrencyStamp,
			change.ConcurrencyStamp, transition.ConcurrencyStamp, enrollment.IsActive, student.Status, history);
	});
	private async Task<T> InTenantAsync<T>(Func<IServiceProvider, Task<T>> action)
	{
		using var scope = factory.Services.CreateScope(); using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId);
		using var work = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true);
		var result = await action(scope.ServiceProvider); await work.CompleteAsync(); return result;
	}
	private async Task<T> PostAsync<T>(HttpClient client, string url, object input)
	{
		using var response = await client.PostAsJsonAsync(url, input, JsonOptions); response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		if (url.StartsWith(Changes, StringComparison.Ordinal)) return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
		return (await response.Content.ReadFromJsonAsync<Result<T>>(JsonOptions))!.Data!;
	}
	private async Task<T> GetAsync<T>(HttpClient client, string url)
	{
		using var response = await client.GetAsync(url); response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		if (url.StartsWith(Changes, StringComparison.Ordinal)) return (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;
		return (await response.Content.ReadFromJsonAsync<Result<T>>(JsonOptions))!.Data!;
	}
	private sealed record Fixture(Guid Student, Guid Year, Guid Actor, Guid Reviewer);
	private sealed record State(string ChangeStatus, string TransitionStatus, string? ChangeError, string? TransitionError, string StudentStamp, string EnrollmentStamp,
		string ChangeStamp, string TransitionStamp, bool Active, StudentLifeCycleStatusEnum StudentStatus, long AppliedEvents);
}

public sealed class StudentRecordMidnightPostgresTheoryAttribute : TheoryAttribute
{
	public StudentRecordMidnightPostgresTheoryAttribute()
	{
		if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(StudentRecordsApplicationPostgresTestFactory.ConnectionVariable)))
			Skip = "Requires a fresh loopback records_tests_* database name in STUDENT_RECORDS_APPLICATION_POSTGRES.";
	}
}

