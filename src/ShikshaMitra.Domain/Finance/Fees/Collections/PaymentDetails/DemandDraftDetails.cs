using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>
/// Payment details for a Demand Draft payment.
/// </summary>
public class DemandDraftDetails : PaymentDetails
{
    public string DDNumber { get; private set; }
    public string IssuingBankName { get; private set; }
    public string BranchName { get; private set; }
    public DateOnly? DDDate { get; private set; }
    public DateOnly? PaymentDate { get; private set; }

    private DemandDraftDetails() { /* For ORM */ }

    public DemandDraftDetails(string ddNumber, string issuingBankName, string branchName, DateOnly? ddDate,
        DateOnly? paymentDate = null)
    {
        DDNumber = ddNumber;
        IssuingBankName = issuingBankName;
        BranchName = branchName;
        DDDate = ddDate;
        PaymentDate = paymentDate;
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return DDNumber;
        yield return IssuingBankName;
        yield return BranchName;
        yield return DDDate!;
        yield return PaymentDate!;
    }
}
