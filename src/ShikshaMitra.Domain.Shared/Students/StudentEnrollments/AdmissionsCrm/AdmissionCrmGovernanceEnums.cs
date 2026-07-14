namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public enum AdmissionCrmRetentionCategory
{
	UnsuccessfulApplicant = 0, WithdrawnApplicant = 1, DuplicateApplicant = 2,
	ActivatedStudent = 3, FinancialReference = 4, Consent = 5,
	GovernmentAcknowledgement = 6, CustodyEvidence = 7, Audit = 8
}

public enum AdmissionCrmRetentionAction { ReviewOnly = 0, Anonymize = 1, DeleteWherePermitted = 2 }
public enum AdmissionCrmGovernedConfigurationStatus { Draft = 0, ApprovalPending = 1, Published = 2, Retired = 3 }
public enum AdmissionCrmPrivacyRequestType { Anonymization = 0, Deletion = 1, OptionalConsentWithdrawal = 2 }
public enum AdmissionCrmPrivacyRequestStatus { Requested = 0, ApprovalPending = 1, Approved = 2, Rejected = 3, Completed = 4 }
public enum AdmissionCrmExportReportType { Funnel = 0, OperationsQueue = 1, AuditTimeline = 2, GovernmentReadiness = 3, DataQuality = 4 }
public enum AdmissionCrmExportStatus { Ready = 0, Expired = 1, Revoked = 2, Failed = 3 }
public enum AdmissionCrmReportFrequency { Daily = 0, Weekly = 1, Monthly = 2 }
public enum AdmissionCrmDelegationStatus { Scheduled = 0, Active = 1, Expired = 2, Cancelled = 3 }
