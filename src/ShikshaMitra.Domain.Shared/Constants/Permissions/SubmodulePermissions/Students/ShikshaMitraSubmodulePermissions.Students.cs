using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

/// <summary>
/// Students module permissions and submodules.
/// This file is split into multiple partial files for better maintainability:
/// - ShikshaMitraSubmodulePermissions.Students.cs (Main module definition)
/// - ShikshaMitraSubmodulePermissions.Students.Enrollment.cs
/// - ShikshaMitraSubmodulePermissions.Students.Records.cs
/// - ShikshaMitraSubmodulePermissions.Students.Attendance.cs
/// - ShikshaMitraSubmodulePermissions.Students.Notification.cs
/// - ShikshaMitraSubmodulePermissions.Students.Reports.cs
/// </summary>
public static partial class ShikshaMitraSubmodulePermissions
{
	/// <summary>
	/// Students module permissions
	/// </summary>
	public static partial class Students
	{
		/// <summary>
		/// Permission group name for Students module.
		/// This creates a separate permission group in ABP for better organization.
		/// </summary>
		public const string GroupName = "Students";

		/// <summary>
		/// Display name for Students module permission group
		/// </summary>
		public const string GroupDisplayName = "Permission:StudentsModule";

		private const string ModuleName = GroupName;
		private const string DisplayName = $"{DisplayNamePrefix}:{GroupName}";

		public const string Default = ModuleName;

		/// <summary>
		/// Module-level display names
		/// </summary>
		public static readonly Dictionary<string, string> DisplayNames = new()
		{
			{ Default, DisplayName }
		};

		/// <summary>
		/// Helper to get all submodules.
		/// This registry is populated across multiple partial files.
		/// </summary>
		public static readonly Dictionary<string, SubmoduleInfo> Submodules = new()
		{
			{ "Students.Dashboard", new SubmoduleInfo(Dashboard.All, Dashboard.DisplayNames) },
			{ "Students.Admissions", new SubmoduleInfo(Admissions.All, Admissions.DisplayNames) },
			{ "Students.Admissions.Offerings", new SubmoduleInfo(AdmissionOfferings.All, AdmissionOfferings.DisplayNames) },
			{ "Students.Admissions.Inquiries", new SubmoduleInfo(AdmissionInquiries.All, AdmissionInquiries.DisplayNames) },
			{ "Students.Admissions.Workflows", new SubmoduleInfo(AdmissionWorkflows.All, AdmissionWorkflows.DisplayNames) },
			{ "Students.Admissions.Forms", new SubmoduleInfo(AdmissionForms.All, AdmissionForms.DisplayNames) },
			{ "Students.Admissions.Applications", new SubmoduleInfo(AdmissionApplications.All, AdmissionApplications.DisplayNames) },
			{ "Students.Admissions.Assessments", new SubmoduleInfo(AdmissionAssessments.All, AdmissionAssessments.DisplayNames) },
			{ "Students.Admissions.Payments", new SubmoduleInfo(AdmissionPayments.All, AdmissionPayments.DisplayNames) },
			{ "Students.Admissions.Offers", new SubmoduleInfo(AdmissionOffers.All, AdmissionOffers.DisplayNames) },
			{ "Students.Admissions.Seats", new SubmoduleInfo(AdmissionSeats.All, AdmissionSeats.DisplayNames) },
			{ "Students.Admissions.Documents", new SubmoduleInfo(AdmissionDocuments.All, AdmissionDocuments.DisplayNames) },
			{ "Students.Admissions.Eligibility", new SubmoduleInfo(AdmissionEligibility.All, AdmissionEligibility.DisplayNames) },
			{ "Students.Admissions.Reviews", new SubmoduleInfo(AdmissionReviews.All, AdmissionReviews.DisplayNames) },
			{ "Students.Admissions.Applicants", new SubmoduleInfo(AdmissionApplicants.All, AdmissionApplicants.DisplayNames) },
			{ "Students.Onboarding", new SubmoduleInfo(StudentOnboarding.All, StudentOnboarding.DisplayNames) },
			{ "Students.IdentityCompliance", new SubmoduleInfo(IdentityCompliance.All, IdentityCompliance.DisplayNames) },

			// Records submodules (defined in Students.Records.cs)
			{ "Students.Records.StudentList", new SubmoduleInfo(Records_StudentList.All, Records_StudentList.DisplayNames) },
			{ "Students.Records.StudentICard", new SubmoduleInfo(Records_StudentICard.All, Records_StudentICard.DisplayNames) },
			{ "Students.Records.StudentManagement", new SubmoduleInfo(Records_StudentManagement.All, Records_StudentManagement.DisplayNames) },
			{ "Students.Records.Certificates", new SubmoduleInfo(Records_Certificates.All, Records_Certificates.DisplayNames) },
			{ "Students.Records.Reports", new SubmoduleInfo(Records_Reports.All, Records_Reports.DisplayNames) },
			{ "Students.Records.Settings", new SubmoduleInfo(Records_Settings.All, Records_Settings.DisplayNames) },

			// Attendance submodules (defined in Students.Attendance.cs)
			{ "Students.Attendance.MarkAttendance", new SubmoduleInfo(Attendance_MarkAttendance.All, Attendance_MarkAttendance.DisplayNames) },
			{ "Students.Attendance.LeaveAndAbsent", new SubmoduleInfo(Attendance_LeaveAndAbsent.All, Attendance_LeaveAndAbsent.DisplayNames) },
			{ "Students.Attendance.Reports", new SubmoduleInfo(Attendance_Reports.All, Attendance_Reports.DisplayNames) },
			{ "Students.Attendance.Setting.LeaveType", new SubmoduleInfo(Attendance_SettingLeaveType.All, Attendance_SettingLeaveType.DisplayNames) },

			// Notification submodules (defined in Students.Notification.cs)
			{ "Students.Notification.InquiryManagement", new SubmoduleInfo(Notification_InquiryManagement.All, Notification_InquiryManagement.DisplayNames) },
			{ "Students.Notification.RegistrationManagement", new SubmoduleInfo(Notification_RegistrationManagement.All, Notification_RegistrationManagement.DisplayNames) },
			{ "Students.Notification.AdmissionManagement", new SubmoduleInfo(Notification_AdmissionManagement.All, Notification_AdmissionManagement.DisplayNames) },
			{ "Students.Notification.StudentRecordManagement", new SubmoduleInfo(Notification_StudentRecordManagement.All, Notification_StudentRecordManagement.DisplayNames) },
			{ "Students.Notification.AttendanceManagement", new SubmoduleInfo(Notification_AttendanceManagement.All, Notification_AttendanceManagement.DisplayNames) },

			// Reports submodules (defined in Students.Reports.cs)
			{ "Students.Reports", new SubmoduleInfo(Reports.All, Reports.DisplayNames) }
		};
	}
}
