using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.ConcessionRequests;

[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class ConcessionAdministratorDecisionApi_Tests(ShikshaMitraApiTestFactory factory)
	: ConcessionRequestApiTestsBase(factory)
{
	[Theory]
	[InlineData("approve")]
	[InlineData("reject")]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Administrator_can_decide_own_request_and_replay_own_command(string decision)
	{
		var (owner, requestId, ownerId) = await CreateOwnedRequestAsync();
		using var client = owner;
		await SetAdministratorAsync(ownerId, true);
		var detail = (await client.GetFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(
			$"{BaseUrl}/{requestId}", JsonOptions))!.Data!;
		detail.AvailableActions.ShouldContain(decision);
		var context = await client.GetFromJsonAsync<ConcessionApprovalContextDto>(
			$"{BaseUrl}/{requestId}/approval-context", JsonOptions);
		object input = decision == "approve"
			? new ApproveConcessionRequestDto
			{
				RequestConcurrencyStamp = context!.RequestConcurrencyStamp,
				PreviewFingerprint = context.PreviewFingerprint,
				IdempotencyKey = Guid.NewGuid().ToString("N"),
			}
			: new RejectConcessionRequestDto
			{
				Message = "Administrator rejected their own request.",
				RequestConcurrencyStamp = detail.ConcurrencyStamp,
				IdempotencyKey = Guid.NewGuid().ToString("N"),
			};
		var response = await client.PostAsJsonAsync(
			$"{ConcessionRequestsBaseUrl}/{requestId}/{decision}", input, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var decided = (await client.GetFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(
			$"{BaseUrl}/{requestId}", JsonOptions))!.Data!;
		decided.Status.ShouldBe(decision == "approve" ? ConcessionRequestStatus.Approved : ConcessionRequestStatus.Rejected);
		decided.DecidedById.ShouldBe(ownerId);
		var replay = await client.PostAsJsonAsync(
			$"{ConcessionRequestsBaseUrl}/{requestId}/{decision}", input, JsonOptions);
		replay.StatusCode.ShouldBe(HttpStatusCode.OK, await replay.Content.ReadAsStringAsync());
		await SetAdministratorAsync(ownerId, false);
		var revoked = await client.PostAsJsonAsync(
			$"{ConcessionRequestsBaseUrl}/{requestId}/{decision}", input, JsonOptions);
		revoked.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await revoked.Content.ReadAsStringAsync()).ShouldContain(FinanceConcessionErrorCodes.ApproverCannotBeRequestor);
	}

	[Theory]
	[InlineData("approve")]
	[InlineData("reject")]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Administrator_still_requires_the_exact_decision_permission(string decision)
	{
		var (owner, requestId, ownerId) = await CreateOwnedRequestAsync();
		using var client = owner;
		await SetAdministratorAsync(ownerId, true);
		client.DefaultRequestHeaders.Add(AlwaysGrantPermissionChecker.TestPermissionsHeader,
			ShikshaMitraSubmodulePermissions.Finance.Fees_Concession.View);
		object input = decision == "approve"
			? new ApproveConcessionRequestDto { RequestConcurrencyStamp = "stamp", PreviewFingerprint = "fingerprint" }
			: new RejectConcessionRequestDto { RequestConcurrencyStamp = "stamp", Message = "Administrator requested rejection." };
		var denied = await client.PostAsJsonAsync(
			$"{ConcessionRequestsBaseUrl}/{requestId}/{decision}", input, JsonOptions);
		denied.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
	}

	private async Task<(HttpClient Client, Guid RequestId, Guid OwnerId)> CreateOwnedRequestAsync()
	{
		var owner = await CreateScopedCheckerClientAsync();
		// A role claim alone must not grant the own-request exception.
		owner.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "admin");
		var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Factory.Services);
		var typeId = await ConcessionRequestTestHelper.CreateConcessionTypeAsync(Client);
		var categoryId = await ConcessionRequestTestHelper.CreateFeeCategoryAsync(Client);
		var headId = await ConcessionRequestTestHelper.CreateFeeHeadAsync(Client, categoryId);
		var requestId = await ConcessionRequestTestHelper.CreateConcessionRequestAsync(owner, studentId, typeId, [headId]);
		var detail = (await owner.GetFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(
			$"{BaseUrl}/{requestId}", JsonOptions))!.Data!;
		detail.Status.ShouldBe(ConcessionRequestStatus.Pending);
		detail.AvailableActions.ShouldNotContain("approve");
		detail.AvailableActions.ShouldNotContain("reject");
		return (owner, requestId, detail.RequesterId!.Value);
	}

	private async Task SetAdministratorAsync(Guid userId, bool enabled)
	{
		using var scope = Factory.Services.CreateScope();
		var services = scope.ServiceProvider;
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		using (services.GetRequiredService<ICurrentTenant>().Change(tenantId))
		using (var uow = services.GetRequiredService<IUnitOfWorkManager>().Begin(new AbpUnitOfWorkOptions(), requiresNew: true))
		{
			var role = services.GetRequiredService<IOptions<UserScopeOptions>>().Value.AdminRoles.First();
			var roles = services.GetRequiredService<IdentityRoleManager>();
			if (await roles.FindByNameAsync(role) is null)
				(await roles.CreateAsync(new IdentityRole(Guid.NewGuid(), role, tenantId))).Succeeded.ShouldBeTrue();
			var users = services.GetRequiredService<IdentityUserManager>();
			var user = await users.GetByIdAsync(userId);
			var result = enabled ? await users.AddToRoleAsync(user, role) : await users.RemoveFromRoleAsync(user, role);
			result.Succeeded.ShouldBeTrue();
			await uow.CompleteAsync();
		}
	}
}
