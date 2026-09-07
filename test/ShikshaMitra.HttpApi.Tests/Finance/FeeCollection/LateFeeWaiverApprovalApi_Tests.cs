using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.ConcessionRequests;
using ShikshaMitra.Finance.Fees.Collections;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using ShikshaMitra.Infrastructure.Auth;
using ShikshaMitra.Infrastructure.Traits;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.FeeCollections;

/// <summary>
/// Phase 4e (collection-policy-implementation-plan.md §4e) — verifies the threshold-aware
/// waiver-approval permission actually gates at the HTTP layer: ApproveWaiver alone only
/// covers waivers up to the resolved policy's own approvalThresholdAmount; above it, the
/// caller also needs ApproveWaiverAboveThreshold or gets a 403, not a silent pass-through.
/// Uses HttpApi.Tests (not Application.Tests) deliberately — Application.Tests runs under
/// AddAlwaysAllowAuthorization(), which bypasses every permission check, so this behavior is
/// only observable through a real HTTP request carrying a restricted permission set.
/// </summary>
[Collection(ApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public class LateFeeWaiverApprovalApi_Tests : ShikshaMitraApiTestBase
{
	private static readonly Guid TestTenantId = Guid.Parse(TestAuthHandler.TestTenantId);

	public LateFeeWaiverApprovalApi_Tests(ShikshaMitraApiTestFactory factory) : base(factory)
	{
		ConcessionRequestTestHelper.DefaultAcademicYearId = factory.SeededAcademicYearId;
		ConcessionRequestTestHelper.DefaultMediumId = factory.SeededMediumId;
		ConcessionRequestTestHelper.DefaultStandardId = factory.SeededStandardId;
		ConcessionRequestTestHelper.DefaultDivisionId = factory.SeededDivisionId;
	}

	private HttpClient CreateClientWithPermissions(params string[] permissions)
	{
		var client = Factory.CreateClient();
		client.DefaultRequestHeaders.Add(TestAuthHandler.TestRolesHeader, "accountant");
		client.DefaultRequestHeaders.Add(
			AlwaysGrantPermissionChecker.TestPermissionsHeader,
			permissions.Length == 0 ? "__none__" : string.Join(",", permissions));
		return client;
	}

	/// <summary>
	/// Seeds a student with an overdue due line against a Collection Policy carrying the given
	/// ApprovalThresholdAmount, then requests (but does not approve) a waiver for the given
	/// amount. Returns the waiver's ID for the test to approve via HTTP with varying permissions.
	/// </summary>
	private async Task<Guid> SeedPendingWaiverAsync(decimal? approvalThresholdAmount, decimal requestedWaiverAmount)
	{
		var feeCategoryId = await ConcessionRequestTestHelper.CreateFeeCategoryAsync(Client);
		var feeHeadId = await ConcessionRequestTestHelper.CreateFeeHeadAsync(Client, feeCategoryId);
		var (studentId, _) = await ConcessionRequestTestHelper.CreateStudentAsync(Client);

		using var scope = Factory.Services.CreateScope();
		var currentTenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
		var uowManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
		var feeStructureRepo = scope.ServiceProvider.GetRequiredService<IRepository<FeeStructure, Guid>>();
		var studentFeeStructureRepo = scope.ServiceProvider.GetRequiredService<IRepository<StudentFeeStructure, Guid>>();
		var dueFeeRepo = scope.ServiceProvider.GetRequiredService<IDueFeeRepository>();
		var collectionPolicies = scope.ServiceProvider.GetRequiredService<ICollectionPolicyAppService>();
		var waivers = scope.ServiceProvider.GetRequiredService<ILateFeeWaiverAppService>();

		using var uow = uowManager.Begin(new Volo.Abp.Uow.AbpUnitOfWorkOptions(), requiresNew: true);
		using (currentTenant.Change(TestTenantId))
		{
			var policy = await collectionPolicies.CreateAsync(new CreateUpdateCollectionPolicyDto
			{
				AcademicYearId = ConcessionRequestTestHelper.DefaultAcademicYearId,
				PolicyName = $"Threshold Test Policy {Guid.NewGuid():N}",
				PolicyCode = $"CP-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
				Status = "Active",
				EnableLateFee = true,
				GracePeriodDays = 0,
				LateFeeType = "PerDay",
				AmountPerDay = 50,
				PercentageBase = "OutstandingAmount",
				CompoundFrequency = "Daily",
				RoundingMethod = "None",
				EnableWaiver = true,
				MaximumWaiverAmount = 10000,
				MaximumWaiverPercentage = 100,
				ApprovalThresholdAmount = approvalThresholdAmount,
				AllowOnlineCollection = true,
			});

			var feeStructure = new FeeStructure(
				Guid.NewGuid(), TestTenantId, Guid.NewGuid(), Guid.NewGuid(), $"Structure {Guid.NewGuid():N}");
			feeStructure.AttachCollectionPolicy(policy.Id);
			await feeStructureRepo.InsertAsync(feeStructure, autoSave: true);

			await studentFeeStructureRepo.InsertAsync(
				new StudentFeeStructure(Guid.NewGuid(), TestTenantId, studentId, feeStructure.Id), autoSave: true);

			var dueFee = new DueFee(
				id: Guid.NewGuid(), tenantId: TestTenantId, studentId: studentId, feeHeadId: feeHeadId,
				month: MonthEnum.April, year: 2025, isOneTime: false, amount: 20000m,
				dueDate: DateTime.UtcNow.Date.AddDays(-30));
			await dueFeeRepo.InsertAsync(dueFee, autoSave: true);

			var waiver = await waivers.ApplyWaiverAsync(new ApplyLateFeeWaiverInput
			{
				DueFeeId = dueFee.Id,
				RequestedAmount = requestedWaiverAmount,
				Reason = "Threshold test fixture.",
			});

			await uow.CompleteAsync();
			return waiver.Id;
		}
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task ApproveWaiver_Alone_Approves_A_Waiver_At_Or_Below_The_Policy_Threshold()
	{
		var waiverId = await SeedPendingWaiverAsync(approvalThresholdAmount: 2000, requestedWaiverAmount: 500);

		using var client = CreateClientWithPermissions(
			ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.ApproveWaiver);

		var response = await client.PostAsync($"/api/finance/late-fee-waivers/{waiverId}/approve", null);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var dto = await response.Content.ReadFromJsonAsync<LateFeeWaiverApplicationDto>(JsonOptions);
		dto.ShouldNotBeNull();
		dto!.Status.ShouldBe("Approved");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task ApproveWaiver_Alone_Is_Blocked_Above_The_Policy_Threshold()
	{
		var waiverId = await SeedPendingWaiverAsync(approvalThresholdAmount: 100, requestedWaiverAmount: 5000);

		using var client = CreateClientWithPermissions(
			ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.ApproveWaiver);

		var response = await client.PostAsync($"/api/finance/late-fee-waivers/{waiverId}/approve", null);

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task ApproveWaiverAboveThreshold_Permits_Approving_Above_The_Policy_Threshold()
	{
		var waiverId = await SeedPendingWaiverAsync(approvalThresholdAmount: 100, requestedWaiverAmount: 5000);

		using var client = CreateClientWithPermissions(
			ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.ApproveWaiver,
			ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.ApproveWaiverAboveThreshold);

		var response = await client.PostAsync($"/api/finance/late-fee-waivers/{waiverId}/approve", null);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var dto = await response.Content.ReadFromJsonAsync<LateFeeWaiverApplicationDto>(JsonOptions);
		dto.ShouldNotBeNull();
		dto!.Status.ShouldBe("Approved");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Important)]
	public async Task No_Threshold_Set_On_The_Policy_Never_Requires_The_Higher_Permission()
	{
		// approvalThresholdAmount left null (no threshold configured) — ApproveWaiver alone
		// must always suffice, no matter how large the requested amount is.
		var waiverId = await SeedPendingWaiverAsync(approvalThresholdAmount: null, requestedWaiverAmount: 999999);

		using var client = CreateClientWithPermissions(
			ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.ApproveWaiver);

		var response = await client.PostAsync($"/api/finance/late-fee-waivers/{waiverId}/approve", null);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
	}
}
