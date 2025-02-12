using System;

namespace ShikshaMitra.Students;

public static class StudentPermissions
{
	public const string GroupName = "Student";

	#region StudentEnrollment
	public static class Enrollment
	{
		public const string Default = GroupName + ".Enrollment";

		// Dashboard Permission with granular operations
		public const string Dashboard = Default + ".Dashboard";
		public const string Dashboard_Create = Dashboard + ".Create";
		public const string Dashboard_Edit = Dashboard + ".Edit";
		public const string Dashboard_Delete = Dashboard + ".Delete";

		// Inquiry Permission with granular operations
		public const string Inquiry = Default + ".Inquiry";
		public const string Inquiry_Create = Inquiry + ".Create";
		public const string Inquiry_Edit = Inquiry + ".Edit";
		public const string Inquiry_Delete = Inquiry + ".Delete";

		// RegistrationForm Permission with granular operations
		public const string RegistrationForm = Default + ".RegistrationForm";
		public const string RegistrationForm_Create = RegistrationForm + ".Create";
		public const string RegistrationForm_Edit = RegistrationForm + ".Edit";
		public const string RegistrationForm_Delete = RegistrationForm + ".Delete";

		// Registration Permission with granular operations
		public const string Registration = Default + ".Registration";
		public const string Registration_Create = Registration + ".Create";
		public const string Registration_Edit = Registration + ".Edit";
		public const string Registration_Delete = Registration + ".Delete";

		// EntranceExam Permission with granular operations
		public const string EntranceExam = Default + ".EntranceExam";
		public const string EntranceExam_Create = EntranceExam + ".Create";
		public const string EntranceExam_Edit = EntranceExam + ".Edit";
		public const string EntranceExam_Delete = EntranceExam + ".Delete";

		// FollowUp Permission with granular operations
		public const string FollowUp = Default + ".FollowUp";
		public const string FollowUp_Create = FollowUp + ".Create";
		public const string FollowUp_Edit = FollowUp + ".Edit";
		public const string FollowUp_Delete = FollowUp + ".Delete";

		// Admission Permission with granular operations
		public const string Admission = Default + ".Admission";
		public const string Admission_Create = Admission + ".Create";
		public const string Admission_Edit = Admission + ".Edit";
		public const string Admission_Delete = Admission + ".Delete";

		// AdmissionFees Permission with granular operations
		public const string AdmissionFees = Default + ".AdmissionFees";
		public const string AdmissionFees_Create = AdmissionFees + ".Create";
		public const string AdmissionFees_Edit = AdmissionFees + ".Edit";
		public const string AdmissionFees_Delete = AdmissionFees + ".Delete";

		// Reports Permission with granular operations
		public const string Reports = Default + ".Reports";
		public const string Reports_Create = Reports + ".Create";
		public const string Reports_Edit = Reports + ".Edit";
		public const string Reports_Delete = Reports + ".Delete";

		// Setting Permission with granular operations
		public const string Setting = Default + ".Setting";
		public const string Setting_Create = Setting + ".Create";
		public const string Setting_Edit = Setting + ".Edit";
		public const string Setting_Delete = Setting + ".Delete";
	}
	#endregion
}

//public static class IndividualStudentProfile
//{
//	public const string Default = GroupName + ".IndividualStudentProfile";
//	public const string Create = Default + ".Create";
//	public const string Edit = Default + ".Edit";
//	public const string Delete = Default + ".Delete";
//}

//public static class AttendanceTracker
//{
//	public const string Default = GroupName + ".AttendanceTracker";
//	public const string Create = Default + ".Create";
//	public const string Edit = Default + ".Edit";
//	public const string Delete = Default + ".Delete";
//}

//public static class AcademicPerformance
//{
//	public const string Default = GroupName + ".AcademicPerformance";
//	public const string Create = Default + ".Create";
//	public const string Edit = Default + ".Edit";
//	public const string Delete = Default + ".Delete";
//}

//public static class BehavioralRecords
//{
//	public const string Default = GroupName + ".BehavioralRecords";
//	public const string Create = Default + ".Create";
//	public const string Edit = Default + ".Edit";
//	public const string Delete = Default + ".Delete";
//}

//public static class HealthRecords
//{
//	public const string Default = GroupName + ".HealthRecords";
//	public const string Create = Default + ".Create";
//	public const string Edit = Default + ".Edit";
//	public const string Delete = Default + ".Delete";
//}

//public static class FinancialInformation
//{
//	public const string Default = GroupName + ".FinancialInformation";
//	public const string Create = Default + ".Create";
//	public const string Edit = Default + ".Edit";
//	public const string Delete = Default + ".Delete";
//}

//public static class DocumentManagement
//{
//	public const string Default = GroupName + ".DocumentManagement";
//	public const string Create = Default + ".Create";
//	public const string Edit = Default + ".Edit";
//	public const string Delete = Default + ".Delete";
//}

//public static class CommunicationLog
//{
//	public const string Default = GroupName + ".CommunicationLog";
//	public const string Create = Default + ".Create";
//	public const string Edit = Default + ".Edit";
//	public const string Delete = Default + ".Delete";
//}

//public static class StudentProgressionPlanner
//{
//	public const string Default = GroupName + ".StudentProgressionPlanner";
//	public const string Create = Default + ".Create";
//	public const string Edit = Default + ".Edit";
//	public const string Delete = Default + ".Delete";
//}

//public static class TransportAndLogistics
//{
//	public const string Default = GroupName + ".TransportAndLogistics";
//	public const string Create = Default + ".Create";
//	public const string Edit = Default + ".Edit";
//	public const string Delete = Default + ".Delete";
//}

//public static class LibraryInteractions
//{
//	public const string Default = GroupName + ".LibraryInteractions";
//	public const string Create = Default + ".Create";
//	public const string Edit = Default + ".Edit";
//	public const string Delete = Default + ".Delete";
//}

//public static class SpecialEducationNeeds
//{
//	public const string Default = GroupName + ".SpecialEducationNeeds";
//	public const string Create = Default + ".Create";
//	public const string Edit = Default + ".Edit";
//	public const string Delete = Default + ".Delete";
//}

//}