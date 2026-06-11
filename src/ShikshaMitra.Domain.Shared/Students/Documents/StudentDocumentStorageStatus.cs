namespace ShikshaMitra.Students.Documents;

public enum StudentDocumentStorageStatus
{
	PendingValidation = 0,
	Stored = 1,
	Rejected = 2,
	Deleted = 3
}

public enum StudentDocumentAuditAction
{
	Upload = 0,
	Replace = 1,
	Download = 2,
	Delete = 3,
	ValidationFailed = 4
}
