using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using ShikshaMitra.Finance.Common;
using ShikshaMitra.Finance.Fees.Collections.Dtos;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class FeeCollectionSummaryQueryService
{
    private static readonly IReadOnlyDictionary<string, FinanceTableColumn> TableColumns = new Dictionary<string, FinanceTableColumn>
    {
        ["name"] = new(FinanceTableValueType.Text),
        ["standardDivision"] = new(FinanceTableValueType.Text),
        ["rollNumber"] = new(FinanceTableValueType.Text),
        ["totalFees"] = new(FinanceTableValueType.Number),
        ["paidFees"] = new(FinanceTableValueType.Number),
        ["pendingFees"] = new(FinanceTableValueType.Number),
        ["overdueFees"] = new(FinanceTableValueType.Number),
        ["totalConcessionAmount"] = new(FinanceTableValueType.Number),
        ["totalCharges"] = new(FinanceTableValueType.Number),
        ["pendingCharges"] = new(FinanceTableValueType.Number),
        ["status"] = new(FinanceTableValueType.Enum, false, [0, 1, 2, 7])
    };

    private static readonly IReadOnlyDictionary<string, LambdaExpression> TableFields = new Dictionary<string, LambdaExpression>
    {
        ["name"] = (Expression<Func<FeeCollectionSummaryItemDto, string>>)(r => r.StudentName + (r.GrNumber == "" ? "" : " (" + r.GrNumber + ")")),
        ["standardDivision"] = (Expression<Func<FeeCollectionSummaryItemDto, string>>)(r => r.StandardDivision),
        ["rollNumber"] = (Expression<Func<FeeCollectionSummaryItemDto, string>>)(r => r.RollNumber),
        ["totalFees"] = (Expression<Func<FeeCollectionSummaryItemDto, double>>)(r => r.TotalFees),
        ["paidFees"] = (Expression<Func<FeeCollectionSummaryItemDto, double>>)(r => r.PaidFees),
        ["pendingFees"] = (Expression<Func<FeeCollectionSummaryItemDto, double>>)(r => r.PendingFees),
        ["overdueFees"] = (Expression<Func<FeeCollectionSummaryItemDto, double>>)(r => r.OverdueFees),
        ["totalConcessionAmount"] = (Expression<Func<FeeCollectionSummaryItemDto, double>>)(r => r.TotalConcessionAmount),
        ["totalCharges"] = (Expression<Func<FeeCollectionSummaryItemDto, double>>)(r => r.TotalCharges),
        ["pendingCharges"] = (Expression<Func<FeeCollectionSummaryItemDto, double>>)(r => r.PendingCharges),
        ["status"] = (Expression<Func<FeeCollectionSummaryItemDto, int>>)(r => (int)r.Status)
    };
}
