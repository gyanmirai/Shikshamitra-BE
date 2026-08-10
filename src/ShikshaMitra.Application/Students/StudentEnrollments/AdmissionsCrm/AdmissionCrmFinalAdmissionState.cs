namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

internal static class AdmissionCrmFinalAdmissionState
{
	public static readonly AdmissionCrmSeatOfferStatus[] OpenOfferStatuses =
	[
		AdmissionCrmSeatOfferStatus.Draft,
		AdmissionCrmSeatOfferStatus.ApprovalPending,
		AdmissionCrmSeatOfferStatus.Approved,
		AdmissionCrmSeatOfferStatus.Issued,
		AdmissionCrmSeatOfferStatus.Opened,
		AdmissionCrmSeatOfferStatus.Accepted
	];
}
