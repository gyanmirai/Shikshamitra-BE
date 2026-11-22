using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students.StudentEnrollments;
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
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Students.Records;

public class StudentIdCardTemplateTestFactory : ShikshaMitraApiTestFactory
{
	protected override bool InitializeTestData => false;
}

/// <summary>JSON contracts intentionally compile against the pre-library DTO and HTTP surface.</summary>
[TestModule(TestTraits.Modules.Students)]
public class StudentIdCardTemplateApi_Tests(StudentIdCardTemplateTestFactory factory)
	: ShikshaMitraApiTestBase(factory), IClassFixture<StudentIdCardTemplateTestFactory>
{
	private const string Route = "/api/students/records/ids/templates";
	private const string View = "Students.Records.StudentICard.View";
	private const string Add = "Students.Records.StudentICard.Add";
	private const string Delete = "Students.Records.StudentICard.Delete";

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Icard_class_assignment_separates_rule_management_from_issuing_and_prevents_duplicate_cards()
	{
		var actor = await CreateActorAsync();
		var member = await SeedStudentAsync(actor);
		var placement = await InTenantAsync(actor.TenantId, async services =>
		{
			var student = await services.GetRequiredService<IStudentRepository>().GetAsync(member.Student);
			var emptyClass = await services.GetRequiredService<StandardManager>().CreateAsync(
				student.AcademicMediumStageId!.Value, "Empty Class", 6, null);
			await services.GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(emptyClass, true);
			return (Board: student.AcademicBoardId!.Value, Medium: student.AcademicMediumId, Standard: student.StandardId!.Value, Empty: emptyClass.Id);
		});
		using var client = AsActor(actor, View + "," + Add + "," + Delete);
		using var viewer = AsActor(actor, View);
		var classes = $"/api/students/records/ids/assignments?academicYearId={member.Year}&boardId={placement.Board}&mediumId={placement.Medium}";
		var rule = $"/api/students/records/ids/assignments/{member.Year}/{placement.Standard}";
		var rows = (await ReadAsync(await viewer.GetAsync(classes)))["items"]!.AsArray();
		rows.Select(item => item!["standardId"]!.GetValue<Guid>()).ShouldContain(placement.Empty);
		var row = rows.Single(item => item!["standardId"]!.GetValue<Guid>() == placement.Standard)!;
		row["standardId"]!.GetValue<Guid>().ShouldBe(placement.Standard);
		row["templateId"].ShouldBeNull();
		var filtered = await ReadAsync(await viewer.GetAsync($"{classes}&standardId={placement.Empty}&maxResultCount=1"));
		filtered["totalCount"]!.GetValue<int>().ShouldBe(1);
		filtered["items"]!.AsArray().Single()!["standardId"]!.GetValue<Guid>().ShouldBe(placement.Empty);
		var template = await ReadAsync(await client.PostAsJsonAsync(Route, Body("Class design"), JsonOptions));
		var templateId = template["id"]!.GetValue<Guid>();
		(await viewer.PutAsJsonAsync(rule, new { templateId }, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		var saved = await ReadAsync(await client.PutAsJsonAsync(rule, new { templateId }, JsonOptions));
		var concurrencyStamp = saved["concurrencyStamp"]!.GetValue<string>();
		var templateConcurrencyStamp = saved["templateConcurrencyStamp"]!.GetValue<string>();
		(await client.PutAsJsonAsync(rule, new { templateId }, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		(await client.DeleteAsync(Route + "/" + templateId)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		var beforeIssue = (await ReadAsync(await viewer.GetAsync(rule + "/roster")))["items"]!.AsArray().Single()!;
		beforeIssue["hasValidIssuedCard"]!.GetValue<bool>().ShouldBeFalse();
		var studentIds = new[] { member.Student };
		var preview = await ReadAsync(await viewer.PostAsJsonAsync(rule + "/preview", new { studentIds, concurrencyStamp, templateConcurrencyStamp }, JsonOptions));
		preview.AsArray().Single()!["templateSnapshot"]!["name"]!.GetValue<string>().ShouldBe("Class design");
		var stalePreview = await viewer.PostAsJsonAsync(rule + "/preview", new { studentIds, concurrencyStamp, templateConcurrencyStamp = "stale-template" }, JsonOptions);
		stalePreview.StatusCode.ShouldBe(HttpStatusCode.Conflict, await stalePreview.Content.ReadAsStringAsync());
		var invalidSelection = await viewer.PostAsJsonAsync(rule + "/preview", new { studentIds = new[] { Guid.NewGuid() }, concurrencyStamp, templateConcurrencyStamp }, JsonOptions);
		invalidSelection.StatusCode.ShouldBe(HttpStatusCode.Conflict, await invalidSelection.Content.ReadAsStringAsync());
		using var export = await client.PostAsJsonAsync(rule + "/export", new { studentIds, concurrencyStamp, templateConcurrencyStamp }, JsonOptions);
		export.StatusCode.ShouldBe(HttpStatusCode.OK, await export.Content.ReadAsStringAsync());
		export.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
		var afterExport = (await ReadAsync(await viewer.GetAsync(rule + "/roster")))["items"]!.AsArray().Single()!;
		afterExport["hasValidIssuedCard"]!.GetValue<bool>().ShouldBeFalse();
		var issue = new { studentIds, concurrencyStamp, templateConcurrencyStamp, idempotencyKey = "class-first-issue" };
		var first = (await ReadAsync(await client.PostAsJsonAsync(rule + "/issue", issue, JsonOptions))).AsArray().Single()!;
		var replay = (await ReadAsync(await client.PostAsJsonAsync(rule + "/issue", issue, JsonOptions))).AsArray().Single()!;
		replay["id"]!.GetValue<Guid>().ShouldBe(first["id"]!.GetValue<Guid>());
		replay["studentSnapshot"]!["_AssignmentBatchFingerprint"].ShouldBeNull();
		using var changedPhoto = await client.PostAsJsonAsync(rule + "/issue", new
		{
			studentIds, concurrencyStamp, templateConcurrencyStamp, idempotencyKey = "class-first-issue",
			photoEdits = new[] { new { recordId = member.Student, sourceHash = "changed", imageData = "changed" } }
		}, JsonOptions);
		changedPhoto.StatusCode.ShouldBe(HttpStatusCode.Conflict, await changedPhoto.Content.ReadAsStringAsync());
		(await changedPhoto.Content.ReadAsStringAsync()).ShouldContain("Students:Records:IdAssignmentIssueKeyReused");
		(await client.PostAsJsonAsync(rule + "/issue", new { studentIds, concurrencyStamp, templateConcurrencyStamp, idempotencyKey = "class-second-issue" }, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		var afterIssue = (await ReadAsync(await viewer.GetAsync(rule + "/roster")))["items"]!.AsArray().Single()!;
		afterIssue["hasValidIssuedCard"]!.GetValue<bool>().ShouldBeTrue();
		var edit = await ReadAsync(await client.GetAsync(Route + "/" + templateId));
		edit["name"] = "Revised class design";
		await ReadAsync(await client.PutAsJsonAsync(Route + "/" + templateId, edit, JsonOptions));
		var revisedClass = (await ReadAsync(await viewer.GetAsync(classes)))["items"]!.AsArray()
			.Single(item => item!["standardId"]!.GetValue<Guid>() == placement.Standard)!;
		var revisedStamp = revisedClass["templateConcurrencyStamp"]!.GetValue<string>();
		revisedStamp.ShouldNotBe(templateConcurrencyStamp);
		templateConcurrencyStamp = revisedStamp;
		using var changedTemplate = await client.PostAsJsonAsync(rule + "/issue", new
		{
			studentIds, concurrencyStamp, templateConcurrencyStamp, idempotencyKey = "class-first-issue"
		}, JsonOptions);
		changedTemplate.StatusCode.ShouldBe(HttpStatusCode.Conflict, await changedTemplate.Content.ReadAsStringAsync());
		(await changedTemplate.Content.ReadAsStringAsync()).ShouldContain("Students:Records:IdAssignmentIssueKeyReused");
		var issuedId = first["id"]!.GetValue<Guid>();
		(await client.PostAsJsonAsync($"/api/students/records/ids/{issuedId}/void", new { reason = "Synthetic replacement" }, JsonOptions)).IsSuccessStatusCode.ShouldBeTrue();
		(await ReadAsync(await client.PostAsJsonAsync(rule + "/issue", new { studentIds, concurrencyStamp, templateConcurrencyStamp, idempotencyKey = "class-replacement-issue" }, JsonOptions)))
			.AsArray().Single()!["id"]!.GetValue<Guid>().ShouldNotBe(issuedId);
		var emptyClassRule = $"/api/students/records/ids/assignments/{member.Year}/{placement.Empty}";
		await ReadAsync(await client.PutAsJsonAsync(emptyClassRule, new { templateId }, JsonOptions));
		await InTenantAsync(actor.TenantId, async services =>
		{
			await services.GetRequiredService<IRepository<Standard, Guid>>().DeleteAsync(placement.Empty, true);
			return true;
		});
		(await client.DeleteAsync(Route + "/" + templateId)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
		(await client.DeleteAsync(rule + "?concurrencyStamp=" + concurrencyStamp)).IsSuccessStatusCode.ShouldBeTrue();
		using var deletedTemplate = await client.DeleteAsync(Route + "/" + templateId);
		deletedTemplate.IsSuccessStatusCode.ShouldBeTrue(await deletedTemplate.Content.ReadAsStringAsync());
		var remainingRules = await InTenantAsync(actor.TenantId, services => services.GetRequiredService<IRepository<StudentIdCardAssignment, Guid>>()
			.GetListAsync(item => item.TemplateId == templateId));
		remainingRules.ShouldBeEmpty();
	}

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Icard_class_assignment_does_not_allow_division_scope_to_manage_a_class()
	{
		var owner = await CreateActorAsync();
		var member = await SeedStudentAsync(owner);
		var placement = await InTenantAsync(owner.TenantId, async services =>
		{
			var student = await services.GetRequiredService<IStudentRepository>().GetAsync(member.Student);
			var userId = Guid.NewGuid();
			await services.GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(
				new IdentityUser(userId, "narrow-" + userId.ToString("N"), userId.ToString("N") + "@example.test", owner.TenantId), true);
			await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(),
				owner.TenantId, userId: userId, academicYearId: member.Year, educationBoardId: student.AcademicBoardId,
				educationMediumId: student.AcademicMediumId, educationMediumStageId: student.AcademicMediumStageId,
				standardId: student.StandardId, divisionId: student.DivisionId, description: "Division-only I-Card staff"), true);
			return (Actor: new Actor(owner.TenantId, userId), Board: student.AcademicBoardId!.Value, Medium: student.AcademicMediumId, Standard: student.StandardId!.Value);
		});
		using var client = AsActor(owner, View + "," + Add + "," + Delete);
		using var scoped = AsActor(placement.Actor, View + "," + Add + "," + Delete);
		var classes = $"/api/students/records/ids/assignments?academicYearId={member.Year}&boardId={placement.Board}&mediumId={placement.Medium}";
		var rule = $"/api/students/records/ids/assignments/{member.Year}/{placement.Standard}";
		var template = await ReadAsync(await client.PostAsJsonAsync(Route, Body("Scoped access design"), JsonOptions));
		var templateId = template["id"]!.GetValue<Guid>();
		var saved = await ReadAsync(await client.PutAsJsonAsync(rule, new { templateId }, JsonOptions));
		var concurrencyStamp = saved["concurrencyStamp"]!.GetValue<string>();
		var row = (await ReadAsync(await scoped.GetAsync(classes)))["items"]!.AsArray().Single()!;
		row["canManage"]!.GetValue<bool>().ShouldBeFalse();
		row["templateId"]!.GetValue<Guid>().ShouldBe(templateId);
		var roster = (await ReadAsync(await scoped.GetAsync(rule + "/roster")))["items"]!.AsArray().Single()!;
		roster["studentId"]!.GetValue<Guid>().ShouldBe(member.Student);
		(await scoped.PutAsJsonAsync(rule, new { templateId, concurrencyStamp }, JsonOptions)).IsSuccessStatusCode.ShouldBeFalse();
		(await scoped.DeleteAsync(rule + "?concurrencyStamp=" + concurrencyStamp)).IsSuccessStatusCode.ShouldBeFalse();
	}
	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Icard_photo_HTTP_requires_view_before_record_lookup()
	{
		var actor = await CreateActorAsync(); using var denied = AsActor(actor, Add);
		var response = await denied.GetAsync("/api/students/records/ids/photos/" + Guid.NewGuid());
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Icard_issued_history_HTTP_returns_paged_metadata_with_view_only_and_preserves_frozen_JSON()
	{
		var actor = await CreateActorAsync(); var member = await SeedStudentAsync(actor);
		var placement = await InTenantAsync(actor.TenantId, async services =>
		{
			var student = await services.GetRequiredService<IStudentRepository>().GetAsync(member.Student);
			await services.GetRequiredService<IRepository<StudentIdCard, Guid>>().InsertAsync(new StudentIdCard(Guid.NewGuid(), actor.TenantId, student.Id,
				"HTTP-CARD-001", "http-issued-history", "{invalid frozen JSON}", "{invalid frozen template}", DateTime.UtcNow, actor.UserId, member.Year), true);
			return (Board: student.AcademicBoardId!.Value, Medium: student.AcademicMediumId);
		});
		var route = $"/api/students/records/ids/history?academicYearId={member.Year}&boardId={placement.Board}&mediumId={placement.Medium}&search=HTTP-CARD&maxResultCount=1";
		using var allowed = AsActor(actor, View); using var response = await allowed.GetAsync(route);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
		json["data"]!["totalCount"]!.GetValue<long>().ShouldBe(1);
		var row = json["data"]!["items"]!.AsArray().Single()!;
		row["studentId"]!.GetValue<Guid>().ShouldBe(member.Student); row["studentName"]!.GetValue<string>().ShouldBe("Synthetic HTTP Icard");
		row["studentSnapshot"].ShouldBeNull(); row["templateSnapshot"].ShouldBeNull();
		using var denied = AsActor(actor, Add);
		(await denied.GetAsync(route)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		(await allowed.GetAsync(route + "&skipCount=-1")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		var foreignActor = await CreateActorAsync(); using var foreign = AsActor(foreignActor, View);
		var forbidden = await foreign.GetAsync(route); forbidden.IsSuccessStatusCode.ShouldBeFalse();
		(await forbidden.Content.ReadAsStringAsync()).ShouldNotContain("HTTP-CARD-001");
	}

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Icard_templates_HTTP_library_CRUD_preserves_oldest_legacy_fallback()
	{
		var actor = await CreateActorAsync();
		using var client = AsActor(actor, View + "," + Add + "," + Delete);
		// This existing route proves the real authenticated tenant pipeline is usable before the missing route.
		(await client.GetAsync(Route)).StatusCode.ShouldBe(HttpStatusCode.OK);
		var first = await ReadAsync(await client.PostAsJsonAsync(Route, Body("First school design"), JsonOptions));
		var second = await ReadAsync(await client.PostAsJsonAsync(Route, Body("Second school design"), JsonOptions));
		var firstId = first["id"]!.GetValue<Guid>(); var secondId = second["id"]!.GetValue<Guid>();
		firstId.ShouldNotBe(Guid.Empty); secondId.ShouldNotBe(firstId);
		var catalogue = (await ReadAsync(await client.GetAsync(Route + "/list"))).AsArray();
		catalogue.Select(x => x!["id"]!.GetValue<Guid>()).ShouldBe(new[] { firstId, secondId });
		(await ReadAsync(await client.GetAsync(Route)))["id"]!.GetValue<Guid>().ShouldBe(firstId);
		var loaded = await ReadAsync(await client.GetAsync(Route + "/" + secondId));
		loaded["name"]!.GetValue<string>().ShouldBe("Second school design");
		loaded["name"] = "Revised second design";
		var updated = await ReadAsync(await client.PutAsJsonAsync(Route + "/" + secondId, loaded, JsonOptions));
		updated["id"]!.GetValue<Guid>().ShouldBe(secondId);
		updated["name"]!.GetValue<string>().ShouldBe("Revised second design");
		var legacy = await ReadAsync(await client.GetAsync(Route)); legacy["name"] = "Revised legacy first";
		(await ReadAsync(await client.PutAsJsonAsync(Route, legacy, JsonOptions)))["id"]!.GetValue<Guid>().ShouldBe(firstId);
		(await ReadAsync(await client.GetAsync(Route + "/" + secondId)))["name"]!.GetValue<string>().ShouldBe("Revised second design");
		using var deleted = await client.DeleteAsync(Route + "/" + firstId);
		deleted.IsSuccessStatusCode.ShouldBeTrue(await deleted.Content.ReadAsStringAsync());
		(await ReadAsync(await client.GetAsync(Route)))["id"]!.GetValue<Guid>().ShouldBe(secondId);
		(await client.GetAsync(Route + "/" + firstId)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Icard_templates_HTTP_catalogue_returns_metadata_without_creating_a_default_design()
	{
		var actor = await CreateActorAsync(); using var client = AsActor(actor, View);
		(await ReadAsync(await client.GetAsync(Route + "/list"))).AsArray().ShouldBeEmpty();
		(await client.GetAsync(Route)).StatusCode.ShouldBe(HttpStatusCode.OK);
		(await ReadAsync(await client.GetAsync(Route + "/list"))).AsArray().ShouldBeEmpty();
	}

	[Theory, TestTier(TestTraits.Tiers.Critical)]
	[InlineData("GET", "/list", Add)]
	[InlineData("POST", "", View)]
	[InlineData("PUT", "/00000000-0000-0000-0000-000000000001", View)]
	[InlineData("DELETE", "/00000000-0000-0000-0000-000000000001", Add)]
	public async Task Icard_templates_HTTP_enforces_each_existing_action_permission(string method, string suffix, string grant)
	{
		var actor = await CreateActorAsync(); using var client = AsActor(actor, grant);
		using var request = new HttpRequestMessage(new HttpMethod(method), Route + suffix);
		if (method is "POST" or "PUT") request.Content = JsonContent.Create(Body("Permission design"), options: JsonOptions);
		using var response = await client.SendAsync(request);
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Icard_templates_HTTP_hidden_cohort_is_not_disclosed_or_cleared_by_design_only_save()
	{
		var owner = await CreateActorAsync(); var member = await SeedStudentAsync(owner);
		using var allowed = AsActor(owner, View + "," + Add + "," + Delete);
		var saved = await ReadAsync(await allowed.PostAsJsonAsync(Route, new
		{
			name = "Private membership design", schoolName = "Synthetic HTTP school", fields = new[] { "StudentName" },
			selection = new { academicYearId = member.Year, studentIds = new[] { member.Student } }
		}, JsonOptions));
		var id = saved["id"]!.GetValue<Guid>();
		var staff = await InTenantAsync(owner.TenantId, async services =>
		{
			var user = Guid.NewGuid(); await services.GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(new IdentityUser(user, "restricted-" + user.ToString("N"), user.ToString("N") + "@example.test", owner.TenantId), true);
			return new Actor(owner.TenantId, user);
		});
		using var restricted = AsActor(staff, View + "," + Add + "," + Delete);
		var rows = await ReadAsync(await restricted.GetAsync(Route + "/list")); rows.ToJsonString().ShouldNotContain(member.Student.ToString());
		var detail = await ReadAsync(await restricted.GetAsync(Route + "/" + id));
		detail["selectionRestricted"]!.GetValue<bool>().ShouldBeTrue(); detail["selection"].ShouldBeNull();
		detail.AsObject().Remove("selection"); detail.AsObject().Remove("selectionRestricted"); detail["name"] = "Safe design-only revision";
		var updated = await ReadAsync(await restricted.PutAsJsonAsync(Route + "/" + id, detail, JsonOptions));
		updated["selection"].ShouldBeNull(); updated["selectionRestricted"]!.GetValue<bool>().ShouldBeTrue();
		var rawBefore = await InTenantAsync(owner.TenantId, async services => (await services.GetRequiredService<IRepository<StudentIdCardTemplate, Guid>>().GetAsync(id)).ConfigurationJson);
		updated["selection"] = JsonSerializerNode(new { academicYearId = member.Year, studentIds = Array.Empty<Guid>() });
		using var deniedClear = await restricted.PutAsJsonAsync(Route + "/" + id, updated, JsonOptions);
		deniedClear.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await deniedClear.Content.ReadAsStringAsync());
		using var deniedDelete = await restricted.DeleteAsync(Route + "/" + id);
		deniedDelete.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await deniedDelete.Content.ReadAsStringAsync());
		(await InTenantAsync(owner.TenantId, async services => (await services.GetRequiredService<IRepository<StudentIdCardTemplate, Guid>>().GetAsync(id)).ConfigurationJson)).ShouldBe(rawBefore);
		var visible = await ReadAsync(await allowed.GetAsync(Route + "/" + id));
		visible["name"]!.GetValue<string>().ShouldBe("Safe design-only revision");
		visible["selection"]!["studentIds"]!.AsArray().Single()!.GetValue<Guid>().ShouldBe(member.Student);
	}

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Icard_templates_HTTP_denied_opaque_cohort_writes_do_not_disclose_saved_member_IDs()
	{
		var owner = await CreateActorAsync(); var member = await SeedStudentAsync(owner);
		using var allowed = AsActor(owner, View + "," + Add + "," + Delete);
		var saved = await ReadAsync(await allowed.PostAsJsonAsync(Route, new
		{
			name = "Opaque cohort design", schoolName = "Synthetic HTTP school", fields = new[] { "StudentName" },
			selection = new { academicYearId = member.Year, studentIds = new[] { member.Student } }
		}, JsonOptions));
		var id = saved["id"]!.GetValue<Guid>();
		var staff = await InTenantAsync(owner.TenantId, async services =>
		{
			var student = await services.GetRequiredService<IStudentRepository>().GetAsync(member.Student);
			var standard = await services.GetRequiredService<StandardManager>().CreateAsync(student.AcademicMediumStageId!.Value, "Sibling staff class", 6, null);
			await services.GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(standard, true);
			var division = await services.GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "Sibling staff division", 30);
			await services.GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, true);
			var user = Guid.NewGuid(); await services.GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(new IdentityUser(user,
				"narrow-icard-" + user.ToString("N"), user.ToString("N") + "@example.test", owner.TenantId), true);
			await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), owner.TenantId,
				userId: user, academicYearId: member.Year, educationBoardId: student.AcademicBoardId, educationMediumId: student.AcademicMediumId,
				educationMediumStageId: student.AcademicMediumStageId, standardId: standard.Id, divisionId: division.Id), true);
			return new Actor(owner.TenantId, user);
		});
		using var narrow = AsActor(staff, View + "," + Add + "," + Delete);
		var hidden = await ReadAsync(await narrow.GetAsync(Route + "/" + id));
		hidden["selectionRestricted"]!.GetValue<bool>().ShouldBeTrue(); hidden["selection"].ShouldBeNull();
		var before = await InTenantAsync(owner.TenantId, async services =>
		{
			var row = await services.GetRequiredService<IRepository<StudentIdCardTemplate, Guid>>().GetAsync(id); return (row.ConfigurationJson, row.ConcurrencyStamp);
		});
		hidden["selection"] = JsonSerializerNode(new { academicYearId = member.Year, studentIds = Array.Empty<Guid>() });
		using var clear = await narrow.PutAsJsonAsync(Route + "/" + id, hidden, JsonOptions);
		using var delete = await narrow.DeleteAsync(Route + "/" + id);
		var clearText = await clear.Content.ReadAsStringAsync(); var deleteText = await delete.Content.ReadAsStringAsync();
		clear.StatusCode.ShouldBe(HttpStatusCode.BadRequest, clearText); delete.StatusCode.ShouldBe(HttpStatusCode.BadRequest, deleteText);
		clearText.ShouldNotContain(member.Student.ToString(), customMessage: "A hidden saved member must not leak through scope exception data.");
		deleteText.ShouldNotContain(member.Student.ToString(), customMessage: "The caller supplied only a template ID, not the hidden Student ID.");
		(await InTenantAsync(owner.TenantId, async services =>
		{
			var row = await services.GetRequiredService<IRepository<StudentIdCardTemplate, Guid>>().GetAsync(id); return (row.ConfigurationJson, row.ConcurrencyStamp);
		})).ShouldBe(before);
	}

	[Fact, TestTier(TestTraits.Tiers.Critical)]
	public async Task Icard_templates_HTTP_foreign_tenant_template_ID_cannot_be_read_changed_deleted_or_selected()
	{
		var owner = await CreateActorAsync(); using var allowed = AsActor(owner, View + "," + Add + "," + Delete);
		var saved = await ReadAsync(await allowed.PostAsJsonAsync(Route, Body("Tenant private design"), JsonOptions));
		var id = saved["id"]!.GetValue<Guid>(); var other = await CreateActorAsync(); using var foreign = AsActor(other, View + "," + Add + "," + Delete);
		(await ReadAsync(await foreign.GetAsync(Route + "/list"))).AsArray().ShouldBeEmpty();
		(await foreign.GetAsync(Route + "/" + id)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await foreign.PutAsJsonAsync(Route + "/" + id, saved, JsonOptions)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await foreign.DeleteAsync(Route + "/" + id)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		foreach (var operation in new[] { "preview", "issue", "export" })
		{
			using var response = await foreign.PostAsJsonAsync("/api/students/records/ids/" + operation,
				new { templateId = id, studentIds = new[] { Guid.NewGuid() }, idempotencyKey = "foreign-design", cardsPerPage = 8 }, JsonOptions);
			response.StatusCode.ShouldBe(HttpStatusCode.NotFound, await response.Content.ReadAsStringAsync());
		}
		(await ReadAsync(await allowed.GetAsync(Route + "/" + id)))["name"]!.GetValue<string>().ShouldBe("Tenant private design");
	}

	private static object Body(string name) => new { name, schoolName = "Contract test school", fields = new[] { "StudentName", "Class" } };
	private static JsonNode JsonSerializerNode(object input) => System.Text.Json.JsonSerializer.SerializeToNode(input, JsonOptions)!;
	private Task<(Guid Student, Guid Year)> SeedStudentAsync(Actor actor) => InTenantAsync(actor.TenantId, async services =>
	{
		var year = await services.GetRequiredService<AcademicYearManager>().CreateAsync("Icard HTTP year", new DateTime(2031, 6, 1), new DateTime(2032, 4, 30));
		await services.GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
		var board = await services.GetRequiredService<EducationBoardManager>().CreateAsync(year.Id, EducationBoardType.CBSE);
		await services.GetRequiredService<IRepository<EducationBoard, Guid>>().InsertAsync(board, true);
		var medium = await services.GetRequiredService<EducationMediumManager>().CreateAsync(board.Id, "Synthetic English");
		await services.GetRequiredService<IRepository<EducationMedium, Guid>>().InsertAsync(medium, true);
		var stage = await services.GetRequiredService<EducationMediumStageManager>().CreateAsync(medium.Id, "Synthetic Primary");
		await services.GetRequiredService<IRepository<EducationMediumStage, Guid>>().InsertAsync(stage, true);
		var standard = await services.GetRequiredService<StandardManager>().CreateAsync(stage.Id, "Synthetic Class", 5, null);
		await services.GetRequiredService<IRepository<Standard, Guid>>().InsertAsync(standard, true);
		var division = await services.GetRequiredService<DivisionManager>().CreateAsync(standard.Id, "Synthetic A", 30);
		await services.GetRequiredService<IRepository<Division, Guid>>().InsertAsync(division, true);
		var student = new Student(Guid.NewGuid(), actor.TenantId, "Synthetic", null, "HTTP Icard", year.Id, board.Id, medium.Id, stage.Id, standard.Id, division.Id, status: StudentLifeCycleStatusEnum.Enrolled);
		await services.GetRequiredService<IStudentRepository>().InsertAsync(student, true);
		await services.GetRequiredService<IRepository<StudentEnrollment, Guid>>().InsertAsync(StudentEnrollment.Create(Guid.NewGuid(), actor.TenantId, student.Id, year.Id, standard.Id, division.Id), true);
		var reviewer = Guid.NewGuid(); await services.GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(new IdentityUser(reviewer, "review-" + reviewer.ToString("N"), reviewer.ToString("N") + "@example.test", actor.TenantId), true);
		var historical = new StudentRecordLegacyOverride(Guid.NewGuid(), actor.TenantId, student.Id, actor.UserId, "Verified synthetic school register", null);
		historical.Decide(reviewer, true, "Independent synthetic review", DateTime.UtcNow);
		await services.GetRequiredService<IRepository<StudentRecordLegacyOverride, Guid>>().InsertAsync(historical, true);
		await services.GetRequiredService<IRepository<UserScopeAssignment, Guid>>().InsertAsync(new UserScopeAssignment(Guid.NewGuid(), actor.TenantId, userId: actor.UserId, description: "Synthetic HTTP I-card tenant-wide actor"), true);
		return (student.Id, year.Id);
	});
	private async Task<T> InTenantAsync<T>(Guid tenantId, Func<IServiceProvider, Task<T>> action)
	{
		using var services = Factory.Services.CreateScope(); using var tenant = services.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId);
		using var work = services.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true);
		var result = await action(services.ServiceProvider); await work.CompleteAsync(); return result;
	}
	private static async Task<JsonNode> ReadAsync(HttpResponseMessage response)
	{
		using (response)
		{
			var text = await response.Content.ReadAsStringAsync();
			response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
			var json = JsonNode.Parse(text)!; return json["data"] ?? json;
		}
	}
	private HttpClient AsActor(Actor actor, string permissions)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, actor.UserId.ToString());
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, actor.TenantId.ToString());
		client.DefaultRequestHeaders.Add("__tenant", actor.TenantId.ToString());
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, permissions); return client;
	}
	private async Task<Actor> CreateActorAsync()
	{
		using var services = Factory.Services.CreateScope(); var provider = services.ServiceProvider;
		var tenantContext = provider.GetRequiredService<ICurrentTenant>(); var uows = provider.GetRequiredService<IUnitOfWorkManager>();
		Guid tenantId;
		using (tenantContext.Change(null))
		using (var work = uows.Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true))
		{
			var tenant = await provider.GetRequiredService<TenantManager>().CreateAsync("Icard-contract-" + Guid.NewGuid().ToString("N"));
			await provider.GetRequiredService<ITenantRepository>().InsertAsync(tenant, true); tenantId = tenant.Id; await work.CompleteAsync();
		}
		var userId = Guid.NewGuid();
		using (tenantContext.Change(tenantId))
		using (var work = uows.Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true))
		{
			await provider.GetRequiredService<IRepository<IdentityUser, Guid>>().InsertAsync(new IdentityUser(userId, "icard-" + userId.ToString("N"), userId.ToString("N") + "@example.test", tenantId), true);
			await work.CompleteAsync();
		}
		return new(tenantId, userId);
	}
	private sealed record Actor(Guid TenantId, Guid UserId);
}
