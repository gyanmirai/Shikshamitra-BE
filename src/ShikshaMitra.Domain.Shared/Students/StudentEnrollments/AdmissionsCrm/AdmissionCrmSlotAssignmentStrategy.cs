namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public enum AdmissionCrmSlotAssignmentStrategy
{
	Manual = 0,
	EarliestAvailable = 1,
	RoundRobin = 2,
	ClassMatch = 3,
	CampusMatch = 4,
	PreferredDate = 5,
	LanguageMatch = 6,
	SiblingGrouping = 7,
	BalancedCapacity = 8
}
