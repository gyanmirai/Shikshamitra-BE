using System;
using System.Collections.Generic;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

/// <summary>
/// Payment details for a Cheque payment.
/// </summary>
public class ChequeDetails : PaymentDetails
{
    public string ChequeNumber { get; private set; }
    public string BankName { get; private set; }
    public string BranchName { get; private set; }
    public DateOnly ChequeDate { get; private set; }

    public ChequeDetails(string chequeNumber, string bankName, string branchName, DateOnly chequeDate)
    {
        ChequeNumber = chequeNumber;
        BankName = bankName;
        BranchName = branchName;
        ChequeDate = chequeDate;
    }

    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return ChequeNumber;
        yield return BankName;
        yield return BranchName;
        yield return ChequeDate;
    }
}
