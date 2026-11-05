using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ShikshaMitra.Common;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Concessions;
using ShikshaMitra.Finance.Fees.Concessions.Dtos;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Finance.Fees.ConcessionRequests;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using ShikshaMitra.Security.UserScope;
using Shouldly;
using Volo.Abp.Identity;
using Volo.Abp.Domain.Repositories;
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
	public async Task Administrator_cannot_manually_decide_own_request(string decision)
	{
		var (owner, requestId, ownerId) = await CreateOwnedRequestAsync();
		using var client = owner;
		await SetAdministratorAsync(ownerId, true);
		var detail = (await client.GetFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(
			$"{BaseUrl}/{requestId}", JsonOptions))!.Data!;
		detail.AvailableActions.ShouldNotContain(decision);
		object input = decision == "approve"
			? new ApproveConcessionRequestDto
			{
				RequestConcurrencyStamp = detail.ConcurrencyStamp,
				PreviewFingerprint = new string('0', 64),
				IdempotencyKey = Guid.NewGuid().ToString("N"),
			}
			: new RejectConcessionRequestDto
			{
				Message = "Administrator attempted to reject their own request.",
				RequestConcurrencyStamp = detail.ConcurrencyStamp,
				IdempotencyKey = Guid.NewGuid().ToString("N"),
			};
		var response = await client.PostAsJsonAsync(
			$"{ConcessionRequestsBaseUrl}/{requestId}/{decision}", input, JsonOptions);
		response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
		(await response.Content.ReadAsStringAsync()).ShouldContain(
			FinanceConcessionErrorCodes.ApproverCannotBeRequestor);
		await SetAdministratorAsync(ownerId, false);
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
			? new ApproveConcessionRequestDto
			{
				RequestConcurrencyStamp = "stamp", PreviewFingerprint = new string('0', 64),
				IdempotencyKey = Guid.NewGuid().ToString("N"),
			}
			: new RejectConcessionRequestDto
			{
				RequestConcurrencyStamp = "stamp", Message = "Administrator requested rejection.",
				IdempotencyKey = Guid.NewGuid().ToString("N"),
			};
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
		var tenantId = Guid.Parse(TestAuthHandler.TestTenantId);
		var policyId = Guid.NewGuid();
		var versionId = Guid.NewGuid();
		var structureId = Guid.NewGuid();
		var assignmentId = Guid.NewGuid();
		using (var scope = Factory.Services.CreateScope())
		using (scope.ServiceProvider.GetRequiredService<ICurrentTenant>().Change(tenantId))
		using (var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>().Begin(requiresNew: true))
		{
			var services = scope.ServiceProvider;
			var metadata = new FeeStructureMetadata(Guid.NewGuid(), tenantId, Factory.SeededAcademicYearId,
				Factory.SeededBoardId, Factory.SeededMediumId, null, $"Admin decision {structureId:N}");
			await services.GetRequiredService<IRepository<FeeStructureMetadata, Guid>>().InsertAsync(metadata);
			await services.GetRequiredService<IRepository<FeeStructure, Guid>>().InsertAsync(new FeeStructure(
				structureId, tenantId, metadata.Id, Factory.SeededStandardId, metadata.Name!));
			var policy = new ConcessionPolicy(policyId, tenantId, Factory.SeededAcademicYearId,
				new ConcessionPolicyUpdateModel($"Admin decision {policyId:N}", $"AD{policyId:N}"[..20], null,
					ConcessionPolicyStatus.Active, false, ConcessionValueSource.RequestEnteredWithinLimits,
					ConcessionCalculationMethod.Percentage, null, null, 100m, null,
					ConcessionApplyOn.AllOutstanding, RoundingMethodType.RoundUp));
			await services.GetRequiredService<IRepository<ConcessionPolicy, Guid>>().InsertAsync(policy);
			await services.GetRequiredService<IRepository<FinancePolicyVersion, Guid>>().InsertAsync(
				new FinancePolicyVersion(versionId, tenantId, "concession", policyId, policy.AcademicYearId,
					policy.Version, policy.PolicyName, "Active", JsonSerializer.Serialize(policy.ToSnapshot()), "created"));
			await services.GetRequiredService<IRepository<StudentFeeStructure, Guid>>().InsertAsync(
				new StudentFeeStructure(assignmentId, tenantId, studentId, structureId,
					Factory.SeededAcademicYearId, policyId, versionId, ConcessionFeeHeadScope.EntireFeeStructure, []));
			var headRepository = services.GetRequiredService<IRepository<FeeHead, Guid>>();
			var head = await headRepository.GetAsync(headId);
			head.Update(head.FeeCategoryId, head.Name, head.Module, true, head.Description, head.IncomeCategoryId);
			await headRepository.UpdateAsync(head);
			var due = new DueFee(Guid.NewGuid(), tenantId, studentId, headId, MonthEnum.June, 2026,
				false, 2000m, new DateTime(2026, 6, 10));
			due.BindAssignment(assignmentId);
			await services.GetRequiredService<IRepository<DueFee, Guid>>().InsertAsync(due);
			await uow.CompleteAsync();
		}
		var draft = await ConcessionRequestTestHelper.CreateDraftAsync(owner, new CreateConcessionDraftDto
		{
			StudentId = studentId, StudentFeeStructureId = assignmentId, ConcessionTypeId = typeId,
			ConcessionPolicyId = policyId, ConcessionPolicyVersionId = versionId,
		});
		var requestId = draft.ConcessionRequestId;
		var detail = (await owner.GetFromJsonAsync<ApiDataResponseDto<ConcessionRequestDetailDto>>(
			$"{BaseUrl}/{requestId}", JsonOptions))!.Data!;
		detail.Status.ShouldBe(ConcessionRequestStatus.Draft);
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
