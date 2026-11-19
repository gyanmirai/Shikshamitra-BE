using System;
using System.Collections.Generic;
using ShikshaMitra.Finance.Common;
using Volo.Abp.Validation;

namespace ShikshaMitra.Finance.Fees.Concessions;

public static class ConcessionApplicationsTable
{
    public static readonly IReadOnlyDictionary<string, FinanceTableColumn> Columns =
        new Dictionary<string, FinanceTableColumn>
        {
            ["studentIdentity"] = new(FinanceTableValueType.Text),
            ["rollNo"] = new(FinanceTableValueType.Text),
            ["classDivision"] = new(FinanceTableValueType.Text),
            ["concessionTypeName"] = new(FinanceTableValueType.Text),
            ["concessionAmount"] = new(FinanceTableValueType.Number),
            ["createdAt"] = new(FinanceTableValueType.Date),
            ["status"] = new(FinanceTableValueType.Enum, false, [0, 1, 2, 3, 4, 5, 6])
        };

    public static ConcessionRequestStatus[] Statuses(string? tab) => tab switch
    {
        null or "" => [],
        "pending" => [ConcessionRequestStatus.Draft, ConcessionRequestStatus.Pending],
        "approved" => [ConcessionRequestStatus.Approved, ConcessionRequestStatus.PartiallyReversed, ConcessionRequestStatus.Reversed],
        "rejected" => [ConcessionRequestStatus.Rejected, ConcessionRequestStatus.Cancelled],
        _ => throw new AbpValidationException("Choose a supported concession tab.")
    };
}
