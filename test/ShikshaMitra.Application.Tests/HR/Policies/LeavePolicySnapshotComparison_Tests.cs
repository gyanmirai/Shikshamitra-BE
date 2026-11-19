using System.Linq;
using ShikshaMitra.HR.Policies;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace ShikshaMitra.HR.Policies;

public class LeavePolicySnapshotComparison_Tests
{
	[Fact]
	public void Rule_Order_And_Generated_Identities_Do_Not_Create_Configuration_Differences()
	{
		var differences = HrPolicyVersionDiffBuilder.BuildDifferences(
			"""{"rules":[{"id":"old","paidType":"Paid","leaveCount":12},{"paidType":"Unpaid","leaveCount":null}]}""",
			"""{"rules":[{"paidType":"Unpaid","leaveCount":null},{"id":"new","paidType":"Paid","leaveCount":12}]}""",
			new HrPolicyVersionDisplayContext());
		differences.ShouldBeEmpty();
	}

	[Fact]
	public void Rule_Differences_Show_The_Bucket_And_Each_Operational_Field()
	{
		var differences = HrPolicyVersionDiffBuilder.BuildDifferences(
			"""{"rules":[{"paidType":"Paid","leaveCount":12,"payrollCategory":"PAID","requiresApproval":true}]}""",
			"""{"rules":[{"paidType":"Paid","leaveCount":15,"payrollCategory":"CL","requiresApproval":false}]}""",
			new HrPolicyVersionDisplayContext());
		differences.Count.ShouldBe(3);
		differences.ShouldAllBe(x => x.Category == "Leave rules" && x.FieldLabel.StartsWith("Paid:"));
		var allocation = differences.Single(x => x.FieldKey == "rules.Paid.leaveCount");
		allocation.FromDisplayValue.ShouldBe("12");
		allocation.ToDisplayValue.ShouldBe("15");
	}

	[Theory]
	[InlineData("[]")]
	[InlineData("{\"rules\":[{\"paidType\":\"Other\"}]}")]
	[InlineData("{\"rules\":[{\"paidType\":\"Paid\",\"leaveCount\":1},{\"paidType\":\"Paid\",\"leaveCount\":2}]}")]
	public void Invalid_Leave_Snapshots_Fail_Closed(string json)
	{
		Should.Throw<BusinessException>(() => HrPolicyVersionDiffBuilder.BuildDifferences(json, "{}",
			new HrPolicyVersionDisplayContext())).Code.ShouldBe(HRErrorCodes.HrPolicyInvalidVersionSnapshot);
	}
}
