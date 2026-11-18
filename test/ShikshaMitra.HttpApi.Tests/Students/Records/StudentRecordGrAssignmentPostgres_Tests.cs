using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
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

namespace ShikshaMitra.Students.Records;

public sealed partial class StudentRecordsApplicationPostgres_Tests
{
	private const string GrChanges = "/api/students/records/changes";
	private const string RegisterReference = "General Register, page 12, entry 71";

	[StudentRecordsApplicationPostgresFact]
	public async Task Record_postgres_GR_manual_and_auto_require_independent_approval_and_preserve_unique_numbers()
	{
		Client.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		var fixture = await SeedGrStudentsAsync();
		using var reviewer = CreateClientAs(fixture.Reviewer);
		reviewer.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		foreach (var student in new[] { fixture.Manual, fixture.Auto, fixture.Competing })
		{
			var historical = await PostResultAsync<StudentLegacyOverrideDto>(Client,
				$"/api/students/records/directory/{student}/legacy-overrides",
				new RequestStudentLegacyOverrideDto { Reason = "Verified synthetic school register for the GR assignment test" });
			var approved = await PostResultAsync<StudentLegacyOverrideDto>(reviewer,
				$"/api/students/records/directory/legacy-overrides/{historical.Id}/approve",
				new DecideStudentLegacyOverrideDto { ConcurrencyStamp = historical.ConcurrencyStamp, Remarks = "Independent register review" });
			approved.Status.ShouldBe("Approved");
		}
		await PublishGrSeriesAsync(fixture.Year);

		var manual = await ReadGrSnapshotAsync(fixture.Manual);
		await AssertGrErrorAsync(GrChanges, GrInput(fixture.Manual, manual, "manual", "00071"), "Students:Records:GrRegisterReferenceRequired");
		await AssertGrErrorAsync(GrChanges, GrInput(fixture.Manual, manual, "manual", "00024", RegisterReference),
			StudentOnboardingErrorCodes.IdentifierAlreadyAllocated);
		var manualInput = GrInput(fixture.Manual, manual, "manual", "00071", RegisterReference);
		var manualRequest = await PostGrChangeAsync(Client, GrChanges, manualInput);
		manualRequest.Status.ShouldBe("Pending");
		manualRequest.RequiredApprovals.ShouldBe(1);
		manualRequest.CanApprove.ShouldBeFalse();
		await AssertGrErrorAsync($"{GrChanges}/{manualRequest.Id}/approve", GrDecision(manualRequest), "Students:Records:ApproverRequired");
		await AssertStoredGrAsync(fixture.Manual, null, null);
		var appliedManual = await ApproveGrAsync(reviewer, manualRequest);
		appliedManual.Status.ShouldBe("Applied");
		appliedManual.Decisions.ShouldHaveSingleItem().ActorId.ShouldBe(fixture.Reviewer);
		appliedManual.Proposed!.Value.GetProperty("registerReference").GetString().ShouldBe(RegisterReference);
		await AssertStoredGrAsync(fixture.Manual, "00071", 71);
		(await PostGrChangeAsync(Client, GrChanges, manualInput)).Id.ShouldBe(manualRequest.Id);

		var automatic = await ReadGrSnapshotAsync(fixture.Auto);
		var preview = automatic.Data.GetProperty("autoGrNumber").GetString()!;
		preview.ShouldBe("PG-GR-1001");
		(await ReadGrSnapshotAsync(fixture.Auto)).Data.GetProperty("autoGrNumber").GetString().ShouldBe(preview);
		var staleRequest = await PostGrChangeAsync(Client, GrChanges, GrInput(fixture.Auto, automatic, "auto", preview));
		staleRequest.Status.ShouldBe("Pending");
		staleRequest.RequiredApprovals.ShouldBe(1);
		await AssertStoredGrAsync(fixture.Auto, null, null);

		var competing = await ReadGrSnapshotAsync(fixture.Competing);
		var competingRequest = await PostGrChangeAsync(Client, GrChanges,
			GrInput(fixture.Competing, competing, "manual", preview, "General Register, page 31, late entry"));
		(await ApproveGrAsync(reviewer, competingRequest)).Status.ShouldBe("Applied");
		var conflict = await ApproveGrAsync(reviewer, staleRequest);
		conflict.Status.ShouldBe("Conflict");
		conflict.ExceptionCode.ShouldBe(StudentOnboardingErrorCodes.IdentifierAlreadyAllocated);
		await AssertStoredGrAsync(fixture.Auto, null, null);
		await AssertStoredGrAsync(fixture.Competing, preview, null);

		var refreshed = await ReadGrSnapshotAsync(fixture.Auto);
		var next = refreshed.Data.GetProperty("autoGrNumber").GetString()!;
		next.ShouldBe("PG-GR-1002");
		var autoRequest = await PostGrChangeAsync(Client, GrChanges, GrInput(fixture.Auto, refreshed, "auto", next));
		autoRequest.Status.ShouldBe("Pending");
		var appliedAuto = await ApproveGrAsync(reviewer, autoRequest);
		appliedAuto.Status.ShouldBe("Applied");
		appliedAuto.Decisions.ShouldHaveSingleItem().ActorId.ShouldBe(fixture.Reviewer);
		await AssertStoredGrAsync(fixture.Auto, next, null);
		output.WriteLine("Authenticated Records HTTP requests verified manual and automatic GR application, independent approval, legacy numeric equivalence, a competing allocation at approval, and fresh retry on PostgreSQL.");
	}

	private async Task<StudentRecordSectionSnapshotDto> ReadGrSnapshotAsync(Guid student)
	{
		using var response = await Client.GetAsync($"{GrChanges}/students/{student}/snapshot/gr-number");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<StudentRecordSectionSnapshotDto>(JsonOptions))!;
	}

	private static CreateStudentRecordChangeDto GrInput(Guid student, StudentRecordSectionSnapshotDto snapshot, string mode, string number, string? reference = null) => new()
	{
		StudentId = student,
		Section = "gr-number",
		ExpectedSnapshot = snapshot.Snapshot,
		ClientKey = Guid.NewGuid().ToString("N"),
		Reason = "Verified missing GR on the active school record",
		Payload = JsonSerializer.SerializeToElement(new { mode, grNumber = number, registerReference = reference })
	};

	private static StudentRecordDecisionDto GrDecision(StudentRecordChangeDto request) => new()
	{
		ConcurrencyStamp = request.ConcurrencyStamp,
		Reason = "Reviewed the permanent register assignment"
	};

	private static Task<StudentRecordChangeDto> ApproveGrAsync(HttpClient reviewer, StudentRecordChangeDto request) =>
		PostGrChangeAsync(reviewer, $"{GrChanges}/{request.Id}/approve", GrDecision(request));

	private static async Task<StudentRecordChangeDto> PostGrChangeAsync(HttpClient client, string route, object input)
	{
		using var response = await client.PostAsJsonAsync(route, input, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		return (await response.Content.ReadFromJsonAsync<StudentRecordChangeDto>(JsonOptions))!;
	}

	private async Task AssertGrErrorAsync(string route, object input, string code)
	{
		using var response = await Client.PostAsJsonAsync(route, input, JsonOptions);
		var body = await response.Content.ReadAsStringAsync();
		((int)response.StatusCode).ShouldBeInRange(400, 499, body);
		body.ShouldContain(code);
	}

	private async Task PublishGrSeriesAsync(Guid year)
	{
		const string configurations = "/api/student/onboarding/settings/configurations";
		string GrConfiguration(string status, bool active) => JsonSerializer.Serialize(new
		{
			readinessAccess = new { },
			onboardingProfiles = Array.Empty<object>(),
			grNumber = new { assignmentMode = "both", configurations = new[] { new
			{
				id = "pg-records-gr", name = "Records GR", status, isActive = active, version = 1,
				boardMode = "all", mediumMode = "all", stageMode = "all", classMode = "all", divisionMode = "all",
				startingNumber = 1001, padding = 4, prefix = "PG-GR-", formatPattern = "{PREFIX}{SEQUENCE}{SUFFIX}", manualEntryAllowed = true
			} } },
			rollNumber = new { configurations = Array.Empty<object>() },
			divisionChange = new { configurations = Array.Empty<object>() }
		});
		using var draftResponse = await Client.PutAsJsonAsync(configurations, new SaveStudentOnboardingSettingsDto
		{
			AcademicYearId = year,
			ConfigurationJson = GrConfiguration("draft", false)
		}, JsonOptions);
		draftResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await draftResponse.Content.ReadAsStringAsync());
		var draft = (await draftResponse.Content.ReadFromJsonAsync<Result<StudentOnboardingSettingsDto>>(JsonOptions))!.Data!;
		using var publication = await Client.PutAsJsonAsync(configurations, new SaveStudentOnboardingSettingsDto
		{
			AcademicYearId = year,
			ConfigurationJson = GrConfiguration("published", true),
			ConcurrencyStamp = draft.ConcurrencyStamp
		}, JsonOptions);
		publication.StatusCode.ShouldBe(HttpStatusCode.OK, await publication.Content.ReadAsStringAsync());
	}

	private Task<GrFixture> SeedGrStudentsAsync() => InTenantAsync<GrFixture>(async services =>
	{
		var years = services.GetRequiredService<IRepository<AcademicYear, Guid>>();
		var baseline = await years.GetAsync(Factory.SeededAcademicYearId);
		var year = await services.GetRequiredService<AcademicYearManager>().CreateAsync("Records PG GR", baseline.StartDate, baseline.EndDate);
		await years.InsertAsync(year, true);
		var board = await services.GetRequiredService<EducationBoardManager>().CreateAsync(year.Id, EducationBoardType.CBSE);
		await services.GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(board, true);
		var medium = await services.GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, "Records PG GR English");
		await services.GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, true);
		var stage = await services.GetRequiredService<EducationMediumStageManager>().CreateAsync(medium.Id, "Records PG GR Primary");
		await services.GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
		var standard = await services.GetRequiredService<StandardManager>().CreateAsync(stage.Id, "Records PG GR Class", 1, null);
		await services.GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(standard, true);
		var division = await services.GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "Records PG GR", 30);
		await services.GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, true);
		var reviewer = new IdentityUser(Guid.NewGuid(), "records-pg-gr-reviewer", "records-pg-gr-reviewer@example.test", TenantId);
		await services.GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(reviewer, true);
		(await services.GetRequiredService<IdentityUserManager>().AddToRoleAsync(reviewer, "admin")).Succeeded.ShouldBeTrue();
		await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(
			new UserScopeAssignment(Guid.NewGuid(), TenantId, userId: reviewer.Id, description: "Isolated GR reviewer"), true);

		var manual = await AddStudentAsync("Manual", 1);
		var automatic = await AddStudentAsync("Auto", 2);
		var competing = await AddStudentAsync("Competing", 3);
		await AddStudentAsync("Legacy", 4, 24);
		return new(year.Id, reviewer.Id, manual, automatic, competing);

		async Task<Guid> AddStudentAsync(string name, int roll, int? legacyGr = null)
		{
			var student = new Student(Guid.NewGuid(), TenantId, name, null, "Register", year.Id, board.Id,
				medium.Id, stage.Id, standard.Id, division.Id, status: StudentLifeCycleStatusEnum.Enrolled);
			if (legacyGr.HasValue) student.AssignGrNo(legacyGr);
			student.AssignRollNo(roll);
			await services.GetRequiredService<IStudentRepository>().InsertAsync(student, true);
			await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().InsertAsync(
				StudentEnrollment.Create(Guid.NewGuid(), TenantId, student.Id, year.Id, student.StandardId, student.DivisionId, roll), true);
			return student.Id;
		}
	});

	private Task<bool> AssertStoredGrAsync(Guid id, string? value, int? legacyValue) => InTenantAsync(async services =>
	{
		var student = await services.GetRequiredService<IStudentRepository>().GetAsync(id);
		student.GrNumber.ShouldBe(value);
		student.GrNo.ShouldBe(legacyValue);
		student.Status.ShouldBe(StudentLifeCycleStatusEnum.Enrolled);
		var enrollment = await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().GetAsync(x => x.StudentId == id);
		enrollment.IsActive.ShouldBeTrue();
		enrollment.RollNumber.ShouldBe(student.RollNo);
		return true;
	});

	private sealed record GrFixture(Guid Year, Guid Reviewer, Guid Manual, Guid Auto, Guid Competing);
}
