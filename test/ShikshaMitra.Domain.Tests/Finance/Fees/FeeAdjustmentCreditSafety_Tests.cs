using ShikshaMitra.Constants.Permissions;
using Xunit;

namespace ShikshaMitra.Domain.Finance.Fees;

public class FeeAdjustmentCreditSafety_Tests
{
    [Fact]
    public void Decision_grants_are_separate_from_creation_grant()
    {
        Assert.NotEqual(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add,
            ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Approve);
        Assert.NotEqual(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Add,
            ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Reject);
        Assert.Contains(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Approve,
            ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.All);
        Assert.Contains(ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.Reject,
            ShikshaMitraSubmodulePermissions.Finance.Fees_Adjustment.All);
    }

}
