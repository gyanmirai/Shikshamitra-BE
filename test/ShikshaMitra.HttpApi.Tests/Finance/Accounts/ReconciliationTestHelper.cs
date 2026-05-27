using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using ShikshaMitra.Finance.FeeCollection;
using ShikshaMitra.Finance.FeeCollectionTests;
using ShikshaMitra.Finance.Fees.Collections.Dtos;

namespace ShikshaMitra.Finance.Accounts.Reconciliation;

/// <summary>
/// Seeds provisional receipt + FeeCollectionDueFeeAllocation rows for reconciliation tests,
/// mirroring the pattern used in <see cref="FeeCollectionTestHelper"/> and EmployeeTestHelper.
/// </summary>
public static class ReconciliationTestHelper
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions =
        new(System.Text.Json.JsonSerializerDefaults.Web);

    private const string FeeCollectionUrl = "/api/v2/fee-collections";
    private const string ReconciliationUrl = "/api/v1/finance/reconciliations";

    /// <summary>
    /// Collects a fee using a non-cash payment method so that a provisional receipt is created
    /// and the transaction lands in the reconciliation inbox.
    /// Returns (provisionalReceiptId, collectionTransactionId, studentId).
    /// </summary>
    public static async Task<(Guid ReceiptId, Guid TransactionId, Guid StudentId)>
        SeedProvisionalReceiptAsync(
            HttpClient client,
            IServiceProvider services,
            Guid academicYearId,
            Guid feeHeadId,
            double amount = 1500d,
            PaymentMethodEnum method = PaymentMethodEnum.Cheque)
    {
        var (studentId, _) = await Finance.ConcessionRequests.ConcessionRequestTestHelper.CreateStudentAsync(client);

        var dueFeeId = await FeeCollectionTestHelper.SeedDueFeeAsync(services, studentId, feeHeadId, amount);

        var input = BuildNonCashCollectFeeInput(studentId, academicYearId, feeHeadId, dueFeeId, amount, method);
        var response = await client.PostAsJsonAsync(FeeCollectionUrl, input, JsonOptions);
        response.EnsureSuccessStatusCode();

        var output = await response.Content.ReadFromJsonAsync<CollectFeeOutput>(JsonOptions)
            ?? throw new InvalidOperationException("Null CollectFeeOutput from response");

        // The FeeReceiptNumber contains the provisional receipt number (KFE-*)
        // but we need the receipt ID — fetch it from the list.
        var listResponse = await client.GetAsync(
            $"{ReconciliationUrl}?academicYearId={academicYearId}" +
            $"&paymentMethod={method}&page=1&pageSize=50");

        Guid receiptId = Guid.Empty;
        if (listResponse.IsSuccessStatusCode)
        {
            var listJson = await listResponse.Content
                .ReadFromJsonAsync<System.Text.Json.JsonElement>(JsonOptions);

            if (listJson.TryGetProperty("items", out var items) &&
                items.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.TryGetProperty("feeCollectionTransactionId", out var txProp) &&
                        Guid.TryParse(txProp.GetString(), out var txId) &&
                        txId == output.Id)
                    {
                        if (item.TryGetProperty("id", out var idProp) &&
                            Guid.TryParse(idProp.GetString(), out receiptId))
                            break;
                    }
                }
            }
        }

        return (receiptId, output.Id, studentId);
    }

    private static CollectFeeInput BuildNonCashCollectFeeInput(
        Guid studentId,
        Guid academicYearId,
        Guid feeHeadId,
        Guid dueFeeId,
        double amount,
        PaymentMethodEnum method)
    {
        var payment = new FeePaymentDto
        {
            PaymentMethod = method,
            Amount = amount,
        };

        // Populate instrument details required by validators
        payment.Details = method switch
        {
            PaymentMethodEnum.Cheque => new System.Collections.Generic.Dictionary<string, object?>
            {
                { "chequeNumber", "123456" },
                { "bankName", "Test Bank" },
                { "chequeDate", DateTime.UtcNow.ToString("O") },
            },
            PaymentMethodEnum.DemandDraft => new System.Collections.Generic.Dictionary<string, object?>
            {
                { "ddNumber", "DD001" },
                { "bankName", "Test Bank" },
                { "ddDate", DateTime.UtcNow.ToString("O") },
            },
            PaymentMethodEnum.UPI => new System.Collections.Generic.Dictionary<string, object?>
            {
                { "transactionId", "UPI123" },
                { "upiId", "test@upi" },
            },
            PaymentMethodEnum.BankTransfer => new System.Collections.Generic.Dictionary<string, object?>
            {
                { "transactionId", "NEFT123" },
                { "bankName", "Test Bank" },
            },
            PaymentMethodEnum.CreditCard => new System.Collections.Generic.Dictionary<string, object?>
            {
                { "transactionId", "CC123" },
                { "cardLast4", "1234" },
                { "bankName", "Test Bank" },
            },
            PaymentMethodEnum.OnlinePaymentGateway => new System.Collections.Generic.Dictionary<string, object?>
            {
                { "transactionId", "OG123" },
                { "gatewayName", "Razorpay" },
            },
            PaymentMethodEnum.WalletPrepaidCard => new System.Collections.Generic.Dictionary<string, object?>
            {
                { "transactionId", "WL123" },
                { "walletProvider", "Paytm" },
            },
            _ => null,
        };

        return new CollectFeeInput
        {
            StudentId = studentId,
            AcademicYearId = academicYearId,
            Notes = "Reconciliation test payment",
            // Cheque/DD are AlwaysProvisional — BankReceiptConfirmed must be null.
            // CashierDecided methods (UPI/Card/etc.) require BankReceiptConfirmed = false
            // to generate a provisional receipt and enter the reconciliation queue.
            PaymentConfirmed = method is
                PaymentMethodEnum.UPI or
                PaymentMethodEnum.CreditCard or
                PaymentMethodEnum.DebitCard or
                PaymentMethodEnum.BankTransfer or
                PaymentMethodEnum.OnlinePaymentGateway or
                PaymentMethodEnum.WalletPrepaidCard
                ? false
                : null,
            DueFeePayments = new System.Collections.Generic.List<DueFeePaymentDto>
            {
                new() { DueFeeId = dueFeeId, FeeHeadId = feeHeadId, Amount = amount },
            },
            Payments = new System.Collections.Generic.List<FeePaymentDto> { payment },
        };
    }
}
