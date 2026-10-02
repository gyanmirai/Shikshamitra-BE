using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using ShikshaMitra.Students.StudentRecords;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;
using Xunit.Abstractions;

namespace ShikshaMitra.Students.Records;

[Trait("Database", "PostgreSQL")]
public sealed partial class StudentRecordsApplicationPostgres_Tests(StudentRecordsApplicationPostgresTestFactory factory, ITestOutputHelper output)
	: ShikshaMitraApiTestBase(factory), IClassFixture<StudentRecordsApplicationPostgresTestFactory>
{
	private const string Transitions = "/api/students/records/transitions";
	private static readonly Guid TenantId = Guid.Parse(TestAuthHandler.TestTenantId);

	[StudentRecordsApplicationPostgresFact]
	public async Task Record_postgres_real_commands_resolve_concurrent_roll_and_capacity_without_loser_mutation_and_replay_once()
	{
		Client.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		var fixture = await SeedHistoricalStudentsAndTargetAsync();
		using var reviewer = CreateClientAs(fixture.Reviewer); reviewer.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		foreach (var student in fixture.Students)
		{
			var requested = await PostResultAsync<StudentLegacyOverrideDto>(Client, $"/api/students/records/directory/{student}/legacy-overrides",
				new RequestStudentLegacyOverrideDto { Reason = "Verified synthetic historical school register for provider concurrency test" });
			var approved = await PostResultAsync<StudentLegacyOverrideDto>(reviewer, $"/api/students/records/directory/legacy-overrides/{requested.Id}/approve",
				new DecideStudentLegacyOverrideDto { ConcurrencyStamp = requested.ConcurrencyStamp, Remarks = "Reviewed historical enrollment evidence" });
			approved.Status.ShouldBe("Approved");
		}
		var policy = StudentRecordsPolicy.Starter(); policy.Changes["Placement"].Mode = "Direct"; policy.Transitions.AnnualRenewalMode = "DirectEnrollment";
		using var save = await Client.PutAsJsonAsync("/api/students/records/settings/draft", new SaveStudentRecordsSettingsDto { Policy = policy }, JsonOptions);
		save.StatusCode.ShouldBe(HttpStatusCode.OK, await save.Content.ReadAsStringAsync());
		var draft = (await save.Content.ReadFromJsonAsync<Result<StudentRecordsSettingsDto>>(JsonOptions))!.Data!;
		(await PostResultAsync<StudentRecordsSettingsDto>(Client, "/api/students/records/settings/publish",
			new PublishStudentRecordsSettingsDto { ConcurrencyStamp = draft.ConcurrencyStamp! })).PublishedVersionId.ShouldNotBeNull();
		using var numbering = await Client.PutAsJsonAsync("/api/student/onboarding/settings/configurations", new SaveStudentOnboardingSettingsDto
		{
			AcademicYearId = fixture.TargetYear,
			ConfigurationJson = JsonSerializer.Serialize(new
			{
				readinessAccess = new { }, onboardingProfiles = Array.Empty<object>(), grNumber = new { configurations = Array.Empty<object>() }, divisionChange = new { configurations = Array.Empty<object>() },
				rollNumber = new { assignmentMode = "automatic", configurations = new[] { new { id = "pg-records-roll", boardMode = "all", mediumMode = "all", stageMode = "all", classMode = "all", divisionMode = "all", startingNumber = 40, numberingScope = "class-division", arrangeBy = "studentName", arrangeType = "ascending", preserveExisting = true, midYearHandling = "next-available" } } }
			})
		}, JsonOptions);
		numbering.StatusCode.ShouldBe(HttpStatusCode.OK, await numbering.Content.ReadAsStringAsync());
		var commands = fixture.Students.Select(id => new StudentRecordTransitionBatchInput
		{
			AcademicYearId = fixture.SourceYear, IdempotencyKey = "pg-records-" + id.ToString("N"),
			Items = [new() { StudentId = id, Outcome = "Promote", TargetAcademicYearId = fixture.TargetYear, TargetStandardId = fixture.TargetStandard,
				TargetDivisionId = fixture.TargetDivision, RollAssignmentMode = "automatic", EffectiveDate = AdmissionCycleWindowEvaluator.GetInstitutionDate(DateTime.UtcNow), Reason = "Reviewed annual target placement" }]
		}).ToArray();
		foreach (var command in commands)
		{
			var row = (await PostResultAsync<StudentRecordTransitionPreviewDto>(Client, Transitions + "/preview", command)).Items.Single();
			row.Ready.ShouldBeTrue(string.Join("; ", row.Issues.Select(x => x.Code))); row.ProposedRollNumber.ShouldBe(40);
			command.Items[0].RollNumber = row.ProposedRollNumber; command.Items[0].StudentConcurrencyStamp = row.StudentConcurrencyStamp; command.Items[0].EnrollmentConcurrencyStamp = row.EnrollmentConcurrencyStamp;
		}
		var before = await InTenantAsync(async services =>
		{
			var students = await services.GetRequiredService<IStudentRepository>().GetListAsync(s => Enumerable.Contains(fixture.Students, s.Id));
			return students.ToDictionary(s => s.Id, s => (s.AcademicYearId, s.GrNumber, s.ConcurrencyStamp));
		});
		factory.Barrier.Arm();
		var requests = commands.Select(command => Client.PostAsJsonAsync(Transitions + "/batches", command, JsonOptions)).ToArray();
		try { await factory.Barrier.Reached.WaitAsync(TimeSpan.FromSeconds(30)); }
		finally { factory.Barrier.Release(); }
		var responses = await Task.WhenAll(requests).WaitAsync(TimeSpan.FromSeconds(60));
		try
		{
			factory.Barrier.Connections.Count.ShouldBe(2, "Concurrent real SERIALIZABLE commands must use distinct PostgreSQL connections.");
			factory.Barrier.ProviderFailures.Keys.ShouldContain("40001", "PostgreSQL must naturally report a serialization conflict; no exception is injected.");
			responses.Count(x => x.StatusCode == HttpStatusCode.OK).ShouldBe(1);
			var winner = Array.FindIndex(responses, x => x.StatusCode == HttpStatusCode.OK); var loser = 1 - winner;
			responses[loser].StatusCode.ShouldBe(HttpStatusCode.BadRequest, await responses[loser].Content.ReadAsStringAsync());
			(await responses[loser].Content.ReadAsStringAsync()).ShouldContain("Students:Records:RollProposalChanged");
			var result = (await responses[winner].Content.ReadFromJsonAsync<Result<StudentRecordTransitionBatchResultDto>>(JsonOptions))!.Data!;
			result.Items.Single().Status.ShouldBe("Applied"); result.Items.Single().RollNumber.ShouldBe(40);
			var replay = await PostResultAsync<StudentRecordTransitionBatchResultDto>(Client, Transitions + "/batches", commands[winner]);
			replay.BatchId.ShouldBe(result.BatchId); replay.Items.Single().Id.ShouldBe(result.Items.Single().Id);
			await InTenantAsync(async services =>
			{
				var enrollments = services.GetRequiredService<IRepository<StudentEnrollment, Guid>>();
				var target = await enrollments.GetListAsync(e => e.AcademicYearId == fixture.TargetYear && e.DivisionId == fixture.TargetDivision && e.IsActive);
				target.Count.ShouldBe(1); target.Single().StudentId.ShouldBe(commands[winner].Items[0].StudentId); target.Single().RollNumber.ShouldBe(40);
				var students = services.GetRequiredService<IStudentRepository>();
				foreach (var studentId in fixture.Students) (await students.GetAsync(studentId)).GrNumber.ShouldBe(before[studentId].GrNumber);
				var losingStudent = await students.GetAsync(commands[loser].Items[0].StudentId);
				losingStudent.AcademicYearId.ShouldBe(before[losingStudent.Id].AcademicYearId); losingStudent.ConcurrencyStamp.ShouldBe(before[losingStudent.Id].ConcurrencyStamp);
				(await enrollments.GetAsync(e => e.StudentId == losingStudent.Id && e.AcademicYearId == fixture.SourceYear)).IsActive.ShouldBeTrue();
				(await enrollments.CountAsync(e => e.StudentId == losingStudent.Id && e.AcademicYearId == fixture.TargetYear)).ShouldBe(0);
				(await services.GetRequiredService<IRepository<StudentRecordTransitionBatch, Guid>>().CountAsync(b => b.IdempotencyKey == commands[loser].IdempotencyKey)).ShouldBe(0);
				(await services.GetRequiredService<IRepository<StudentRecordTransition, Guid>>().CountAsync(t => Enumerable.Contains(fixture.Students, t.StudentId))).ShouldBe(1);
				return true;
			});
			var retryPreview = await PostResultAsync<StudentRecordTransitionPreviewDto>(Client, Transitions + "/preview", commands[loser]);
			retryPreview.CanSubmit.ShouldBeFalse(); retryPreview.Items.Single().Issues.ShouldContain(x => x.Code == StudentOnboardingErrorCodes.DivisionFull,
				"Capacity preview must preserve the owning error code. Actual codes: " + string.Join(", ", retryPreview.Items.Single().Issues.Select(x => x.Code)));
			output.WriteLine("Natural PostgreSQL 40001 through two real HTTP Records submissions: one permanent roll/seat assignment, loser rolled back, winning request replayed once.");
		}
		finally { foreach (var response in responses) response.Dispose(); }
	}

	private async Task<Fixture> SeedHistoricalStudentsAndTargetAsync() => await InTenantAsync<Fixture>(async services =>
	{
		var years = services.GetRequiredService<IRepository<AcademicYear, Guid>>();
		var sourceBoard = await services.GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(factory.SeededBoardId);
		var sourceYear = await years.GetAsync(sourceBoard.AcademicYearId);
		var sourceDivision = await services.GetRequiredService<DivisionManager>().CreateAsync(factory.SeededStandardId, "Records PG source", 30);
		await services.GetRequiredService<IRepository<Division, Guid>>().InsertAsync(sourceDivision, true);
		var targetYear = await services.GetRequiredService<AcademicYearManager>().CreateAsync("Records PG " + Guid.NewGuid().ToString("N"), sourceYear.StartDate.AddYears(1), sourceYear.EndDate.AddYears(1));
		await years.InsertAsync(targetYear, true);
		var board = await services.GetRequiredService<EducationBoardManager>().CreateAsync(targetYear.Id, EducationBoardType.CBSE);
		await services.GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(board, true);
		var medium = await services.GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, "Records PG English");
		await services.GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, true);
		var stage = await services.GetRequiredService<EducationMediumStageManager>().CreateAsync(medium.Id, "Records PG Primary");
		await services.GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
		var standard = await services.GetRequiredService<StandardManager>().CreateAsync(stage.Id, "Records PG Target", 6, null);
		await services.GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(standard, true);
		var division = await services.GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "Records PG seat", 1);
		await services.GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, true);
		var reviewerId = Guid.NewGuid(); await services.GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(new IdentityUser(reviewerId, "records-pg-reviewer", "records-pg-reviewer@example.test", TenantId), true);
		await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TenantId, userId: reviewerId, description: "Isolated provider reviewer"), true);
		var studentIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
		for (var index = 0; index < studentIds.Length; index++)
		{
			var student = new Student(studentIds[index], TenantId, index == 0 ? "Asha" : "Bhavya", null, "Provider", sourceYear.Id, sourceBoard.Id, factory.SeededMediumId,
				factory.SeededMediumStageId, factory.SeededStandardId, sourceDivision.Id, status: StudentLifeCycleStatusEnum.Enrolled);
			student.AssignGrNumber("PG-RECORDS-" + index); student.AssignRollNo(index + 1);
			await services.GetRequiredService<IStudentRepository>().InsertAsync(student, true);
			await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().InsertAsync(StudentEnrollment.Create(Guid.NewGuid(), TenantId, student.Id, sourceYear.Id, student.StandardId, student.DivisionId, index + 1), true);
		}
		return new(sourceYear.Id, targetYear.Id, standard.Id, division.Id, reviewerId, studentIds);
	});
	private async Task<T> PostResultAsync<T>(HttpClient client, string url, object input)
	{
		using var response = await client.PostAsJsonAsync(url, input, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<Result<T>>(JsonOptions))!.Data!;
	}
	private async Task<T> InTenantAsync<T>(Func<IServiceProvider, Task<T>> action)
	{
		using var scope = factory.Services.CreateScope();
		using var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(TenantId);
		using var work = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true);
		var result = await action(scope.ServiceProvider); await work.CompleteAsync(); return result;
	}
	private sealed record Fixture(Guid SourceYear, Guid TargetYear, Guid TargetStandard, Guid TargetDivision, Guid Reviewer, Guid[] Students);
}
