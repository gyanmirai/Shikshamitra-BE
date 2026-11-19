using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
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
	public async Task Record_postgres_two_actual_workers_apply_one_due_change_without_overwriting_winning_result()
	{
		factory.UtcNow = new DateTime(2026, 10, 1, 18, 29, 59, DateTimeKind.Utc);
		var fixture = await SeedAsync();
		await InTenantAsync(async services =>
		{
			await services.GetRequiredService<IRepository<StudentCommunicationDetail, Guid>>().InsertAsync(new StudentCommunicationDetail(fixture.Student, TenantId), true);
			return true;
		});
		using var actor = CreateClientAs(fixture.Actor); actor.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		using var reviewer = CreateClientAs(fixture.Reviewer); reviewer.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		var legacy = await PostAsync<StudentLegacyOverrideDto>(actor, $"/api/students/records/directory/{fixture.Student}/legacy-overrides",
			new RequestStudentLegacyOverrideDto { Reason = "Verified synthetic register for overlapping scheduled workers" });
		await PostAsync<StudentLegacyOverrideDto>(reviewer, $"/api/students/records/directory/legacy-overrides/{legacy.Id}/approve",
			new DecideStudentLegacyOverrideDto { ConcurrencyStamp = legacy.ConcurrencyStamp, Remarks = "Independent source register review" });
		var settings = await GetAsync<StudentRecordsSettingsDto>(actor, "/api/students/records/settings");
		using var save = await actor.PutAsJsonAsync("/api/students/records/settings/draft",
			new SaveStudentRecordsSettingsDto { Policy = StudentRecordsPolicy.Starter(), ConcurrencyStamp = settings.ConcurrencyStamp }, JsonOptions);
		save.StatusCode.ShouldBe(HttpStatusCode.OK, await save.Content.ReadAsStringAsync());
		var draft = (await save.Content.ReadFromJsonAsync<Result<StudentRecordsSettingsDto>>(JsonOptions))!.Data!;
		await PostAsync<StudentRecordsSettingsDto>(actor, "/api/students/records/settings/publish", new PublishStudentRecordsSettingsDto { ConcurrencyStamp = draft.ConcurrencyStamp! });
		var snapshotUrl = Changes + $"/students/{fixture.Student}/snapshot/communication";
		var before = await GetAsync<StudentRecordSectionSnapshotDto>(actor, snapshotUrl);
		var payload = JsonNode.Parse(before.Data.GetRawText())!.AsObject(); payload["emergencyContactName"] = "Verified concurrent worker contact";
		var change = await PostAsync<StudentRecordChangeDto>(actor, Changes, new CreateStudentRecordChangeDto
		{
			StudentId = fixture.Student, Section = "communication", ClientKey = Guid.NewGuid().ToString("N"),
			Reason = "Scheduled contact correction through source owner", ExpectedSnapshot = before.Snapshot,
			Payload = JsonSerializer.SerializeToElement(payload), EffectiveAt = new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.FromHours(5.5))
		});
		change.Status.ShouldBe("Scheduled");
		var baseline = await ReadChangeRaceAsync(fixture.Student, change.Id);
		factory.UtcNow = factory.UtcNow.AddSeconds(1);
		factory.Barrier.Arm("UPDATE \"StudentCommunicationDetails\"");
		var workers = new[] { TickChangeOnlyAsync(), TickChangeOnlyAsync() };
		try { await factory.Barrier.Reached.WaitAsync(TimeSpan.FromSeconds(30)); }
		finally { factory.Barrier.Release(); }
		await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(60));
		factory.Barrier.Connections.Count.ShouldBe(2);
		factory.Barrier.ProviderFailures.Keys.ShouldContain("40001", "Two real worker applications must cause the provider conflict without exception injection.");
		var applied = await ReadChangeRaceAsync(fixture.Student, change.Id);
		applied.Status.ShouldBe("Applied"); applied.Error.ShouldBeNull(); applied.AppliedAt.ShouldBe(new DateTimeOffset(factory.UtcNow));
		applied.ContactName.ShouldBe("Verified concurrent worker contact");
		applied.StudentData.ShouldBe(baseline.StudentData); applied.EnrollmentStamp.ShouldBe(baseline.EnrollmentStamp);
		applied.ChangeCount.ShouldBe(1); applied.DecisionCount.ShouldBe(0); applied.ContactCount.ShouldBe(1);
		applied.ChangeStamp.ShouldNotBe(baseline.ChangeStamp); applied.ContactModifiedAt.ShouldNotBe(baseline.ContactModifiedAt);
		await TickChangeOnlyAsync();
		(await ReadChangeRaceAsync(fixture.Student, change.Id)).ShouldBe(applied, "A repeated worker tick must preserve the winning result and source stamps.");
	}
	private async Task TickChangeOnlyAsync()
	{
		using var scope = factory.Services.CreateScope(); var provider = scope.ServiceProvider;
		await new ChangeWorker(provider.GetRequiredService<AbpAsyncTimer>(), provider.GetRequiredService<IServiceScopeFactory>()).TickAsync(provider);
	}
	private Task<ChangeRaceState> ReadChangeRaceAsync(Guid studentId, Guid changeId) => InTenantAsync(async services =>
	{
		var student = await (await services.GetRequiredService<IStudentRepository>().GetQueryableAsync()).Where(x => x.Id == studentId)
			.Select(x => new { x.ConcurrencyStamp, x.Status, x.AcademicYearId, x.StandardId, x.DivisionId, x.GrNumber, x.GrNo, x.RollNo, x.FirstName, x.MiddleName, x.LastName }).SingleAsync();
		var enrollment = await (await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().GetQueryableAsync()).Where(x => x.StudentId == studentId).Select(x => x.ConcurrencyStamp).SingleAsync();
		var change = await services.GetRequiredService<IRepository<StudentRecordChange, Guid>>().GetAsync(changeId);
		var communication = services.GetRequiredService<IRepository<StudentCommunicationDetail, Guid>>(); var contact = await communication.GetAsync(studentId);
		var studentData = JsonSerializer.Serialize(new { student.Status, student.AcademicYearId, student.StandardId, student.DivisionId, student.GrNumber, student.GrNo, student.RollNo, student.FirstName, student.MiddleName, student.LastName });
		return new ChangeRaceState(change.Status, change.ExceptionCode, change.AppliedAt, change.ConcurrencyStamp, contact.LastModificationTime, student.ConcurrencyStamp, studentData, enrollment,
			contact.EmergencyContactName, await services.GetRequiredService<IRepository<StudentRecordChange, Guid>>().CountAsync(x => x.StudentId == studentId),
			await services.GetRequiredService<IRepository<StudentRecordChangeDecision, Guid>>().CountAsync(x => x.ChangeId == changeId), await communication.CountAsync(x => x.Id == studentId));
	});
	private sealed record ChangeRaceState(string Status, string? Error, DateTimeOffset? AppliedAt, string ChangeStamp, DateTime? ContactModifiedAt,
		string StudentStamp, string StudentData, string EnrollmentStamp, string? ContactName, long ChangeCount, long DecisionCount, long ContactCount);
}
