using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Common.Responses;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using ShikshaMitra.TenantSettings.AcademicSetups;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Records;

/// <summary>Real HTTP pipeline with the existing test authentication/grant checker and isolated SQLite data.</summary>
[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Students)]
public class StudentRecordsAuthorizationApi_Tests(ShikshaMitraApiTestFactory factory) : ShikshaMitraApiTestBase(factory)
{
	private static readonly Guid TenantId = Guid.Parse(TestAuthHandler.TestTenantId);
	private const string DirectoryView = ShikshaMitraSubmodulePermissions.Students.Records_StudentList.View;

	[Theory, TestTier(TestTraits.Tiers.Critical)]
	[InlineData("settings/publish", "Students.Records.Settings.View,Students.Records.Settings.Add")]
	[InlineData("changes/{id}/approve", "Students.Records.Changes.View,Students.Records.Changes.Create")]
	[InlineData("transitions/{id}/apply", "Students.Records.Transitions.View,Students.Records.Transitions.Create")]
	[InlineData("guardian-access/reviews/{id}/resolve", "Students.Records.GuardianAccess.View")]
	[InlineData("ids/issue", "Students.Records.StudentICard.View")]
	[InlineData("certificates/{id}/void", "Students.Records.Certificates.View,Students.Records.Certificates.Add")]
	[InlineData("reports", "Students.Records.Reports.View")]
	public async Task Records_HTTP_denies_exact_action_even_with_related_view_or_create_grants(string route, string permissions)
	{
		var actor = await CreateActorAsync(tenantWide: true);
		using var client = AsActor(actor, permissions);
		using var response = await client.PostAsJsonAsync("/api/students/records/" + route.Replace("{id}", Guid.NewGuid().ToString()), new { }, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Records_HTTP_anonymous_and_ungranted_directory_requests_do_not_reach_records()
	{
		using var anonymous = CreateAnonymousClient();
		// The shared test checker otherwise grants every named permission even to an anonymous principal.
		anonymous.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "__none__");
		anonymous.DefaultRequestHeaders.Add("__tenant", TenantId.ToString());
		(await anonymous.GetAsync("/api/students/records/directory")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
		var actor = await CreateActorAsync(tenantWide: true);
		using var denied = AsActor(actor, "__none__");
		(await denied.GetAsync("/api/students/records/directory")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Records_HTTP_forged_admin_claim_cannot_bypass_absent_database_scope()
	{
		var record = await CreateRecordAsync();
		var reader = await CreateActorAsync(tenantWide: true); var noScope = await CreateActorAsync();
		using var allowed = AsActor(reader, DirectoryView); using var denied = AsActor(noScope, DirectoryView);
		var permitted = await DirectoryAsync(allowed, record.StudentId); permitted.TotalCount.ShouldBe(1);
		permitted.Items[0].Id.ShouldBe(record.StudentId);
		(await DirectoryAsync(denied, record.StudentId)).TotalCount.ShouldBe(0);
		using var profile = await denied.GetAsync($"/api/students/records/{record.StudentId}/profile");
		profile.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await profile.Content.ReadAsStringAsync());
		(await profile.Content.ReadAsStringAsync()).ShouldContain(UserScopeErrorCodes.AccessDenied);
	}

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Records_HTTP_disabled_identity_with_valid_principal_and_grants_is_forbidden()
	{
		var actor = await CreateActorAsync(tenantWide: true, active: false);
		using var client = AsActor(actor, DirectoryView + ",Students.Records.StudentICard.View,Students.Records.Reports.View");
		foreach (var route in new[] { "directory", "ids/templates", "reports" })
		{
			using var response = await client.GetAsync("/api/students/records/" + route);
			response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
		}
	}

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Records_HTTP_foreign_tenant_profile_and_directory_do_not_disclose_local_student()
	{
		var record = await CreateRecordAsync();
		var foreignTenant = await InTenantAsync(TenantId, async services =>
		{
			var tenant = await services.GetRequiredService<TenantManager>().CreateAsync($"RecordsHttp-{Guid.NewGuid():N}");
			await services.GetRequiredService<ITenantRepository>().InsertAsync(tenant, true); return tenant.Id;
		});
		var actor = await CreateActorAsync(tenantId: foreignTenant, tenantWide: true);
		using var foreign = AsActor(actor, DirectoryView, foreignTenant);
		(await DirectoryAsync(foreign, record.StudentId)).TotalCount.ShouldBe(0);
		using var profile = await foreign.GetAsync($"/api/students/records/{record.StudentId}/profile");
		profile.StatusCode.ShouldBe(HttpStatusCode.NotFound, await profile.Content.ReadAsStringAsync());
		(await profile.Content.ReadAsStringAsync()).ShouldNotContain(record.Name);
	}

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Records_HTTP_visible_source_cannot_be_moved_to_an_out_of_scope_target_class()
	{
		var record = await CreateRecordAsync(); var actor = await CreateActorAsync(scopeRecord: record);
		await InTenantAsync(TenantId, async services =>
		{
			var assignments = await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().GetListAsync(x => x.UserId == actor);
			assignments.Count.ShouldBe(1); assignments[0].StandardId.ShouldBe(record.StandardId); assignments[0].AcademicYearId.ShouldBe(record.YearId);
			assignments[0].EducationBoardId.ShouldBe(Factory.SeededBoardId); assignments[0].EducationMediumStageId.ShouldBe(Factory.SeededMediumStageId);
			var users = services.GetRequiredService<IdentityUserManager>(); (await users.GetRolesAsync((await users.FindByIdAsync(actor.ToString()))!)).ShouldBeEmpty(); return true;
		});
		using var client = AsActor(actor, DirectoryView + ",Students.Records.Changes.View,Students.Records.Changes.Create");
		using var profile = await client.GetAsync($"/api/students/records/{record.StudentId}/profile");
		profile.StatusCode.ShouldBe(HttpStatusCode.OK, await profile.Content.ReadAsStringAsync());
		var snapshot = await client.GetFromJsonAsync<StudentRecordSectionSnapshotDto>($"/api/students/records/changes/students/{record.StudentId}/snapshot/placement", JsonOptions);
		snapshot.ShouldNotBeNull(); var key = Guid.NewGuid().ToString("N");
		using var rejected = await client.PostAsJsonAsync("/api/students/records/changes", new CreateStudentRecordChangeDto
		{
			StudentId = record.StudentId, Section = "placement", ClientKey = key, Reason = "Target class must remain within staff scope", ExpectedSnapshot = snapshot.Snapshot,
			Payload = JsonSerializer.SerializeToElement(new StudentRecordPlacementPayload { AcademicYearId = record.YearId, StandardId = record.TargetStandardId, DivisionId = record.TargetDivisionId, RollNumber = 12 })
		}, JsonOptions);
		rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await rejected.Content.ReadAsStringAsync());
		(await rejected.Content.ReadAsStringAsync()).ShouldContain(UserScopeErrorCodes.AccessDenied);
		await InTenantAsync(TenantId, async services =>
		{
			(await services.GetRequiredService<IRepository<StudentRecordChange, Guid>>().CountAsync(c => c.StudentId == record.StudentId && c.ClientKey == key)).ShouldBe(0);
			await AssertUnchangedClassAsync(services, record); return true;
		});
	}

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Records_HTTP_promotion_cannot_persist_or_activate_an_out_of_scope_target_class_in_a_permitted_year()
	{
		var record = await CreateRecordAsync(); var actor = await CreateActorAsync(scopeRecord: record);
		var target = await InTenantAsync<TargetFixture>(TenantId, async services =>
		{
			var sourceYear = await services.GetRequiredService<IRepository<AcademicYear, Guid>>().GetAsync(record.YearId);
			var year = await services.GetRequiredService<AcademicYearManager>().CreateAsync($"Http-{Guid.NewGuid():N}", sourceYear.StartDate.AddYears(1), sourceYear.EndDate.AddYears(1));
			await services.GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
			var board = await services.GetRequiredService<EducationBoardManager>().CreateAsync(year.Id, EducationBoardType.CBSE);
			await services.GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(board, true);
			var medium = await services.GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, "HTTP English");
			await services.GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, true);
			var stage = await services.GetRequiredService<EducationMediumStageManager>().CreateAsync(medium.Id, "HTTP stage");
			await services.GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
			var allowed = await services.GetRequiredService<StandardManager>().CreateAsync(stage.Id, "HTTP allowed class", 6, null);
			var forbidden = await services.GetRequiredService<StandardManager>().CreateAsync(stage.Id, "HTTP forbidden class", 6, null);
			await services.GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(allowed, true);
			await services.GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(forbidden, true);
			var division = await services.GetRequiredService<DivisionManager>().CreateAsync(forbidden.Id, "HTTP target", 30);
			await services.GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, true);
			await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), TenantId, userId: actor,
				academicYearId: year.Id, educationBoardId: board.Id, educationMediumId: medium.Id, educationMediumStageId: stage.Id, standardId: allowed.Id), true);
			return new(year.Id, forbidden.Id, division.Id);
		});
		using var client = AsActor(actor, DirectoryView + ",Students.Records.Transitions.View,Students.Records.Transitions.Create");
		var input = new StudentRecordTransitionBatchInput
		{
			AcademicYearId = record.YearId, IdempotencyKey = Guid.NewGuid().ToString("N"),
			Items = [new() { StudentId = record.StudentId, Outcome = "Promote", TargetAcademicYearId = target.YearId, TargetStandardId = target.StandardId,
				TargetDivisionId = target.DivisionId, RollNumber = 12, EffectiveDate = AdmissionCycleWindowEvaluator.GetInstitutionDate(DateTime.UtcNow), Reason = "Target class must remain within staff scope" }]
		};
		using var previewResponse = await client.PostAsJsonAsync("/api/students/records/transitions/preview", input, JsonOptions);
		previewResponse.StatusCode.ShouldBe(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
		var preview = (await previewResponse.Content.ReadFromJsonAsync<Result<StudentRecordTransitionPreviewDto>>(JsonOptions))!.Data!;
		preview.CanSubmit.ShouldBeFalse(); preview.Items[0].Ready.ShouldBeFalse(); preview.Items[0].Issues.ShouldContain(i => i.Code == UserScopeErrorCodes.AccessDenied);
		input.Items[0].StudentConcurrencyStamp = preview.Items[0].StudentConcurrencyStamp; input.Items[0].EnrollmentConcurrencyStamp = preview.Items[0].EnrollmentConcurrencyStamp;
		using var rejected = await client.PostAsJsonAsync("/api/students/records/transitions/batches", input, JsonOptions);
		rejected.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await rejected.Content.ReadAsStringAsync());
		(await rejected.Content.ReadAsStringAsync()).ShouldContain("Students:Records:BatchHeld");
		await InTenantAsync(TenantId, async services =>
		{
			(await services.GetRequiredService<IRepository<StudentRecordTransitionBatch, Guid>>().CountAsync(b => b.IdempotencyKey == input.IdempotencyKey)).ShouldBe(0);
			(await services.GetRequiredService<IRepository<StudentRecordTransition, Guid>>().CountAsync(t => t.StudentId == record.StudentId)).ShouldBe(0);
			(await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().CountAsync(e => e.StudentId == record.StudentId && e.AcademicYearId == target.YearId)).ShouldBe(0);
			await AssertUnchangedClassAsync(services, record); return true;
		});
	}

	private static async Task AssertUnchangedClassAsync(IServiceProvider services, RecordFixture record)
	{
		var query = await services.GetRequiredService<IStudentRepository>().GetQueryableAsync();
		(await services.GetRequiredService<IAsyncQueryableExecuter>().SingleAsync(query.Where(s => s.Id == record.StudentId).Select(s => s.StandardId))).ShouldBe(record.StandardId);
	}
	private HttpClient AsActor(Guid actorId, string permissions, Guid? tenantId = null)
	{
		var client = Factory.CreateClient(); var tenant = (tenantId ?? TenantId).ToString();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, actorId.ToString());
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "admin");
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, tenant);
		client.DefaultRequestHeaders.Add("__tenant", tenant);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permissions); return client;
	}
	private static async Task<PagedResultDto<StudentDirectoryItemDto>> DirectoryAsync(HttpClient client, Guid studentId)
	{
		using var response = await client.GetAsync($"/api/students/records/directory?studentId={studentId}");
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var result = await response.Content.ReadFromJsonAsync<Result<PagedResultDto<StudentDirectoryItemDto>>>(JsonOptions);
		result.ShouldNotBeNull(); result.Data.ShouldNotBeNull(); return result.Data;
	}
	private Task<Guid> CreateActorAsync(Guid? tenantId = null, bool tenantWide = false, bool active = true, RecordFixture? scopeRecord = null) => InTenantAsync(tenantId ?? TenantId, async services =>
	{
		var id = Guid.NewGuid(); var user = new IdentityUser(id, $"records-http-{id:N}", $"{id:N}@example.test", tenantId ?? TenantId); user.SetIsActive(active);
		await services.GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(user, true);
		if (tenantWide || scopeRecord != null)
			await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), tenantId ?? TenantId, userId: id,
				academicYearId: scopeRecord?.YearId, educationBoardId: scopeRecord == null ? null : Factory.SeededBoardId,
				educationMediumId: scopeRecord == null ? null : Factory.SeededMediumId, educationMediumStageId: scopeRecord == null ? null : Factory.SeededMediumStageId,
				standardId: scopeRecord?.StandardId, description: "Isolated Records HTTP regression actor"), true);
		return id;
	});
	private Task<RecordFixture> CreateRecordAsync() => InTenantAsync<RecordFixture>(TenantId, async services =>
	{
		var board = await services.GetRequiredService<IRepository<EducationBoard, Guid>>().GetAsync(Factory.SeededBoardId);
		var division = await services.GetRequiredService<DivisionManager>().CreateAsync(Factory.SeededStandardId, $"Http-{Guid.NewGuid():N}", 30);
		await services.GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, true);
		var target = await services.GetRequiredService<StandardManager>().CreateAsync(Factory.SeededMediumStageId, $"Http-{Guid.NewGuid():N}", 6, null);
		await services.GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(target, true);
		var targetDivision = await services.GetRequiredService<DivisionManager>().CreateAsync(target.Id, "HTTP target", 30);
		await services.GetRequiredService<IRepository<Division, Guid>>().InsertAsync(targetDivision, true);
		var id = Guid.NewGuid(); var name = $"RecordHttp-{id:N}";
		var student = new Student(id, TenantId, name, null, "Student", board.AcademicYearId, board.Id, Factory.SeededMediumId, Factory.SeededMediumStageId,
			Factory.SeededStandardId, division.Id, status: StudentLifeCycleStatusEnum.Enrolled);
		await services.GetRequiredService<IStudentRepository>().InsertAsync(student, true);
		await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().InsertAsync(StudentEnrollment.Create(Guid.NewGuid(), TenantId, id, board.AcademicYearId, student.StandardId, division.Id), true);
		var reviewerId = Guid.NewGuid();
		await services.GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(new IdentityUser(reviewerId, $"records-review-{reviewerId:N}", $"{reviewerId:N}@example.test", TenantId), true);
		var historical = new StudentRecordLegacyOverride(Guid.NewGuid(), TenantId, id, Guid.Parse(TestAuthHandler.TestUserId), "Verified isolated historical school register", null);
		historical.Decide(reviewerId, true, "Historical evidence verified", DateTime.UtcNow);
		await services.GetRequiredService<IRepository<StudentRecordLegacyOverride, Guid>>().InsertAsync(historical, true);
		return new(id, name, board.AcademicYearId, Factory.SeededStandardId, division.Id, target.Id, targetDivision.Id);
	});
	private async Task<T> InTenantAsync<T>(Guid tenantId, Func<IServiceProvider, Task<T>> action)
	{
		using var services = Factory.Services.CreateScope();
		using var tenant = services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var work = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true);
		var result = await action(services.ServiceProvider); await work.CompleteAsync(); return result;
	}
	private sealed record RecordFixture(Guid StudentId, string Name, Guid YearId, Guid StandardId, Guid DivisionId, Guid TargetStandardId, Guid TargetDivisionId);
	private sealed record TargetFixture(Guid YearId, Guid StandardId, Guid DivisionId);
}
