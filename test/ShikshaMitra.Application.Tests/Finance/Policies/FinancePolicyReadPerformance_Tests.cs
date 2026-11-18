using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.EntityFrameworkCore;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
	[Theory]
	[InlineData(null, "Charlie", "Bravo")]
	[InlineData("policyName asc", "Bravo", "Charlie")]
	[InlineData("policyName desc", "Charlie", "Bravo")]
	[InlineData("amountMode asc", "Delta", "Charlie")]
	[InlineData("amountMode desc", "Charlie", "Delta")]
	[InlineData("allowedPayoutMethods asc", "Delta", "Bravo")]
	[InlineData("allowedPayoutMethods desc", "Bravo", "Alpha")]
	[InlineData("feeHeadScope asc", "Delta", "Bravo")]
	[InlineData("feeHeadScope desc", "Bravo", "Alpha")]
	[InlineData("allowPartialRefund asc", "Charlie", "Bravo")]
	[InlineData("allowPartialRefund desc", "Delta", "Alpha")]
	[InlineData("updatedAt asc", "Bravo", "Charlie")]
	[InlineData("updatedAt desc", "Charlie", "Bravo")]
	[InlineData("status asc", "Charlie", "Alpha")]
	[InlineData("status desc", "Bravo", "Charlie")]
	public async Task Refund_policy_pages_limit_rows_in_SQL_before_mapping(string? sorting, string first, string second)
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (year, _, _, _) = await SeedStandardAsync();
		var service = GetRequiredService<IRefundPolicyAppService>();
		await WithUnitOfWorkAsync(async () =>
		{
			var names = new[] { "Alpha", "Bravo", "Charlie", "Delta" };
			var idPrefix = Guid.NewGuid().ToString("N")[..20];
			var modes = new[] { RefundAmountMode.FixedAmount, RefundAmountMode.ManualUpToEligible,
				RefundAmountMode.Percentage, RefundAmountMode.RemainingPaidAmount };
			for (var i = 0; i < 4; i++)
			{
				var selected = i is 1 or 2;
				var model = new RefundPolicyUpdateModel(
					PolicyName: names[i], PolicyCode: $"SORT-{i}", Description: null,
					Status: i == 3 ? RefundPolicyStatus.Active : RefundPolicyStatus.Draft, IsDefault: i == 3,
					FeeHeadScope: selected ? RefundFeeHeadScope.SelectedFeeHeads : RefundFeeHeadScope.AllFeeHeads,
					ApplicableFeeHeadIds: selected ? Enumerable.Range(0, i).Select(_ => Guid.NewGuid()).ToArray() : [],
					IncludePaidCharges: false, IncludePaidLateFee: false, AllowPartialRefund: i % 2 == 0,
					AmountMode: modes[i], PercentageValue: i == 2 ? 10m : null, FixedAmount: i == 0 ? 100m : null,
					MaximumPerRequest: null, MaximumPerStudentYear: null, RoundingMethod: RoundingMethodType.RoundUp,
					DeductionType: RefundDeductionType.None, DeductionValue: null, MaximumDeduction: null,
					DeductionExplanation: null, AllowedPayoutMethods: Enumerable.Range(0, i == 3 ? 1 : i + 1)
						.Select(value => (RefundPayoutMethod)value).ToArray(), ProcessingTargetDays: null);
				var entity = new RefundPolicy(Guid.ParseExact($"{idPrefix}{i + 1:x12}", "N"), TestTenantId, year, model);
				if (i == 2) entity.Archive();
				entity.CreationTime = new DateTime(2026, 1, i + 1);
				entity.LastModificationTime = entity.CreationTime;
				await GetRequiredService<IRepository<RefundPolicy, Guid>>().InsertAsync(entity, true);
			}
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			using var reads = new FinanceSettingsReadCapture(db.Database.GetDbConnection());
			var result = await service.GetListAsync(new() { AcademicYearId = year, IncludeArchived = true, Sorting = sorting, SkipCount = 1, MaxResultCount = 2 });
			result.TotalCount.ShouldBe(4);
			result.Items.Count.ShouldBe(2);
			result.Items.Select(x => x.PolicyName).ShouldBe(new[] { first, second });
			reads.Commands.ShouldContain(sql => sql.Contains("Finance_RefundPolicies") && sql.Contains("LIMIT"),
				"the database must return only the requested page, even for JSON-backed sorts");
		});
	}

	[Fact]
	public async Task Proration_policy_usage_reads_distinct_references_without_assignment_payloads()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (year, _, _, head) = await SeedStandardAsync();
		var service = GetRequiredService<IProrationPolicyAppService>();
		await service.CreateAsync(new()
		{
			AcademicYearId = year, PolicyName = "Proration read test",
			FeeHeadRules = [new() { FeeHeadId = head, RuleType = "FullCharge", RoundingMode = "RoundUpRupee" }]
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			using var reads = new FinanceSettingsReadCapture(db.Database.GetDbConnection());
			var result = await service.GetListAsync(new() { AcademicYearId = year });
			result.Items.Single().IsUsedInDownstream.ShouldBeFalse();
			reads.Commands.ShouldContain(sql => sql.Contains("ProrationPolicyId") && sql.Contains("SELECT DISTINCT"));
			reads.Commands.ShouldNotContain(sql => sql.Contains("StudentFeeStructures") && sql.Contains("SnapshotJson"),
				"usage checks must not hydrate each student's captured fee schedule");
		});
	}

	[Fact]
	public async Task Current_policy_version_lookup_excludes_snapshots_and_unrelated_historical_versions()
	{
		using var reviewer = Reviewer();
		using var tenant = _currentTenant.Change(TestTenantId);
		var (year, _, _, head) = await SeedStandardAsync();
		var id = await CreateCollectionPolicyAsync(year, head, "Current version read test");
		var policy = await _collectionPolicies.GetAsync(id);
		policy.CurrentVersionId.ShouldNotBeNull();
		await WithUnitOfWorkAsync(async () =>
		{
			await GetRequiredService<IRepository<FinancePolicyVersion, Guid>>().InsertAsync(new(
				Guid.NewGuid(), TestTenantId, "collection", id, year, policy.Version + 100,
				"Unrelated historical version", "Active", new string('x', 10000), "edited"), true);
		});
		await WithUnitOfWorkAsync(async () =>
		{
			var db = await GetRequiredService<IDbContextProvider<ShikshaMitraDbContext>>().GetDbContextAsync();
			using var reads = new FinanceSettingsReadCapture(db.Database.GetDbConnection());
			var result = await _collectionPolicies.GetAsync(id);
			result.CurrentVersionId.ShouldBe(policy.CurrentVersionId);
			reads.Commands.ShouldNotContain(sql => sql.Contains("Finance_PolicyVersions") && sql.Contains("SnapshotJson"),
				"resolving one version ID must not load historical snapshots");
			reads.Commands.ShouldContain(sql => sql.Contains("Finance_PolicyVersions") && sql.Contains("WHERE") &&
				sql.Substring(sql.IndexOf("WHERE", StringComparison.Ordinal)).Contains("VersionNumber"),
				"the exact current version must be filtered in SQL, not searched through history in memory");
		});
	}
}
