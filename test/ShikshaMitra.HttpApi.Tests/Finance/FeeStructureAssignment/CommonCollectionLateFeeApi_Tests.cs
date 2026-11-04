using System.Threading.Tasks;
using ShikshaMitra.Infrastructure.Traits;
using Xunit;

namespace ShikshaMitra.Finance.FeeStructureAssignment;

[Collection(FinanceReceiptApiTestCollection.Name)]
[TestModule(TestTraits.Modules.Finance)]
public sealed class CommonCollectionLateFeeApi_Tests(FinanceReceiptApiTestFactory factory)
{
	[Fact]
	[TestTier(TestTraits.Tiers.Critical)]
	public async Task Common_Late_Fee_Persists_Across_Heads_Months_Partial_Payments_And_Repeated_Reads()
	{
		await new StudentFeeAssignmentApi_Tests(factory).VerifyCommonLateFeeBrowserScenarioAsync();
	}
}
