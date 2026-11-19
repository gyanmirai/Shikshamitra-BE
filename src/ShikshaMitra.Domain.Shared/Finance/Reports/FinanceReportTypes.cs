namespace ShikshaMitra.Finance.Reports;

public static class FinanceReportTypes
{
    public const string FeeDemandSummary = "Fee demand and collection summary";
    public const string OutstandingFees = "Outstanding fees and ageing";
    public const string StudentStatement = "Student fee statement";
    public const string DailyCollections = "Daily collection register";
    public const string ReceiptRegister = "Receipt register and corrections";
    public const string PaymentVerification = "Payment verification and clearing ageing";

    public static readonly string[] PhaseOne =
        [FeeDemandSummary, OutstandingFees, StudentStatement, DailyCollections, ReceiptRegister, PaymentVerification];

    public const string FeeCollectionBatchAndMonthwise = "Fee Collection Batch And Monthwise";
    public const string FeeCollectionBatchwise = "Fee Collection Batchwise";
    public const string FeeCollectionMonthAndFeeHeadwise = "Fee Collection Month And FeeHeadwise";
    public const string FeeCollectionStudentWithFeewise = "Fee Collection Student With Feewise";
    public const string FeeListChequeClearDateWise = "Fee List Cheque Clear Date Wise";
    public const string FeeMonthlyRegister = "Fee Monthly Register";
    public const string BankDepositReport = "Bank Deposit Report";
    public const string ClasswiseFeeSetup = "Classwise Fee Setup";
    public const string CustomGroupReceiptList = "Custom Group Receipt List";
    public const string FeeRegisterAllStudent = "Fee Register All Student";
}
