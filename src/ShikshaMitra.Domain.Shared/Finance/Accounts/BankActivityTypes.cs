namespace ShikshaMitra.Finance.Accounts.BankActivity;
public enum BankActivityRecognitionStatus { Recorded, Pending, Reversed, Unverified }
public enum BankActivityConfirmationStatus { Unavailable, CreditConfirmed, InTransit }
public static class BankActivityErrorCodes
{
	public const string InvalidQuery = "Finance:BankActivity:InvalidQuery";
	public const string ExportLimit = "Finance:BankActivity:ExportLimit";
	public const string UnsupportedProvider = "Finance:BankActivity:UnsupportedProvider";
}
