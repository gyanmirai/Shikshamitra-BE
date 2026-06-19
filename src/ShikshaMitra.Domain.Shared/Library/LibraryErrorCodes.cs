namespace ShikshaMitra.Library;

/// <summary>
/// Centralized error-code catalog for Library module BusinessExceptions.
/// </summary>
public static class LibraryErrorCodes
{
	public const string AcademicYearRequired = "Library:Core:010001";
	public const string BookAlreadyExists = "Library:Books:010002";
	public const string BookNotFound = "Library:Books:010003";
	public const string BookCategoryNotFound = "Library:Books:010004";
	public const string BookCategoryAlreadyExists = "Library:Books:010005";
	public const string BookCategoryInUse = "Library:Books:010006";
	public const string BookHasActiveIssues = "Library:Books:010007";
	public const string AccessionNumberAlreadyExists = "Library:Books:010008";
	public const string DuplicateAccessionNumber = "Library:Books:010009";
	public const string CopyNotFound = "Library:Books:010010";
	public const string CopyNotAvailable = "Library:Books:010011";
	public const string InvalidCopyStatusTransition = "Library:Books:010012";
	public const string SectionNotFound = "Library:Sections:010013";
	public const string SectionAlreadyExists = "Library:Sections:010014";
	public const string SectionInUse = "Library:Sections:010015";
	public const string ShelfNotFound = "Library:Sections:010016";
	public const string InvalidShelfConfiguration = "Library:Sections:010017";
	public const string MemberNotFound = "Library:Members:010018";
	public const string MemberInactive = "Library:Members:010019";
	public const string BorrowingLimitExceeded = "Library:Circulation:010020";
	public const string ReservationLimitExceeded = "Library:Circulation:010021";
	public const string DuplicateActiveIssue = "Library:Circulation:010022";
	public const string ActiveIssueNotFound = "Library:Circulation:010023";
	public const string InvalidIssueDateRange = "Library:Circulation:010024";
	public const string InvalidReturnDate = "Library:Circulation:010025";
	public const string DuplicateReservation = "Library:Circulation:010026";
	public const string ReservationNotFound = "Library:Circulation:010027";
	public const string ChargeCategoryNotFound = "Library:Charges:010028";
	public const string ChargeCategoryAlreadyExists = "Library:Charges:010029";
	public const string ChargeCategoryInUse = "Library:Charges:010030";
	public const string LibraryChargeNotFound = "Library:Charges:010031";
	public const string InvalidCharge = "Library:Charges:010032";
	public const string BorrowingRuleInvalid = "Library:Settings:010033";
	public const string LibraryHoursInvalid = "Library:Settings:010034";
	public const string ReportTypeNotSupported = "Library:Reports:010035";
	public const string ReportExportNotReady = "Library:Reports:010036";
	public const string ReportExportExpired = "Library:Reports:010037";
	public const string ReportExportFileNotFound = "Library:Reports:010038";
	public const string ReportUserRequired = "Library:Reports:010039";
	public const string NotificationEventNotSupported = "Library:Notifications:010040";
	public const string NotificationChannelDisabled = "Library:Notifications:010041";
	public const string NotificationRecipientRequired = "Library:Notifications:010042";
}
