using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Domain.Finance.Policies;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.Finance.Fees.FeeRefund.Dtos;
using Volo.Abp;
using Volo.Abp.Users;

namespace ShikshaMitra.Finance.Fees.FeeRefund;

public partial class FeeRefundAppService
{
    private async Task CompleteCashOnApprovalAsync(FeeRefundRequest request, FeeRefundActionDto? input = null)
    {
        if (request.WorkflowVersion != 1 || request.RefundMethod != "Cash") return;
        await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_Refund.Process);
        if (request.RefundPaymentDetails is not CashRefundDetails cash || string.IsNullOrWhiteSpace(cash.ReceivedBy))
            throw new BusinessException(FeeRefundErrorCodes.PaymentNotSettled);
        var policy = string.IsNullOrWhiteSpace(request.PolicySnapshotJson) ? null
            : JsonSerializer.Deserialize<RefundPolicySnapshot>(request.PolicySnapshotJson);
        var category = await routingManager.RequireCategoryAsync(request.AcademicYearId,
            input?.ExpenseCategoryId ?? policy?.ExpenseCategoryId, TransactionType.Expense);
        var date = new DateTimeOffset(Clock.Now).ToUniversalTime();
        var bank = await routingManager.RequireBankAsync(request.AcademicYearId,
            input?.BankAccountId ?? policy?.DefaultPayingBankAccountId, date);
        request.SetPayoutRouting(category.Id, category.Name, bank.Id,
            $"{bank.BankName} • {bank.AccountNumber[^Math.Min(4, bank.AccountNumber.Length)..]}", "Cash");
        var key = $"cash-approval:{request.Id:N}";
        var evidence = $"Cash payout authorized for {cash.ReceivedBy}.";
        var payout = request.RecordPayout(GuidGenerator.Create(), currentUser.GetId(), key,
            "Cash", request.ApprovedAmount, date, $"CASH-{request.VoucherNumber}", null, bank.Id, evidence);
        await feeRefundRepository.StageNewSettlementAsync(payout);
        await CompleteRecordedPayoutAsync(request, payout, key, evidence);
    }
}
