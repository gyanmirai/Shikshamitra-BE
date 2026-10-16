using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments.Onboarding;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace ShikshaMitra.Students.Records;

public sealed partial class StudentRecordsApplicationPostgres_Tests
{
	[StudentRecordsApplicationPostgresFact]
	public async Task Roll_batch_Postgres_persists_jsonb_preview_and_rejects_a_competing_frozen_roster()
	{
		var fixture = await SeedOnboardingWorklistReadModelsAsync();
		var actor = await InTenantAsync(async services => {
			var users = services.GetRequiredService<IdentityUserManager>(); var roles = services.GetRequiredService<IdentityRoleManager>();
			if (!await roles.RoleExistsAsync("admin")) (await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), "admin", TenantId))).Succeeded.ShouldBeTrue();
			var user = new IdentityUser(Guid.NewGuid(), "roll-pg-admin", "roll-pg-admin@example.test", TenantId);
			(await users.CreateAsync(user)).Succeeded.ShouldBeTrue(); (await users.AddToRoleAsync(user, "admin")).Succeeded.ShouldBeTrue();
			await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new(Guid.NewGuid(), TenantId, userId: user.Id, description: "Roll provider gate"), true);
			return user.Id;
		});
		using var client = CreateClientAs(actor); client.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		string Configuration(string status, bool active) => JsonSerializer.Serialize(new {
			readinessAccess = new { }, onboardingProfiles = Array.Empty<object>(), grNumber = new { configurations = Array.Empty<object>() }, divisionChange = new { configurations = Array.Empty<object>() },
			rollNumber = new { assignmentMode = "both", configurations = new[] { new { id = "roll-pg-rule", status, isActive = active, version = 1,
				boardMode = "all", mediumMode = "all", stageMode = "all", classMode = "all", divisionMode = "all", grouping = "mixed", nameFormat = "first-name-first", arrangeBy = "studentName", arrangeType = "ascending" } } } });
		using var draftResponse = await client.PutAsJsonAsync("/api/student/onboarding/settings/configurations", new SaveStudentOnboardingSettingsDto { AcademicYearId = factory.SeededAcademicYearId, ConfigurationJson = Configuration("draft", false) }, JsonOptions);
		draftResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await draftResponse.Content.ReadAsStringAsync());
		var draft = (await draftResponse.Content.ReadFromJsonAsync<Result<StudentOnboardingSettingsDto>>(JsonOptions))!.Data!;
		using var publish = await client.PutAsJsonAsync("/api/student/onboarding/settings/configurations", new SaveStudentOnboardingSettingsDto { AcademicYearId = factory.SeededAcademicYearId, ConfigurationJson = Configuration("published", true), ConcurrencyStamp = draft.ConcurrencyStamp }, JsonOptions);
		publish.StatusCode.ShouldBe(HttpStatusCode.OK, await publish.Content.ReadAsStringAsync());
		var root = "/api/students/roll-number-batches";
		var scope = new PreviewStudentRollBatchInput { AcademicYearId = factory.SeededAcademicYearId, StandardId = factory.SeededStandardId, DivisionId = fixture.DivisionA, Operation = "renumber" };
		async Task<StudentRollNumberBatchDto> Preview() {
			using var response = await client.PostAsJsonAsync(root + "/preview", scope, JsonOptions);
			response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
			return (await response.Content.ReadFromJsonAsync<Result<StudentRollNumberBatchDto>>(JsonOptions))!.Data!;
		}
		var first = await Preview(); var competing = await Preview(); first.TotalCount.ShouldBe(2); first.Items.Select(x => x.ProposedRollNumber).ShouldBe([1, 2]);
		var command = new SubmitStudentRollBatchInput { ConcurrencyStamp = first.ConcurrencyStamp, IdempotencyKey = first.Id.ToString("D"), Reason = "Reviewed provider roster" };
		using var saved = await client.PostAsJsonAsync($"{root}/{first.Id}/submit", command, JsonOptions);
		saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
		(await saved.Content.ReadFromJsonAsync<Result<StudentRollNumberBatchDto>>(JsonOptions))!.Data!.Status.ShouldBe("Applied");
		using var stale = await client.PostAsJsonAsync($"{root}/{competing.Id}/submit", new SubmitStudentRollBatchInput { ConcurrencyStamp = competing.ConcurrencyStamp, IdempotencyKey = competing.Id.ToString("D"), Reason = command.Reason }, JsonOptions);
		stale.StatusCode.ShouldBe(HttpStatusCode.Conflict, await stale.Content.ReadAsStringAsync());
		using var replay = await client.PostAsJsonAsync($"{root}/{first.Id}/submit", command, JsonOptions);
		replay.StatusCode.ShouldBe(HttpStatusCode.OK, await replay.Content.ReadAsStringAsync());
		var numbers = await InTenantAsync(async services => (await services.GetRequiredService<IRepository<StudentOnboardingCase, Guid>>().GetListAsync(x => fixture.Cases.Contains(x.Id) && x.DivisionId == fixture.DivisionA)).Select(x => x.RollNumber).Order().ToArray());
		numbers.ShouldBe([1, 2]);
	}
}
