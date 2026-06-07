namespace ShikshaMitra.HR;

public static partial class HRErrorCodes
{
	public const string EventTypeNotFound = "HR:Calendar:040001";
	public const string EventMeetingDateRequired = "HR:Calendar:040002";
	public const string EventMeetingDateNotAllowed = "HR:Calendar:040003";
	public const string HolidayTypeNotFound = "HR:Calendar:040004";
	public const string CalendarItemNotFound = "HR:Calendar:040005";
	public const string EventDateRangeRequired = "HR:Calendar:040006";
	public const string EventDateRangeInvalid = "HR:Calendar:040007";
	public const string ReportExportFailed = "HR:Reports:050001";
	public const string ReportUserRequired = "HR:Reports:050003";
	public const string ReportExportNotReady = "HR:Reports:050004";
	public const string ReportAccessDenied = "HR:Reports:050005";
	public const string ReportTemplateFieldRequired = "HR:Reports:050006";
	public const string ProfessionalReviewCycleInvalidPeriod = "HR:ProfessionalManagement:060001";
	public const string ProfessionalReviewAlreadySubmitted = "HR:ProfessionalManagement:060002";
	public const string PerformanceTemplateWeightsInvalid = "HR:ProfessionalManagement:060003";
	public const string PerformanceTemplatePublished = "HR:ProfessionalManagement:060004";
	public const string PerformanceReviewStageLocked = "HR:ProfessionalManagement:060005";
	public const string PerformanceReviewFinalized = "HR:ProfessionalManagement:060006";
	public const string PerformanceEvidenceRequired = "HR:ProfessionalManagement:060007";
	public const string PerformanceObservationTeacherOnly = "HR:ProfessionalManagement:060008";
	public const string PerformanceTemplateRubricInvalid = "HR:ProfessionalManagement:060009";
	public const string PerformanceReviewCycleInvalidStatus = "HR:ProfessionalManagement:060010";
	public const string PerformanceReviewCriterionInvalid = "HR:ProfessionalManagement:060011";
	public const string DashboardScopeChanged = "HR:Dashboard:ScopeChanged";
	public const string DashboardInvalidKey = "HR:Dashboard:InvalidKey";
}
