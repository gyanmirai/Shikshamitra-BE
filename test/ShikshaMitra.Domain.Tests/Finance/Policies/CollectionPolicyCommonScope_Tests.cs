using System;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Policies;

public class CollectionPolicyCommonScope_Tests
{
	[Fact]
	public void New_Writes_Apply_One_Rule_To_All_Heads_And_Ignore_Legacy_Overrides()
	{
		var excluded = Guid.NewGuid();
		var model = new CollectionPolicyUpdateModelBuilder().Build() with
		{
			LateFeeApplyOnFeeHeads = Array.Empty<Guid>(),
			FeeHeadOverrides = new[]
			{
				new CollectionPolicyFeeHeadOverride(excluded, FeeHeadOverrideMode.NoLateFee, null),
				new CollectionPolicyFeeHeadOverride(Guid.NewGuid(), FeeHeadOverrideMode.CustomRule, null)
			}
		};
		var policy = new CollectionPolicy(Guid.NewGuid(), null, Guid.NewGuid(), model);
		Assert.True(policy.AppliesLateFeeTo(excluded));
		Assert.True(policy.AppliesLateFeeTo(Guid.NewGuid()));
		Assert.Empty(policy.GetLateFeeApplyOnFeeHeads());
		Assert.Empty(policy.GetWaiverApplyOnFeeHeads());
		Assert.Empty(policy.GetFeeHeadOverrides());
		Assert.False(policy.TryGetFeeHeadOverride(excluded, out var ignored));
		Assert.Null(ignored);
		Assert.True(policy.ApplyLateFeeToAllFeeHeads);
		Assert.True(policy.AppliesWaiverTo(excluded));
	}

	[Fact]
	public void Snapshot_Roundtrip_Preserves_Common_Marker_And_Legacy_Json_Defaults_To_Selected_Scope()
	{
		var policy = new CollectionPolicy(Guid.NewGuid(), null, Guid.NewGuid(), new CollectionPolicyUpdateModelBuilder().Build());
		var snapshot = CollectionPolicySnapshot.Deserialize(policy.ToSnapshot().Serialize());
		var restored = CollectionPolicy.FromSnapshot(policy.Id, null, policy.AcademicYearId, 5, snapshot);
		Assert.True(restored.ApplyLateFeeToAllFeeHeads);
		Assert.True(restored.AppliesLateFeeTo(Guid.NewGuid()));
		Assert.Equal(5, restored.Version);

		var selected = Guid.NewGuid();
		snapshot.LateFeeApplyOnFeeHeads = new() { selected };
		var legacy = CollectionPolicySnapshot.Deserialize(snapshot.Serialize().Replace("\"ApplyLateFeeToAllFeeHeads\":true,", ""));
		restored = CollectionPolicy.FromSnapshot(policy.Id, null, policy.AcademicYearId, 4, legacy);
		Assert.False(restored.ApplyLateFeeToAllFeeHeads);
		Assert.True(restored.AppliesLateFeeTo(selected));
		Assert.False(restored.AppliesLateFeeTo(Guid.NewGuid()));
		Assert.False(restored.AppliesWaiverTo(Guid.NewGuid()));
		restored.Update(legacy.ToUpdateModel());
		Assert.True(restored.ApplyLateFeeToAllFeeHeads);
		Assert.Empty(restored.GetLateFeeApplyOnFeeHeads());
	}

	[Fact]
	public void Disabled_Common_Policy_Does_Not_Apply_Late_Fee_Or_Waiver()
	{
		var model = new CollectionPolicyUpdateModelBuilder().Build() with { EnableLateFee = false, EnableWaiver = false };
		var policy = new CollectionPolicy(Guid.NewGuid(), null, Guid.NewGuid(), model);
		Assert.False(policy.AppliesLateFeeTo(Guid.NewGuid()));
		Assert.False(policy.AppliesWaiverTo(Guid.NewGuid()));
	}
}
