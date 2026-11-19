using System.Linq;
using ShikshaMitra.Constants.Permissions;
using Shouldly;
using Xunit;

namespace ShikshaMitra.Finance.Accounts;

public class FeeConfigurationPermissionContract_Tests
{
	[Theory]
	[InlineData("Finance.Settings.FeeStructure")]
	[InlineData("Finance.Policies.CollectionPolicy")]
	[InlineData("Finance.Policies.ConcessionPolicy")]
	[InlineData("Finance.Policies.RefundPolicy")]
	[InlineData("Finance.Policies.ProrationPolicy")]
	public void Configuration_Should_Expose_Only_Three_Canonical_Actions(string root)
	{
		var configuration = ShikshaMitraSubmodulePermissions.Finance.Submodules[root];
		configuration.Permissions.ShouldBe(new[] { $"{root}.View", $"{root}.Add", $"{root}.Delete" });
		foreach (var permission in configuration.Permissions)
			configuration.DisplayNames.ShouldContainKey(permission);
	}

	[Fact]
	public void Configuration_Changes_Should_Preserve_Independent_Transaction_Grants()
	{
		var definitions = ShikshaMitraSubmodulePermissions.Finance.Submodules.Values.SelectMany(x => x.Permissions).ToArray();
		definitions.Distinct().Count().ShouldBe(definitions.Length);
		definitions.ShouldContain("Finance.Fees.FeeCollections.ApplyWaiver");
		definitions.ShouldNotContain("Finance.Policies.CollectionPolicy.ViewWaivers");
		definitions.ShouldNotContain("Finance.Policies.CollectionPolicy.RequestWaiver");
		definitions.ShouldNotContain("Finance.Policies.CollectionPolicy.ApproveWaiver");
		definitions.ShouldNotContain("Finance.Policies.CollectionPolicy.RejectWaiver");
		definitions.ShouldNotContain("Finance.Policies.CollectionPolicy.ApproveWaiverAboveThreshold");
		definitions.ShouldContain("Finance.Policies.ProrationPolicy.OverrideChargePeriod");
		definitions.ShouldNotContain("Finance.Settings.FeeStructure.Publish");
		definitions.ShouldNotContain("Finance.Policies.CollectionPolicy.ManageAttachment");
	}
}
