using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class LateFeeCollectionRuntime_Tests
{
	[Fact]
	public async Task Legacy_Pinned_Selected_Heads_And_Overrides_Survive_A_New_Common_Policy_Revision()
	{
		using var tenant = _currentTenant.Change(TestTenantId);
		var (year, _, first, second) = await SeedCommonLateFeeAsync("Flat", 40);
		var assignment = await _studentFeeStructureRepo.GetAsync(first.StudentFeeStructureId!.Value);
		var policy = await _collectionPolicies.GetAsync(assignment.CollectionPolicyId!.Value);
		var original = await _policyVersionRepo.GetAsync(assignment.CollectionPolicyVersionId!.Value);
		var snapshot = CollectionPolicySnapshot.Deserialize(original.SnapshotJson);
		snapshot.PolicyName = "Historical Collection";
		snapshot.PolicyCode = $"LEG-{Guid.NewGuid():N}"[..16];
		snapshot.IsDefault = false;
		snapshot.ApplyLateFeeToAllFeeHeads = false;
		snapshot.LateFeeApplyOnFeeHeads = [first.FeeHeadId, second.FeeHeadId];
		snapshot.FeeHeadOverrides =
		[
			new(first.FeeHeadId, FeeHeadOverrideMode.CustomRule, new(LateFeeCalculationType.Flat, 25m, null,
				PercentageBaseType.OutstandingAmount, null, null, null, CompoundFrequencyType.Daily,
				Array.Empty<CollectionPolicyEscalationRule>(), RoundingMethodType.RoundUp, null, null)),
			new(second.FeeHeadId, FeeHeadOverrideMode.NoLateFee, null)
		];
		var legacyPolicy = CollectionPolicy.FromSnapshot(Guid.NewGuid(), TestTenantId, year, 1, snapshot);
		await GetRequiredService<IRepository<CollectionPolicy, Guid>>().InsertAsync(legacyPolicy, autoSave: true);
		var legacy = new FinancePolicyVersion(Guid.NewGuid(), TestTenantId, "collection", legacyPolicy.Id, year,
			1, snapshot.PolicyName, "Active", snapshot.Serialize(), "Historical fixture");
		await _policyVersionRepo.InsertAsync(legacy, autoSave: true);
		policy = await _collectionPolicies.GetAsync(legacyPolicy.Id);
		assignment.ChangeAssignedPolicies(null, null, ConcessionFeeHeadScope.EntireFeeStructure, [], legacyPolicy.Id,
			legacy.Id, null, null, null, null, "Pin historical policy", FakeAdminUserId, DateTime.UtcNow);
		await _studentFeeStructureRepo.UpdateAsync(assignment, autoSave: true);
		var head = await _feeHeadRepo.GetAsync(first.FeeHeadId);
		var excludedHead = new FeeHead(Guid.NewGuid(), TestTenantId, year, head.FeeCategoryId,
			"Historical excluded head", head.Module, false, null, head.IncomeCategoryId);
		await _feeHeadRepo.InsertAsync(excludedHead, autoSave: true);
		var excluded = new DueFee(Guid.NewGuid(), TestTenantId, first.StudentId, excludedHead.Id,
			first.Month, first.Year, false, 1000m, first.DueDate);
		excluded.BindAssignment(assignment.Id);
		await _dueFeeRepo.InsertAsync(excluded, autoSave: true);
		await _collectionPolicies.UpdateAsync(policy.Id, new CreateUpdateCollectionPolicyDto
		{
			AcademicYearId = year, PolicyName = policy.PolicyName, PolicyCode = policy.PolicyCode,
			Status = "Active", IsDefault = policy.IsDefault, EnableLateFee = true, LateFeeType = "Flat", FlatAmount = 125m,
			LateFeeIncomeCategoryId = head.IncomeCategoryId, LateFeeApplyOnFeeHeads = []
		});
		(await _collectionPolicies.GetAsync(policy.Id)).ApplyLateFeeToAllFeeHeads.ShouldBeTrue();
		var balances = await ReadCommonBalancesAsync([first, second, excluded], year, DateTime.UtcNow.Date);
		balances[first.Id].Total.ShouldBe(25m);
		balances[second.Id].Total.ShouldBe(0m);
		balances[excluded.Id].Total.ShouldBe(0m);
		balances.Values.Sum(x => x.Total).ShouldBe(25m);
	}
}
