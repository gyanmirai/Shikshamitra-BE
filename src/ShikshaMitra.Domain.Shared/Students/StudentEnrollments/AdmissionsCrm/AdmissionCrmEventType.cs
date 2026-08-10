namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public enum AdmissionCrmEventType
{
	CampusVisit = 0,
	OpenHouse = 1,
	Counselling = 2,
	Orientation = 3,
	Other = 4
}

public enum AdmissionCrmEventStatus
{
	Draft = 0,
	Published = 1,
	Cancelled = 2,
	Completed = 3
}

public enum AdmissionCrmEventRegistrationStatus
{
	Reserved = 0,
	CheckedIn = 1,
	Cancelled = 2,
	NoShow = 3
}
