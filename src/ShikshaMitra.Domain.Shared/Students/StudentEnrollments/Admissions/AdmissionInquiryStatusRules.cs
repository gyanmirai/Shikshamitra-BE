namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Preserves persisted lifecycle values while exposing three inquiry business statuses.</summary>
public static class AdmissionInquiryStatusRules
{
	public static AdmissionInquiryStatus Display(AdmissionInquiryStatus status) => status switch
	{
		AdmissionInquiryStatus.Interested or AdmissionInquiryStatus.ApplicationStarted or AdmissionInquiryStatus.Converted => AdmissionInquiryStatus.Interested,
		AdmissionInquiryStatus.NotInterested or AdmissionInquiryStatus.Lost or AdmissionInquiryStatus.Duplicate => AdmissionInquiryStatus.NotInterested,
		_ => AdmissionInquiryStatus.New
	};

	public static bool IsTransferred(AdmissionInquiryStatus status) =>
		status is AdmissionInquiryStatus.ApplicationStarted or AdmissionInquiryStatus.Converted;

	public static bool IsBusinessStatus(AdmissionInquiryStatus status) =>
		status is AdmissionInquiryStatus.New or AdmissionInquiryStatus.Interested or AdmissionInquiryStatus.NotInterested;
}
