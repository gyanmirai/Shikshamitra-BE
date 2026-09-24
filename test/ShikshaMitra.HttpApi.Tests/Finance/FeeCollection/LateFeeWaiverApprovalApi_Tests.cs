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
using Volo.Abp.Application.Dtos;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;
using Xunit;

namespace ShikshaMitra.Finance.FeeCollections;

/// <summary>
/// Verifies that a policy configured to require approval creates a pending waiver and that
/// the normal ApproveWaiver permission gates the approval endpoint. Amount thresholds were
/// intentionally removed from the collection-policy model.
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
	/// Seeds a student with an overdue due line and requests a waiver that requires approval.
	/// </summary>
	private async Task<Guid> SeedPendingWaiverAsync(decimal requestedWaiverAmount)
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
				LateFeeApplyOnFeeHeads = [feeHeadId],
				RequiresApproval = true,
				ApprovalRoleIds = [Guid.NewGuid()],
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
	public async Task ApproveWaiver_Permission_Approves_A_Pending_Waiver_Regardless_Of_Amount()
	{
		var waiverId = await SeedPendingWaiverAsync(requestedWaiverAmount: 500);

		using var client = CreateClientWithPermissions(
			ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.ApproveWaiver);

		var response = await client.PostAsync($"/api/finance/late-fee-waivers/{waiverId}/approve", null);

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var dto = await response.Content.ReadFromJsonAsync<LateFeeWaiverApplicationDto>(JsonOptions);
		dto.ShouldNotBeNull();
		dto!.Status.ShouldBe("Applied");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Approval_Is_Blocked_Without_ApproveWaiver_Permission()
	{
		var waiverId = await SeedPendingWaiverAsync(requestedWaiverAmount: 500);

		using var client = CreateClientWithPermissions();

		var response = await client.PostAsync($"/api/finance/late-fee-waivers/{waiverId}/approve", null);

		response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await response.Content.ReadAsStringAsync());
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task ViewWaivers_Lists_A_Pending_Request()
	{
		var waiverId = await SeedPendingWaiverAsync(requestedWaiverAmount: 125);
		using var client = CreateClientWithPermissions(
			ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.ViewWaivers);

		var response = await client.GetAsync("/api/finance/late-fee-waivers/list?status=PendingApproval&maxResultCount=25");

		response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
		var page = await response.Content.ReadFromJsonAsync<PagedResultDto<LateFeeWaiverApplicationDto>>(JsonOptions);
		page.ShouldNotBeNull();
		page!.Items.ShouldContain(x => x.Id == waiverId && x.Status == "PendingApproval");
	}

	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task RejectWaiver_Requires_Reason_And_Reject_Permission()
	{
		var waiverId = await SeedPendingWaiverAsync(requestedWaiverAmount: 125);
		using var unauthorizedClient = CreateClientWithPermissions();
		var forbidden = await unauthorizedClient.PostAsJsonAsync(
			$"/api/finance/late-fee-waivers/{waiverId}/reject", new { reason = "Not approved." });
		forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden, await forbidden.Content.ReadAsStringAsync());

		using var reviewerClient = CreateClientWithPermissions(
			ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.Finance.Policies_CollectionPolicy.RejectWaiver);
		var rejected = await reviewerClient.PostAsJsonAsync(
			$"/api/finance/late-fee-waivers/{waiverId}/reject", new { reason = "Supporting evidence is insufficient." });

		rejected.StatusCode.ShouldBe(HttpStatusCode.OK, await rejected.Content.ReadAsStringAsync());
		var dto = await rejected.Content.ReadFromJsonAsync<LateFeeWaiverApplicationDto>(JsonOptions);
		dto.ShouldNotBeNull();
		dto!.Status.ShouldBe("Rejected");
		dto.DecisionReason.ShouldBe("Supporting evidence is insufficient.");
	}
}
