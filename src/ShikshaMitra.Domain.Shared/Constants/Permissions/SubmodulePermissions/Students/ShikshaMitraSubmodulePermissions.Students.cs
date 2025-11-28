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
			// Enrollment submodules (defined in Students.Enrollment.cs)
			{ Enrollment_Inquiry.Default, new SubmoduleInfo(Enrollment_Inquiry.All, Enrollment_Inquiry.DisplayNames) },
			{ Enrollment_RegistrationFormIssued.Default, new SubmoduleInfo(Enrollment_RegistrationFormIssued.All, Enrollment_RegistrationFormIssued.DisplayNames) },
			{ Enrollment_EntranceExam.Default, new SubmoduleInfo(Enrollment_EntranceExam.All, Enrollment_EntranceExam.DisplayNames) },
			{ Enrollment_Admission.Default, new SubmoduleInfo(Enrollment_Admission.All, Enrollment_Admission.DisplayNames) },
			{ Enrollment_Reports.Default, new SubmoduleInfo(Enrollment_Reports.All, Enrollment_Reports.DisplayNames) },
			{ Enrollment_SettingsDocumentTypes.Default, new SubmoduleInfo(Enrollment_SettingsDocumentTypes.All, Enrollment_SettingsDocumentTypes.DisplayNames) },
			{ Enrollment_SettingsInquiryPreference.Default, new SubmoduleInfo(Enrollment_SettingsInquiryPreference.All, Enrollment_SettingsInquiryPreference.DisplayNames) },
			{ Enrollment_SettingsEntranceExamSlots.Default, new SubmoduleInfo(Enrollment_SettingsEntranceExamSlots.All, Enrollment_SettingsEntranceExamSlots.DisplayNames) },
			{ Enrollment_SettingsRegistrationFormAmount.Default, new SubmoduleInfo(Enrollment_SettingsRegistrationFormAmount.All, Enrollment_SettingsRegistrationFormAmount.DisplayNames) },
			{ Enrollment_SettingsFormNumberSeries.Default, new SubmoduleInfo(Enrollment_SettingsFormNumberSeries.All, Enrollment_SettingsFormNumberSeries.DisplayNames) },

			// Records submodules (defined in Students.Records.cs)
			{ Records_StudentList.Default, new SubmoduleInfo(Records_StudentList.All, Records_StudentList.DisplayNames) },
			{ Records_StudentICard.Default, new SubmoduleInfo(Records_StudentICard.All, Records_StudentICard.DisplayNames) },
			{ Records_StudentManagement.Default, new SubmoduleInfo(Records_StudentManagement.All, Records_StudentManagement.DisplayNames) },
			{ Records_CertificatesBonafide.Default, new SubmoduleInfo(Records_CertificatesBonafide.All, Records_CertificatesBonafide.DisplayNames) },
			{ Records_CertificatesFirstTrial.Default, new SubmoduleInfo(Records_CertificatesFirstTrial.All, Records_CertificatesFirstTrial.DisplayNames) },
			{ Records_CertificatesLeaving.Default, new SubmoduleInfo(Records_CertificatesLeaving.All, Records_CertificatesLeaving.DisplayNames) },
			{ Records_CertificatesCharacter.Default, new SubmoduleInfo(Records_CertificatesCharacter.All, Records_CertificatesCharacter.DisplayNames) },
			{ Records_Settings.Default, new SubmoduleInfo(Records_Settings.All, Records_Settings.DisplayNames) },

			// Attendance submodules (defined in Students.Attendance.cs)
			{ Attendance_MarkAttendance.Default, new SubmoduleInfo(Attendance_MarkAttendance.All, Attendance_MarkAttendance.DisplayNames) },
			{ Attendance_LeaveAndAbsent.Default, new SubmoduleInfo(Attendance_LeaveAndAbsent.All, Attendance_LeaveAndAbsent.DisplayNames) },
			{ Attendance_Reports.Default, new SubmoduleInfo(Attendance_Reports.All, Attendance_Reports.DisplayNames) },
			{ Attendance_SettingLeaveType.Default, new SubmoduleInfo(Attendance_SettingLeaveType.All, Attendance_SettingLeaveType.DisplayNames) },

			// Notification submodules (defined in Students.Notification.cs)
			{ Notification_InquiryManagement.Default, new SubmoduleInfo(Notification_InquiryManagement.All, Notification_InquiryManagement.DisplayNames) },
			{ Notification_RegistrationManagement.Default, new SubmoduleInfo(Notification_RegistrationManagement.All, Notification_RegistrationManagement.DisplayNames) },
			{ Notification_AdmissionManagement.Default, new SubmoduleInfo(Notification_AdmissionManagement.All, Notification_AdmissionManagement.DisplayNames) },
			{ Notification_StudentRecordManagement.Default, new SubmoduleInfo(Notification_StudentRecordManagement.All, Notification_StudentRecordManagement.DisplayNames) },
			{ Notification_AttendanceManagement.Default, new SubmoduleInfo(Notification_AttendanceManagement.All, Notification_AttendanceManagement.DisplayNames) },

			// Reports submodules (defined in Students.Reports.cs)
			{ Reports.Default, new SubmoduleInfo(Reports.All, Reports.DisplayNames) }
		};
	}
}
