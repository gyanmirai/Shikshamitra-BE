using System;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.Infrastructure.Auth;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Xunit;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public partial class LeavePolicySafetyApi_Tests
{
	[Theory]
	[InlineData("effectiveStaff")]
	[InlineData("assignmentReferences")]
	[InlineData("requestReferences")]
	[InlineData("payrollExposure")]
	public async Task Impact_Drilldowns_Require_Owning_Read_Grant_Even_For_Zero_Records(string metric)
	{
		var (year, policy) = await CreateFixtureAsync();
		using var reader = Restricted("View");
		var assessment = new LeavePolicyImpactRequest { AcademicYearId = year, PolicyId = policy.Id, Action = "inspect",
			AsOfDate = Factory.Services.GetRequiredService<Volo.Abp.Timing.IClock>().Now.Date };
		var response = await reader.PostAsJsonAsync($"{Root}/impact/preview", assessment);
		response.EnsureSuccessStatusCode();
		var preview = (await response.Content.ReadFromJsonAsync<LeavePolicyImpactReviewDto>(JsonOptions))!;
		preview.Impact.Measures[metric].Count.ShouldBe(0);
		var denied = await reader.PostAsJsonAsync($"{Root}/impact/details", new LeavePolicyImpactDetailsRequest
			{ Assessment = assessment, Fingerprint = preview.Impact.Fingerprint, Metric = metric });
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Impact_Drilldowns_Reject_Stale_Assessments_And_Foreign_Year()
	{
		var (year, policy) = await CreateFixtureAsync();
		var assessment = new LeavePolicyImpactRequest { AcademicYearId = year, PolicyId = policy.Id, Action = "inspect",
			AsOfDate = Factory.Services.GetRequiredService<Volo.Abp.Timing.IClock>().Now.Date };
		var response = await Client.PostAsJsonAsync($"{Root}/impact/preview", assessment);
		response.EnsureSuccessStatusCode();
		var preview = (await response.Content.ReadFromJsonAsync<LeavePolicyImpactReviewDto>(JsonOptions))!;
		var page = await Client.PostAsJsonAsync($"{Root}/impact/details", new LeavePolicyImpactDetailsRequest
			{ Assessment = assessment, Fingerprint = preview.Impact.Fingerprint, Metric = "requestReferences" });
		page.EnsureSuccessStatusCode();
		var rows = (await page.Content.ReadFromJsonAsync<LeavePolicyImpactPageDto>(JsonOptions))!;
		rows.AssessedTotalCount.ShouldBe(0); rows.TotalCount.ShouldBe(0); rows.Items.ShouldBeEmpty();
		(await ActAsync(Client, "default", year, policy, reviewed: true)).EnsureSuccessStatusCode();
		var stale = await Client.PostAsJsonAsync($"{Root}/impact/details", new LeavePolicyImpactDetailsRequest
			{ Assessment = assessment, Fingerprint = preview.Impact.Fingerprint, Metric = "requestReferences" });
		stale.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await stale.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.HrPolicyImpactConfirmationInvalid);
		using var foreign = Restricted("View");
		foreign.DefaultRequestHeaders.Add(TestAuthHandler.TestTenantIdHeader, Guid.NewGuid().ToString());
		(await foreign.PostAsJsonAsync($"{Root}/impact/preview", assessment)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task Impact_Token_Cannot_Be_Used_By_Another_Actor()
	{
		var (year, policy) = await CreateFixtureAsync();
		using var setter = Restricted("SetDefault");
		await AttachReviewAsync(setter, year, policy.Id, policy.Version, "set-default");
		setter.DefaultRequestHeaders.Add(TestAuthHandler.TestUserIdHeader, Guid.NewGuid().ToString());
		var response = await setter.PatchAsync($"{Root}/{policy.Id}/set-default", null);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await response.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.HrPolicyImpactConfirmationInvalid);
		(await Client.GetFromJsonAsync<LeavePolicyDetailResponse>($"{Root}/{policy.Id}", JsonOptions))!.IsDefault.ShouldBeFalse();
	}
	[Fact]
	public async Task History_And_Draft_Restore_Use_Existing_Leave_Grants_And_Fresh_Identities()
	{
		var (year, policy) = await CreateFixtureAsync();
		using var reader = Restricted("View");
		var path = $"/api/hr/leave/policies/{policy.Id}/versions";
		var history = await reader.GetFromJsonAsync<PagedResultDto<HrPolicyVersionDto>>(path, JsonOptions);
		history!.Items.Count.ShouldBe(1);
		history.Items[0].ChangeAction.ShouldBe("created");
		using var creator = Restricted("Add");
		var restoredResponse = await creator.PostAsJsonAsync($"{path}/{history.Items[0].Id}/restore-draft", new HrPolicyRestoreDraftRequestDto { Name = $"Restored {Guid.NewGuid():N}", ChangeReason = "Synthetic history restore" });
		restoredResponse.EnsureSuccessStatusCode();
		var restored = (await restoredResponse.Content.ReadFromJsonAsync<LeavePolicyDetailResponse>(JsonOptions))!;
		restored.Id.ShouldNotBe(policy.Id);
		restored.Status.ShouldBe("Draft");
		restored.IsDefault.ShouldBeFalse();
		restored.AcademicYearId.ShouldBe(year);
		restored.SourceVersionId.ShouldBe(history.Items[0].Id);
		restored.SourcePolicyId.ShouldBe(policy.Id);
		restored.Rules.Select(x => x.Id).Intersect(policy.Rules.Select(x => x.Id)).ShouldBeEmpty();
		(await reader.GetFromJsonAsync<LeavePolicyDetailResponse>($"{Root}/{policy.Id}", JsonOptions))!.Version.ShouldBe(1);
		(await reader.PostAsJsonAsync($"{path}/{history.Items[0].Id}/restore-draft", new HrPolicyRestoreDraftRequestDto { Name = "Denied" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
		using var generic = Factory.CreateClient();
		generic.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader, "HR.Policies.View");
		generic.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "Teacher");
		(await generic.GetAsync(path)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	[Fact]
	public async Task Active_Edit_Requires_Server_Confirmation_And_Rejects_Changed_Proposal_At_Same_Count()
	{
		var (year, policy) = await CreateFixtureAsync();
		using var editor = Restricted("Edit");
		var body = new LeavePolicyUpdateRequest { PolicyTitle = policy.PolicyTitle, Status = "Active", EffectiveFrom = policy.EffectiveFrom,
			ExpectedVersion = policy.Version, Rules = Request().Rules };
		body.Rules[0].LeaveCount = 1;
		var denied = await editor.PutAsJsonAsync($"{Root}/{policy.Id}", body);
		denied.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await denied.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.HrPolicyImpactConfirmationRequired);
		var preview = await AttachReviewAsync(editor, year, policy.Id, policy.Version, "update", body);
		preview.RequiresConfirmation.ShouldBeTrue();
		body.Rules[0].LeaveCount = 2;
		var stale = await editor.PutAsJsonAsync($"{Root}/{policy.Id}", body);
		stale.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await stale.Content.ReadAsStringAsync()).ShouldContain(HRErrorCodes.HrPolicyImpactConfirmationInvalid);
		(await Client.GetFromJsonAsync<LeavePolicyDetailResponse>($"{Root}/{policy.Id}", JsonOptions))!.Version.ShouldBe(1);
		await AttachReviewAsync(editor, year, policy.Id, policy.Version, "update", body);
		(await editor.PutAsJsonAsync($"{Root}/{policy.Id}", body)).EnsureSuccessStatusCode();
		var updated = (await Client.GetFromJsonAsync<LeavePolicyDetailResponse>($"{Root}/{policy.Id}", JsonOptions))!;
		updated.Version.ShouldBe(2);
		(await editor.PutAsJsonAsync($"{Root}/{policy.Id}", body)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task Default_Replacement_Records_Both_Policies_And_Stale_Review_Cannot_Write_Again()
	{
		var (year, policy) = await CreateFixtureAsync();
		var before = (await Client.GetFromJsonAsync<LeavePolicyListResponse>($"{Root}?academicYearId={year}&pageSize=100", JsonOptions))!;
		var previous = before.Items.Single(x => x.IsDefault);
		using var setter = Restricted("SetDefault");
		await AttachReviewAsync(setter, year, policy.Id, policy.Version, "set-default");
		(await setter.PatchAsync($"{Root}/{policy.Id}/set-default", null)).EnsureSuccessStatusCode();
		var promoted = (await Client.GetFromJsonAsync<LeavePolicyDetailResponse>($"{Root}/{policy.Id}", JsonOptions))!;
		var demoted = (await Client.GetFromJsonAsync<LeavePolicyDetailResponse>($"{Root}/{previous.Id}", JsonOptions))!;
		promoted.IsDefault.ShouldBeTrue(); promoted.Version.ShouldBe(2);
		demoted.IsDefault.ShouldBeFalse(); demoted.Version.ShouldBe(2);
		var history = await Client.GetFromJsonAsync<PagedResultDto<HrPolicyVersionDto>>($"/api/hr/leave/policies/{previous.Id}/versions", JsonOptions);
		history!.Items.ShouldContain(x => x.VersionNumber == 2 && x.ChangeAction == "set-default-cleared");
		(await setter.PatchAsync($"{Root}/{policy.Id}/set-default", null)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
	}
}
