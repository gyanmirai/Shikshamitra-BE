using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Reports;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

[Collection(ApiTestCollection.Name)]
public partial class LeavePolicySafetyApi_Tests(HrOperationalTableApiTestFactory factory)
	: ShikshaMitraApiTestBase(factory), IClassFixture<HrOperationalTableApiTestFactory>
{
	private const string Root = "/api/leave-policies";
	private const string Permission = "HR.LeaveManagement.Setting.LeavePolicy.";

	[Theory]
	[InlineData("view", "View", HttpStatusCode.OK)]
	[InlineData("create", "Add", HttpStatusCode.Created)]
	[InlineData("edit", "Edit", HttpStatusCode.OK)]
	[InlineData("default", "SetDefault", HttpStatusCode.OK)]
	[InlineData("archive", "Delete", HttpStatusCode.OK)]
	[InlineData("restore", "Delete", HttpStatusCode.OK)]
	public async Task Each_Action_Uses_Its_Existing_Exact_Grant(string action, string grant, HttpStatusCode expected)
	{
		var (year, policy) = await CreateFixtureAsync();
		if (action == "restore") (await ActAsync(Client, "archive", year, policy, reviewed: true)).EnsureSuccessStatusCode();
		using var client = Restricted(grant);
		using var response = await ActAsync(client, action, year, policy, reviewed: true);
		response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync());
	}

	[Theory]
	[InlineData("create")]
	[InlineData("edit")]
	[InlineData("default")]
	[InlineData("archive")]
	[InlineData("restore")]
	public async Task View_Only_Cannot_Invoke_Writes_Directly(string action)
	{
		var (year, policy) = await CreateFixtureAsync();
		if (action == "restore") (await ActAsync(Client, "archive", year, policy, reviewed: true)).EnsureSuccessStatusCode();
		using var client = Restricted("View");
		using var response = await ActAsync(client, action, year, policy);
		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	private HttpClient Restricted(string grant)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, Permission + grant);
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "Teacher");
		return client;
	}

	[Theory]
	[InlineData(0)]
	[InlineData(30)]
	public async Task Archive_Direct_Api_Blocks_Current_And_Future_References_For_Restricted_Writer(int startOffset)
	{
		var (year, policy) = await CreateFixtureAsync();
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			await scope.ServiceProvider.GetRequiredService<IRepository<HrPolicyAssignment, Guid>>().InsertAsync(new HrPolicyAssignment(
				Guid.NewGuid(), Guid.Parse(TestAuthHandler.TestTenantId), year, HrPolicyAssignmentTargetType.Group,
				Guid.NewGuid().ToString(), "Synthetic assigned group", Guid.NewGuid(), Guid.NewGuid(), policy.Id,
				DateTime.UtcNow.Date.AddDays(startOffset), null, true), true);
			await unit.CompleteAsync();
		}
		using var client = Restricted("Delete");
		using var response = await client.DeleteAsync($"{Root}/{policy.Id}");
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await response.Content.ReadAsStringAsync()).ShouldContain("ShikshaMitra:LeavePolicyHasActiveAssignments");
		var preserved = await Client.GetFromJsonAsync<LeavePolicyDetailResponse>($"{Root}/{policy.Id}", JsonOptions);
		preserved!.Status.ShouldBe("Active");
		preserved.Rules.Count.ShouldBe(2);
	}

	[Fact]
	public async Task Foreign_Tenant_Cannot_Read_Or_Archive_A_Known_Policy_Id()
	{
		var (_, policy) = await CreateFixtureAsync();
		using var client = Restricted("View");
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, Guid.NewGuid().ToString());
		(await client.GetAsync($"{Root}/{policy.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		using var writer = Restricted("Delete");
		writer.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, Guid.NewGuid().ToString());
		(await writer.DeleteAsync($"{Root}/{policy.Id}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
		(await Client.GetFromJsonAsync<LeavePolicyDetailResponse>($"{Root}/{policy.Id}", JsonOptions))!.Status.ShouldBe("Active");
	}

	private async Task<HttpResponseMessage> ActAsync(HttpClient client, string action, Guid year, LeavePolicyResponse policy, bool reviewed = false)
	{
		var body = new LeavePolicyUpdateRequest { PolicyTitle = policy.PolicyTitle, Status = "Active", EffectiveFrom = policy.EffectiveFrom, Rules = Request().Rules, ExpectedVersion = policy.Version };
		if (reviewed && action is "edit" or "default" or "archive")
			await AttachReviewAsync(client, year, policy.Id, policy.Version, action == "edit" ? "update" : action == "default" ? "set-default" : "archive", action == "edit" ? body : null);
		return await (action switch
		{
			"view" => client.GetAsync($"{Root}/{policy.Id}"),
			"create" => client.PostAsJsonAsync($"{Root}?academicYearId={year}", Request()),
			"edit" => client.PutAsJsonAsync($"{Root}/{policy.Id}", body),
			"default" => client.PatchAsync($"{Root}/{policy.Id}/set-default", null),
			"archive" => client.DeleteAsync($"{Root}/{policy.Id}"),
			"restore" => client.PostAsync($"{Root}/{policy.Id}/unarchive", null),
			_ => throw new ArgumentOutOfRangeException(nameof(action))
		});
	}

	private async Task<LeavePolicyImpactReviewDto> AttachReviewAsync(HttpClient client, Guid year, Guid id, int version, string action, LeavePolicyUpdateRequest? body = null)
	{
		var date = Factory.Services.GetRequiredService<Volo.Abp.Timing.IClock>().Now.Date;
		var scope = new LeavePolicyReviewScope { AsOfDate = date, ExpectedVersion = version };
		if (body != null) body.ImpactScope = scope;
		using var response = await client.PostAsJsonAsync($"{Root}/impact/preview", new LeavePolicyImpactRequest
			{ AcademicYearId = year, PolicyId = id, Action = action, AsOfDate = date, ExpectedVersion = version, Proposal = body });
		response.EnsureSuccessStatusCode();
		var preview = (await response.Content.ReadFromJsonAsync<LeavePolicyImpactReviewDto>(JsonOptions))!;
		client.DefaultRequestHeaders.Remove(HrPolicyImpactConfirmationService.HeaderName);
		client.DefaultRequestHeaders.Remove(LeavePolicyMutationReview.ScopeHeader);
		if (preview.ConfirmationToken != null) client.DefaultRequestHeaders.Add(HrPolicyImpactConfirmationService.HeaderName, preview.ConfirmationToken);
		client.DefaultRequestHeaders.Add(LeavePolicyMutationReview.ScopeHeader, JsonSerializer.Serialize(scope, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
		return preview;
	}

	private async Task<(Guid, LeavePolicyResponse)> CreateFixtureAsync()
	{
		Guid yearId;
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(Guid.Parse(TestAuthHandler.TestTenantId)))
		using (var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var year = await scope.ServiceProvider.GetRequiredService<AcademicYearManager>().CreateAsync($"Policy safety {Guid.NewGuid():N}", DateTime.UtcNow.Date.AddDays(-1), DateTime.UtcNow.Date.AddYears(1));
			await scope.ServiceProvider.GetRequiredService<IRepository<AcademicYear, Guid>>().InsertAsync(year, true);
			yearId = year.Id;
			await unit.CompleteAsync();
		}
		(await Client.PostAsJsonAsync($"{Root}?academicYearId={yearId}", Request())).EnsureSuccessStatusCode();
		var response = await Client.PostAsJsonAsync($"{Root}?academicYearId={yearId}", Request());
		response.EnsureSuccessStatusCode();
		return (yearId, (await response.Content.ReadFromJsonAsync<LeavePolicyResponse>(JsonOptions))!);
	}

	private static LeavePolicyCreateRequest Request() => new()
	{
		PolicyTitle = $"Synthetic policy {Guid.NewGuid():N}",
		Status = "Active",
		EffectiveFrom = DateTime.UtcNow.Date,
		Rules = [new() { PaidType = "Paid", LeaveCount = 0 }, new() { PaidType = "Unpaid", EntitlementMode = "Unlimited", LeaveCount = null, AllowNegativeBalance = true }]
	};
}
