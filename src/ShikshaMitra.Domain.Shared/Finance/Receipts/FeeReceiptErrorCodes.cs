namespace ShikshaMitra.Finance.Receipts;

/// <summary>Business exception codes for the receipt domain.</summary>
public static class FeeReceiptErrorCodes
{
    private const string Prefix = "ShikshaMitra:FeeReceipt:";

    public const string ReceiptNotFound             = Prefix + "001";
    public const string PdfGenerationFailed         = Prefix + "002";
    public const string InvalidAcademicContext      = Prefix + "003";
    public const string BoardMediumRequired         = Prefix + "004";
    public const string AlreadyFinalized            = Prefix + "005";
    public const string CannotFinalizeNonProvisional = Prefix + "006";
}
