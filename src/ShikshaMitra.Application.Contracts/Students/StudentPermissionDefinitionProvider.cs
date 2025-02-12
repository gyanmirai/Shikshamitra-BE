using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using static ShikshaMitra.Localization.LocalizationHelper;

namespace ShikshaMitra.Students;

public class StudentPermissionDefinitionProvider : PermissionDefinitionProvider
{
	public override void Define(IPermissionDefinitionContext context)
	{
		// Create a new group for all student permissions
		var studentGroup = context.AddGroup(
			StudentPermissions.GroupName,
			L("Permission:Student")
		);

		// Base enrollment permission
		var enrollmentPermission = studentGroup.AddPermission(
			StudentPermissions.Enrollment.Default,
			L("Permission:StudentEnrollment")
		);

		// Create granular permissions for all enrollment features
		CreateGranularPermission(
			enrollmentPermission,
			StudentPermissions.Enrollment.Dashboard,
			"Dashboard");

		CreateGranularPermission(
			enrollmentPermission,
			StudentPermissions.Enrollment.Inquiry,
			"Inquiry");

		CreateGranularPermission(
			enrollmentPermission,
			StudentPermissions.Enrollment.RegistrationForm,
			"RegistrationForm");

		CreateGranularPermission(
			enrollmentPermission,
			StudentPermissions.Enrollment.Registration,
			"Registration");

		CreateGranularPermission(
			enrollmentPermission,
			StudentPermissions.Enrollment.EntranceExam,
			"EntranceExam");

		CreateGranularPermission(
			enrollmentPermission,
			StudentPermissions.Enrollment.FollowUp,
			"FollowUp");

		CreateGranularPermission(
			enrollmentPermission,
			StudentPermissions.Enrollment.Admission,
			"Admission");

		CreateGranularPermission(
			enrollmentPermission,
			StudentPermissions.Enrollment.AdmissionFees,
			"AdmissionFees");

		CreateGranularPermission(
			enrollmentPermission,
			StudentPermissions.Enrollment.Reports,
			"Reports");

		CreateGranularPermission(
			enrollmentPermission,
			StudentPermissions.Enrollment.Setting,
			"Setting");
	}

	/// <summary>
	/// Creates a permission with granular control (Create, Edit, Delete) as children.
	/// </summary>
	/// <param name="parentPermission">The parent permission under which to add the granular permission.</param>
	/// <param name="permissionName">The name of the permission (e.g. StudentPermissions.Enrollment.Inquiry).</param>
	/// <param name="displayNameSuffix">The display name suffix for the permission (e.g. Inquiry).</param>
	private static void CreateGranularPermission(PermissionDefinition parentPermission, string permissionName, string displayNameSuffix)
	{
		// Add the base permission for this feature.
		var basePermission = parentPermission.AddChild(
			permissionName,
			L($"Permission:StudentEnrollment.{displayNameSuffix}")
		);

		// Add granular permissions (Create, Edit, Delete) for this feature.
		basePermission.AddChild(
			$"{permissionName}.Create",
			L($"Permission:StudentEnrollment.{displayNameSuffix}.Create")
		);
		basePermission.AddChild(
			$"{permissionName}.Edit",
			L($"Permission:StudentEnrollment.{displayNameSuffix}.Edit")
		);
		basePermission.AddChild(
			$"{permissionName}.Delete",
			L($"Permission:StudentEnrollment.{displayNameSuffix}.Delete")
		);
	}
}


//var studentGroup = context.AddGroup(StudentPermissions.GroupName, L("Permission:Student"));

//// Define permissions for IndividualStudentProfile
//var individualStudentProfilePermission = studentGroup.AddPermission(
//	StudentPermissions.IndividualStudentProfile.Default, L("Permission:Student.IndividualStudentProfile"));
//individualStudentProfilePermission.AddChild(StudentPermissions.IndividualStudentProfile.Create,
//	L("Permission:Student.IndividualStudentProfile.Create"));
//individualStudentProfilePermission.AddChild(StudentPermissions.IndividualStudentProfile.Edit,
//	L("Permission:Student.IndividualStudentProfile.Edit"));
//individualStudentProfilePermission.AddChild(StudentPermissions.IndividualStudentProfile.Delete,
//	L("Permission:Student.IndividualStudentProfile.Delete"));

//// Define permissions for AttendanceTracker
//var attendanceTrackerPermission = studentGroup.AddPermission(StudentPermissions.AttendanceTracker.Default,
//	L("Permission:Student.AttendanceTracker"));
//attendanceTrackerPermission.AddChild(StudentPermissions.AttendanceTracker.Create,
//	L("Permission:Student.AttendanceTracker.Create"));
//attendanceTrackerPermission.AddChild(StudentPermissions.AttendanceTracker.Edit,
//	L("Permission:Student.AttendanceTracker.Edit"));
//attendanceTrackerPermission.AddChild(StudentPermissions.AttendanceTracker.Delete,
//	L("Permission:Student.AttendanceTracker.Delete"));

//// Define permissions for AcademicPerformance
//var academicPerformancePermission = studentGroup.AddPermission(StudentPermissions.AcademicPerformance.Default,
//	L("Permission:Student.AcademicPerformance"));
//academicPerformancePermission.AddChild(StudentPermissions.AcademicPerformance.Create,
//	L("Permission:Student.AcademicPerformance.Create"));
//academicPerformancePermission.AddChild(StudentPermissions.AcademicPerformance.Edit,
//	L("Permission:Student.AcademicPerformance.Edit"));
//academicPerformancePermission.AddChild(StudentPermissions.AcademicPerformance.Delete,
//	L("Permission:Student.AcademicPerformance.Delete"));

//// Define permissions for BehavioralRecords
//var behavioralRecordsPermission = studentGroup.AddPermission(StudentPermissions.BehavioralRecords.Default,
//	L("Permission:Student.BehavioralRecords"));
//behavioralRecordsPermission.AddChild(StudentPermissions.BehavioralRecords.Create,
//	L("Permission:Student.BehavioralRecords.Create"));
//behavioralRecordsPermission.AddChild(StudentPermissions.BehavioralRecords.Edit,
//	L("Permission:Student.BehavioralRecords.Edit"));
//behavioralRecordsPermission.AddChild(StudentPermissions.BehavioralRecords.Delete,
//	L("Permission:Student.BehavioralRecords.Delete"));

//// Define permissions for HealthRecords
//var healthRecordsPermission = studentGroup.AddPermission(StudentPermissions.HealthRecords.Default,
//	L("Permission:Student.HealthRecords"));
//healthRecordsPermission.AddChild(StudentPermissions.HealthRecords.Create,
//	L("Permission:Student.HealthRecords.Create"));
//healthRecordsPermission.AddChild(StudentPermissions.HealthRecords.Edit,
//	L("Permission:Student.HealthRecords.Edit"));
//healthRecordsPermission.AddChild(StudentPermissions.HealthRecords.Delete,
//	L("Permission:Student.HealthRecords.Delete"));

//// Define permissions for FinancialInformation
//var financialInformationPermission = studentGroup.AddPermission(StudentPermissions.FinancialInformation.Default,
//	L("Permission:Student.FinancialInformation"));
//financialInformationPermission.AddChild(StudentPermissions.FinancialInformation.Create,
//	L("Permission:Student.FinancialInformation.Create"));
//financialInformationPermission.AddChild(StudentPermissions.FinancialInformation.Edit,
//	L("Permission:Student.FinancialInformation.Edit"));
//financialInformationPermission.AddChild(StudentPermissions.FinancialInformation.Delete,
//	L("Permission:Student.FinancialInformation.Delete"));

//// Define permissions for DocumentManagement
//var documentManagementPermission = studentGroup.AddPermission(StudentPermissions.DocumentManagement.Default,
//	L("Permission:Student.DocumentManagement"));
//documentManagementPermission.AddChild(StudentPermissions.DocumentManagement.Create,
//	L("Permission:Student.DocumentManagement.Create"));
//documentManagementPermission.AddChild(StudentPermissions.DocumentManagement.Edit,
//	L("Permission:Student.DocumentManagement.Edit"));
//documentManagementPermission.AddChild(StudentPermissions.DocumentManagement.Delete,
//	L("Permission:Student.DocumentManagement.Delete"));

//// Define permissions for CommunicationLog
//var communicationLogPermission = studentGroup.AddPermission(StudentPermissions.CommunicationLog.Default,
//	L("Permission:Student.CommunicationLog"));
//communicationLogPermission.AddChild(StudentPermissions.CommunicationLog.Create,
//	L("Permission:Student.CommunicationLog.Create"));
//communicationLogPermission.AddChild(StudentPermissions.CommunicationLog.Edit,
//	L("Permission:Student.CommunicationLog.Edit"));
//communicationLogPermission.AddChild(StudentPermissions.CommunicationLog.Delete,
//	L("Permission:Student.CommunicationLog.Delete"));

//// Define permissions for StudentProgressionPlanner
//var studentProgressionPlannerPermission = studentGroup.AddPermission(
//	StudentPermissions.StudentProgressionPlanner.Default, L("Permission:Student.StudentProgressionPlanner"));
//studentProgressionPlannerPermission.AddChild(StudentPermissions.StudentProgressionPlanner.Create,
//	L("Permission:Student.StudentProgressionPlanner.Create"));
//studentProgressionPlannerPermission.AddChild(StudentPermissions.StudentProgressionPlanner.Edit,
//	L("Permission:Student.StudentProgressionPlanner.Edit"));
//studentProgressionPlannerPermission.AddChild(StudentPermissions.StudentProgressionPlanner.Delete,
//	L("Permission:Student.StudentProgressionPlanner.Delete"));

//// Define permissions for TransportAndLogistics
//var transportAndLogisticsPermission = studentGroup.AddPermission(
//	StudentPermissions.TransportAndLogistics.Default, L("Permission:Student.TransportAndLogistics"));
//transportAndLogisticsPermission.AddChild(StudentPermissions.TransportAndLogistics.Create,
//	L("Permission:Student.TransportAndLogistics.Create"));
//transportAndLogisticsPermission.AddChild(StudentPermissions.TransportAndLogistics.Edit,
//	L("Permission:Student.TransportAndLogistics.Edit"));
//transportAndLogisticsPermission.AddChild(StudentPermissions.TransportAndLogistics.Delete,
//	L("Permission:Student.TransportAndLogistics.Delete"));

//// Define permissions for LibraryInteractions
//var libraryInteractionsPermission = studentGroup.AddPermission(StudentPermissions.LibraryInteractions.Default,
//	L("Permission:Student.LibraryInteractions"));
//libraryInteractionsPermission.AddChild(StudentPermissions.LibraryInteractions.Create,
//	L("Permission:Student.LibraryInteractions.Create"));
//libraryInteractionsPermission.AddChild(StudentPermissions.LibraryInteractions.Edit,
//	L("Permission:Student.LibraryInteractions.Edit"));
//libraryInteractionsPermission.AddChild(StudentPermissions.LibraryInteractions.Delete,
//	L("Permission:Student.LibraryInteractions.Delete"));

//// Define permissions for SpecialEducationNeeds
//var specialEducationNeedsPermission = studentGroup.AddPermission(
//	StudentPermissions.SpecialEducationNeeds.Default, L("Permission:Student.SpecialEducationNeeds"));
//specialEducationNeedsPermission.AddChild(StudentPermissions.SpecialEducationNeeds.Create,
//	L("Permission:Student.SpecialEducationNeeds.Create"));
//specialEducationNeedsPermission.AddChild(StudentPermissions.SpecialEducationNeeds.Edit,
//	L("Permission:Student.SpecialEducationNeeds.Edit"));
//specialEducationNeedsPermission.AddChild(StudentPermissions.SpecialEducationNeeds.Delete,
//	L("Permission:Student.SpecialEducationNeeds.Delete"));
