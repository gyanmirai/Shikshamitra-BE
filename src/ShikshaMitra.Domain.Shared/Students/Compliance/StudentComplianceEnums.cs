namespace ShikshaMitra.Students.Compliance;

public enum IdentityVerificationStatus
{
	NotProvided = 0,
	Pending = 1,
	Verified = 2,
	Rejected = 3,
	Mismatch = 4,
	Expired = 5
}

public enum ConsentStatus
{
	NotAsked = 0,
	Requested = 1,
	Granted = 2,
	Refused = 3,
	Revoked = 4
}

public enum UdiseSyncStatus
{
	NotReady = 0,
	Ready = 1,
	Exported = 2,
	Rejected = 3,
	NeedsCorrection = 4
}

public enum ScholarshipStatus
{
	NotApplicable = 0,
	Eligible = 1,
	Applied = 2,
	Approved = 3,
	Rejected = 4,
	Disbursed = 5
}
