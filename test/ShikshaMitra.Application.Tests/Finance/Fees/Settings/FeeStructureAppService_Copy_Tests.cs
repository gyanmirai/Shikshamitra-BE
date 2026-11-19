using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Domain.Finance.Fees.Settings;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures.Dtos;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Xunit;

namespace ShikshaMitra.Finance.Fees.Settings;

public partial class FeeStructureAppService_CollectionPolicy_Tests
{
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Copy_Preflight_Selects_Current_Refund_And_Preserves_Source(bool updatePolicy)
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var (metadata, standard, candidate) = await ReadyCandidateAsync();
		var year = (await _metadataRepo.GetAsync(metadata)).AcademicYearId!.Value;
		var refunds = GetRequiredService<IRefundPolicyAppService>();
		var model = new CreateUpdateRefundPolicyDto
		{
			AcademicYearId = year, PolicyName = "School Refund", Status = "Active",
			ExpenseCategoryId = await CreateRefundExpenseCategoryAsync(year), AllowedPayoutMethods = ["Cash"]
		};
		var policy = await refunds.CreateAsync(model);
		candidate.RefundPolicyId = policy.Id;
		var source = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
		if (updatePolicy)
		{
			var latest = await refunds.GetAsync(policy.Id);
			model.ProcessingTargetDays = 10; model.ChangeReason = "New refund processing target"; model.ConcurrencyStamp = latest.ConcurrencyStamp; model.IsDefault = latest.IsDefault;
			await refunds.UpdateAsync(policy.Id, model);
		}
		var current = await refunds.GetAsync(policy.Id);
		var preflight = await _feeStructures.GetCopyPoliciesAsync(source.Id, metadata, standard);
		var refund = preflight.Single(x => x.Family == "refund");
		refund.PolicyName.ShouldBe("School Refund");
		refund.SourceVersionId.ShouldBe(source.RefundPolicyVersionId);
		refund.SourceVersionNumber.ShouldBe(policy.Version);
		refund.CurrentVersionId.ShouldBe(current.CurrentVersionId);
		refund.CurrentVersionNumber.ShouldBe(current.Version);
		refund.Error.ShouldBeNull();
		var copy = AsRequest(source); copy.Name = "New class fees"; copy.ConcurrencyStamp = null;
		foreach (var head in copy.FeeHeads) head.Id = null;
		if (updatePolicy)
		{
			var rejectedId = Guid.NewGuid();
			var rejected = await Should.ThrowAsync<BusinessException>(() => _feeStructures.CreateAsync(metadata,
				new() { RequestId = rejectedId, StandardId = standard, StandardFeeStructures = copy }));
			((System.Collections.Generic.List<string>)rejected.Data["Errors"]!).ShouldContain(x =>
				x.Contains("Refund Policy") && x.Contains("School Refund") && x.Contains($"v{policy.Version}") && x.Contains($"v{current.Version}"));
			(await GetRequiredService<IRepository<FeeStructure, Guid>>().FindAsync(rejectedId)).ShouldBeNull();
		}
		copy.RefundPolicyVersionId = refund.CurrentVersionId;
		var requestId = Guid.NewGuid();
		var input = new StandardFeeStructureSaveRequest { RequestId = requestId, StandardId = standard, StandardFeeStructures = copy };
		var created = await _feeStructures.CreateAsync(metadata, input);
		(await _feeStructures.CreateAsync(metadata, input)).Id.ShouldBe(created.Id);
		created.RefundPolicyVersionId.ShouldBe(current.CurrentVersionId);
		var records = (await _feeStructures.GetByStandardAsync(standard)).StandardFeeStructures;
		records.Count.ShouldBe(2);
		records.Single(x => x.Id == source.Id).RefundPolicyVersionId.ShouldBe(source.RefundPolicyVersionId);
		(await refunds.GetAsync(policy.Id)).Version.ShouldBe(current.Version);
	}

	[Fact]
	public async Task Copy_Preflight_Resolves_Legacy_Concession_And_Uses_Master_Version_Instead_Of_Largest_Snapshot()
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var (metadata, standard, candidate) = await ReadyCandidateAsync();
		var source = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
		var policy = await GetRequiredService<IConcessionPolicyAppService>().GetAsync(candidate.ConcessionPolicyId!.Value);
		var versions = GetRequiredService<IRepository<FinancePolicyVersion, Guid>>();
		var snapshot = await versions.GetAsync(policy.CurrentVersionId!.Value);
		await versions.InsertAsync(new FinancePolicyVersion(Guid.NewGuid(), TestTenantId, "concession", policy.Id,
			policy.AcademicYearId, policy.Version + 10, snapshot.PolicyName, "Active", snapshot.SnapshotJson, "Unrelated historical snapshot"), autoSave: true);
		var repository = GetRequiredService<IRepository<FeeStructure, Guid>>();
		var stored = await repository.GetAsync(source.Id);
		stored.AttachConcessionPolicy(policy.Id, policy.CurrentVersionId, ConcessionFeeHeadScope.EntireFeeStructure, []);
		await repository.UpdateAsync(stored, autoSave: true);
		var preflight = await _feeStructures.GetCopyPoliciesAsync(source.Id, metadata, standard);
		var concession = preflight.Single(x => x.Family == "concession");
		concession.CurrentVersionId.ShouldBe(policy.CurrentVersionId);
		concession.CurrentVersionNumber.ShouldBe(policy.Version);
		concession.SourceVersionId.ShouldBe(policy.CurrentVersionId);
		concession.Error.ShouldBeNull();
	}

	[Theory]
	[InlineData("stale")]
	[InlineData("inactive")]
	[InlineData("missing-snapshot")]
	[InlineData("missing-policy")]
	public async Task Invalid_Copy_Attachment_Has_Clear_Error_And_No_Writes(string problem)
	{
		using var reviewer = Reviewer(); using var tenant = _currentTenant.Change(TestTenantId);
		var (metadata, standard, candidate) = await ReadyCandidateAsync();
		var source = await _feeStructures.CreateAsync(metadata, new() { StandardId = standard, StandardFeeStructures = candidate });
		var policies = GetRequiredService<IRepository<CollectionPolicy, Guid>>();
		var policy = await _collectionPolicies.GetAsync(source.CollectionPolicyId!.Value);
		if (problem == "stale")
			await _collectionPolicies.UpdateAsync(policy.Id, new CreateUpdateCollectionPolicyDto
			{
				AcademicYearId = policy.AcademicYearId, PolicyName = policy.PolicyName, PolicyCode = policy.PolicyCode,
				Status = "Active", IsDefault = policy.IsDefault, ChangeReason = "New version for copy race", AllowOnlineCollection = true
			});
		if (problem == "inactive")
		{
			var master = await policies.GetAsync(policy.Id); master.SetDefault(false); master.Archive(); await policies.UpdateAsync(master, autoSave: true);
		}
		if (problem == "missing-snapshot")
		{
			// Simulate a master whose latest immutable snapshot was never written, without deleting referenced history.
			var master = await policies.GetAsync(policy.Id); master.SetDefault(false); await policies.UpdateAsync(master, autoSave: true);
		}
		if (problem == "missing-policy") await policies.DeleteAsync(policy.Id, autoSave: true);
		var currentVersion = (await policies.FindAsync(policy.Id))?.Version ?? policy.Version;
		var preflight = (await _feeStructures.GetCopyPoliciesAsync(source.Id, metadata, standard)).Single();
		if (problem != "stale") { preflight.Error.ShouldNotBeNullOrEmpty(); preflight.CurrentVersionId.ShouldBeNull(); }
		var copy = AsRequest(source); copy.Name = "Blocked copy";
		foreach (var head in copy.FeeHeads) head.Id = null;
		var requestId = Guid.NewGuid();
		var failure = await Should.ThrowAsync<BusinessException>(() => _feeStructures.CreateAsync(metadata,
			new() { RequestId = requestId, StandardId = standard, StandardFeeStructures = copy }));
		var errors = (System.Collections.Generic.List<string>)failure.Data["Errors"]!;
		errors.ShouldContain(x => x.Contains("Collection Policy"));
		if (problem == "stale") errors.ShouldContain(x => x.Contains(policy.PolicyName) && x.Contains($"v{policy.Version}") && x.Contains($"v{currentVersion}"));
		var records = (await _feeStructures.GetByStandardAsync(standard)).StandardFeeStructures;
		records.Count.ShouldBe(1); records.Single().CollectionPolicyVersionId.ShouldBe(source.CollectionPolicyVersionId);
		records.Single().ConcurrencyStamp.ShouldBe(source.ConcurrencyStamp);
		if (problem == "missing-policy") (await policies.FindAsync(policy.Id)).ShouldBeNull();
		else (await policies.GetAsync(policy.Id)).Version.ShouldBe(currentVersion);
		(await GetRequiredService<IRepository<FeeStructure, Guid>>().FindAsync(requestId)).ShouldBeNull();
	}
}
