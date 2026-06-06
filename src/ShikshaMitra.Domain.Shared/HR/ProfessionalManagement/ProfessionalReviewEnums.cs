namespace ShikshaMitra.HR.ProfessionalManagement;

public enum ProfessionalReviewCycleStatus
{
	Draft = 0,
	Published = 1,
	InProgress = 2,
	Calibration = 3,
	Finalized = 4,
	Archived = 5,
	Active = InProgress,
	Closed = Finalized
}

public enum ProfessionalReviewStatus
{
	Draft = 0,
	Submitted = 1,
	InProgress = 2,
	Finalized = 3,
	Reopened = 4
}

public enum PerformanceCycleType
{
	Annual = 0,
	HalfYearly = 1,
	Quarterly = 2,
	Probation = 3,
	ContractRenewal = 4,
	Promotion = 5,
	IncidentBased = 6
}

public enum PerformanceStaffCategory
{
	Teacher = 0,
	ClassTeacher = 1,
	HodCoordinator = 2,
	PrincipalVicePrincipal = 3,
	Admin = 4,
	Accounts = 5,
	LibrarianLabAssistant = 6,
	Transport = 7,
	SecurityHousekeeping = 8,
	ItSupport = 9
}

public enum PerformanceBoardType
{
	GenericIndian = 0,
	CBSE = 1,
	GSEB = 2
}

public enum PerformanceSqaaDomain
{
	CurriculumPedagogyAssessment = 0,
	Infrastructure = 1,
	HumanResources = 2,
	LeadershipGovernance = 3,
	InclusivenessWellBeing = 4,
	BeneficiarySatisfaction = 5,
	ManagementSchoolProcesses = 6
}

public enum PerformanceTemplateStatus
{
	Draft = 0,
	Published = 1,
	Archived = 2
}

public enum PerformanceReviewStageType
{
	SelfAppraisal = 0,
	ReportingManager = 1,
	HodCoordinator = 2,
	PrincipalManagement = 3,
	HrVerification = 4
}

public enum PerformanceReviewStageStatus
{
	Pending = 0,
	Draft = 1,
	Submitted = 2,
	Reopened = 3
}

public enum PerformanceFeedbackSource
{
	Peer = 0,
	Student = 1,
	Parent = 2,
	InternalStakeholder = 3
}

public enum PerformanceRecommendationType
{
	None = 0,
	Increment = 1,
	Promotion = 2,
	Confirmation = 3,
	ContractRenewal = 4,
	PerformanceImprovementPlan = 5,
	Training = 6,
	Recognition = 7
}

public enum DevelopmentPlanStatus
{
	Open = 0,
	InProgress = 1,
	Completed = 2,
	Deferred = 3
}
