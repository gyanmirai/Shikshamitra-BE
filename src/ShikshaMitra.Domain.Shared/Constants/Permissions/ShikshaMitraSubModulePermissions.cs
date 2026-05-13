using System.Collections.Generic;

namespace ShikshaMitra.Constants.Permissions;

public static class ShikshaMitraSubModulePermissions
{
	// student module submodules
	public const string StudentDashboard = $"{ShikshaMitraModulePermissions.Students}.Dashboard";
	public const string Enrollment = $"{ShikshaMitraModulePermissions.Students}.Enrollment";
	public const string EnrollmentSettings = $"{ShikshaMitraModulePermissions.Students}.Enrollment.Settings";
	public const string Records = $"{ShikshaMitraModulePermissions.Students}.Records";
	public const string RecordsSettings = $"{ShikshaMitraModulePermissions.Students}.Records.Settings";
	public const string Attendance = $"{ShikshaMitraModulePermissions.Students}.Attendance";
	public const string AttendanceSettings = $"{ShikshaMitraModulePermissions.Students}.Attendance.Settings";
	public const string Notification = $"{ShikshaMitraModulePermissions.Students}.Notification";

	// Academics module submodules
	public const string AcademicsDashboard = $"{ShikshaMitraModulePermissions.Academics}.Dashboard";
	public const string Curriculum = $"{ShikshaMitraModulePermissions.Academics}.Curriculum";
	public const string CurriculumSettings = $"{ShikshaMitraModulePermissions.Academics}.Curriculum.Settings";
	public const string Timetable = $"{ShikshaMitraModulePermissions.Academics}.Timetable";
	public const string TimetableSettings = $"{ShikshaMitraModulePermissions.Academics}.Timetable.Settings";
	public const string HomeworkAssignment = $"{ShikshaMitraModulePermissions.Academics}.HomeworkAssignment";
	public const string HomeworkAssignmentSettings = $"{ShikshaMitraModulePermissions.Academics}.HomeworkAssignment.Settings";
	public const string Examination = $"{ShikshaMitraModulePermissions.Academics}.Examination";
	public const string ExaminationSettings = $"{ShikshaMitraModulePermissions.Academics}.Examination.Settings";
	public const string GroupManagement = $"{ShikshaMitraModulePermissions.Academics}.GroupManagement";
	public const string LMS = $"{ShikshaMitraModulePermissions.Academics}.LMS";
	public const string LMSSettings = $"{ShikshaMitraModulePermissions.Academics}.LMS.Settings";
	public const string AcademicsNotification = $"{ShikshaMitraModulePermissions.Academics}.Notification";

	// Class management module submodules
	public const string ClassManagementDashboard = $"{ShikshaMitraModulePermissions.ClassManagement}.Dashboard";
	public const string ElectiveAssignment = $"{ShikshaMitraModulePermissions.ClassManagement}.ElectiveAssignment";
	public const string Chapters = $"{ShikshaMitraModulePermissions.ClassManagement}.Chapters";
	public const string TeacherAssignment = $"{ShikshaMitraModulePermissions.ClassManagement}.TeacherAssignment";
	public const string ClassTeacherAssignment = $"{ShikshaMitraModulePermissions.ClassManagement}.ClassTeacherAssignment";
	public const string ClassManagementNotification = $"{ShikshaMitraModulePermissions.ClassManagement}.Notification";

	// HR module submodules
	public const string HRDashboard = $"{ShikshaMitraModulePermissions.HR}.Dashboard";
	public const string EmployeeRecords = $"{ShikshaMitraModulePermissions.HR}.Employee";
	public const string EmployeeRecordsSettings = $"{ShikshaMitraModulePermissions.HR}.Employee.Settings";
	public const string Payroll = $"{ShikshaMitraModulePermissions.HR}.Payroll";
	public const string PayrollSettings = $"{ShikshaMitraModulePermissions.HR}.Payroll.Settings";
	public const string AttendanceManagement = $"{ShikshaMitraModulePermissions.HR}.AttendanceManagement";
	public const string AttendanceManagementSettings = $"{ShikshaMitraModulePermissions.HR}.AttendanceManagement.Settings";
	public const string ProfessionalManagement = $"{ShikshaMitraModulePermissions.HR}.ProfessionalManagement";
	public const string HRReports = $"{ShikshaMitraModulePermissions.HR}.Reports";
	public const string HRNotification = $"{ShikshaMitraModulePermissions.HR}.Notification";

	// Finance module submodules
	public const string FinanceDashboard = $"{ShikshaMitraModulePermissions.Finance}.Dashboard";
	public const string FeeCollection = $"{ShikshaMitraModulePermissions.Finance}.FeeCollection";
	public const string Charges = $"{ShikshaMitraModulePermissions.Finance}.Charges";
	public const string FeeRefund = $"{ShikshaMitraModulePermissions.Finance}.FeeRefund";
	public const string Concession = $"{ShikshaMitraModulePermissions.Finance}.Concession";
	public const string InvoiceReceipt = $"{ShikshaMitraModulePermissions.Finance}.InvoiceReceipt";
	public const string Reconciliation = $"{ShikshaMitraModulePermissions.Finance}.Reconciliation";
	public const string IncomeTracking = $"{ShikshaMitraModulePermissions.Finance}.IncomeTracking";
	public const string ExpenseTracking = $"{ShikshaMitraModulePermissions.Finance}.ExpenseTracking";
	public const string Budgeting = $"{ShikshaMitraModulePermissions.Finance}.Budgeting";
	public const string FinanceReports = $"{ShikshaMitraModulePermissions.Finance}.Reports";
	public const string FinanceNotification = $"{ShikshaMitraModulePermissions.Finance}.Notification";
	public const string FinanceSettings = $"{ShikshaMitraModulePermissions.Finance}.Settings";
	public const string Fees = $"{ShikshaMitraModulePermissions.Finance}.Fees";
	public const string FeesSettings = $"{ShikshaMitraModulePermissions.Finance}.Fees.Settings";
	public const string Accounts = $"{ShikshaMitraModulePermissions.Finance}.Accounts";
	public const string IncomeAndExpense = $"{ShikshaMitraModulePermissions.Finance}.IncomeAndExpense";
	public const string AccountsSettings = $"{ShikshaMitraModulePermissions.Finance}.Accounts.Settings";
	public const string FinanceSettingsFeeCategory = $"{ShikshaMitraModulePermissions.Finance}.Settings.FeeCategory";
	public const string FinanceSettingsFeeHead = $"{ShikshaMitraModulePermissions.Finance}.Settings.FeeHead";
	public const string FinanceSettingsReceiptBook = $"{ShikshaMitraModulePermissions.Finance}.Settings.ReceiptBook";
	public const string FinanceSettingsPaymentTerms = $"{ShikshaMitraModulePermissions.Finance}.Settings.PaymentTerms";
	public const string FinanceSettingsFeeStructure = $"{ShikshaMitraModulePermissions.Finance}.Settings.FeeStructure";
	public const string FinanceSettingsConcessionType = $"{ShikshaMitraModulePermissions.Finance}.Settings.ConcessionType";
	public const string FinanceSettingsChargeCategory = $"{ShikshaMitraModulePermissions.Finance}.Settings.ChargeCategory";
	public const string FinanceSettingsExpenseCategory = $"{ShikshaMitraModulePermissions.Finance}.Settings.ExpenseCategory";
	public const string FinanceSettingsIncomeCategory = $"{ShikshaMitraModulePermissions.Finance}.Settings.IncomeCategory";
	public const string FinanceSettingsBankAccount = $"{ShikshaMitraModulePermissions.Finance}.Settings.BankAccount";

	// Communication module submodules
	public const string CommunicationDashboard = $"{ShikshaMitraModulePermissions.Communication}.Dashboard";
	public const string Announcements = $"{ShikshaMitraModulePermissions.Communication}.Announcements";
	public const string CommunicationNotification = $"{ShikshaMitraModulePermissions.Communication}.Notification";
	public const string NotificationHistory = $"{ShikshaMitraModulePermissions.Communication}.NotificationHistory";
	public const string CommunicationSettings = $"{ShikshaMitraModulePermissions.Communication}.Settings";

	// Calendar module submodules
	public const string CalendarDashboard = $"{ShikshaMitraModulePermissions.Calendar}.Dashboard";
	public const string CalendarView = $"{ShikshaMitraModulePermissions.Calendar}.CalendarView";
	public const string Events = $"{ShikshaMitraModulePermissions.Calendar}.Events";
	public const string Holidays = $"{ShikshaMitraModulePermissions.Calendar}.Holidays";
	public const string CalendarGroupManagement = $"{ShikshaMitraModulePermissions.Calendar}.GroupManagement";
	public const string CalendarSettings = $"{ShikshaMitraModulePermissions.Calendar}.Settings";

	// Library module submodules
	public const string LibraryDashboard = $"{ShikshaMitraModulePermissions.Library}.Dashboard";
	public const string AllBooks = $"{ShikshaMitraModulePermissions.Library}.AllBooks";
	public const string IssueReturn = $"{ShikshaMitraModulePermissions.Library}.IssueReturn";
	public const string LibraryMembers = $"{ShikshaMitraModulePermissions.Library}.LibraryMembers";
	public const string LibraryCharges = $"{ShikshaMitraModulePermissions.Library}.Charges";
	public const string LibraryReports = $"{ShikshaMitraModulePermissions.Library}.Reports";
	public const string LibraryNotification = $"{ShikshaMitraModulePermissions.Library}.Notification";
	public const string LibrarySettings = $"{ShikshaMitraModulePermissions.Library}.Settings";

	// Transport module submodules
	public const string TransportDashboard = $"{ShikshaMitraModulePermissions.Transport}.Dashboard";
	public const string StopManagement = $"{ShikshaMitraModulePermissions.Transport}.StopManagement";
	public const string RouteManagement = $"{ShikshaMitraModulePermissions.Transport}.RouteManagement";
	public const string VehicleManagement = $"{ShikshaMitraModulePermissions.Transport}.VehicleManagement";
	public const string TransportStaffManagement = $"{ShikshaMitraModulePermissions.Transport}.TransportStaffManagement";
	public const string ApplicationManagement = $"{ShikshaMitraModulePermissions.Transport}.ApplicationManagement";
	public const string TransportReports = $"{ShikshaMitraModulePermissions.Transport}.Reports";
	public const string TransportNotification = $"{ShikshaMitraModulePermissions.Transport}.Notification";
	public const string TransportSettings = $"{ShikshaMitraModulePermissions.Transport}.Settings";

	// Resource Management module submodules
	public const string ResourceManagementDashboard = $"{ShikshaMitraModulePermissions.ResourceManagement}.Dashboard";
	public const string InventoryManagement = $"{ShikshaMitraModulePermissions.ResourceManagement}.InventoryManagement";
	public const string MyResources = $"{ShikshaMitraModulePermissions.ResourceManagement}.MyResources";
	public const string MyResourceQuery = $"{ShikshaMitraModulePermissions.ResourceManagement}.MyResourceQuery";
	public const string ReviewResourceRequest = $"{ShikshaMitraModulePermissions.ResourceManagement}.ReviewResourceRequest";
	public const string OrderRequest = $"{ShikshaMitraModulePermissions.ResourceManagement}.OrderRequest";
	public const string MaintenanceRequest = $"{ShikshaMitraModulePermissions.ResourceManagement}.MaintenanceRequest";
	public const string DisposedResources = $"{ShikshaMitraModulePermissions.ResourceManagement}.DisposedResources";
	public const string ResourceAllocation = $"{ShikshaMitraModulePermissions.ResourceManagement}.ResourceAllocation";
	public const string ProcurementVendors = $"{ShikshaMitraModulePermissions.ResourceManagement}.ProcurementVendors";
	public const string ReportLossDamage = $"{ShikshaMitraModulePermissions.ResourceManagement}.ReportLossDamage";
	public const string IncidentReport = $"{ShikshaMitraModulePermissions.ResourceManagement}.IncidentReport";
	public const string AuditVerification = $"{ShikshaMitraModulePermissions.ResourceManagement}.AuditVerification";
	public const string ResourceManagementReports = $"{ShikshaMitraModulePermissions.ResourceManagement}.Reports";
	public const string ResourceManagementSettings = $"{ShikshaMitraModulePermissions.ResourceManagement}.Settings";

	// Security module submodules
	public const string SecurityDashboard = $"{ShikshaMitraModulePermissions.Security}.Dashboard";
	public const string RoleAndPermission = $"{ShikshaMitraModulePermissions.Security}.RoleAndPermission";
	public const string AbpIdentityRolesAndPermissions = "AbpIdentity.RolesAndPermissions";

	// Hostel module submodules
	public const string HostelDashboard = $"{ShikshaMitraModulePermissions.Hostel}.Dashboard";
	public const string NewRequest = $"{ShikshaMitraModulePermissions.Hostel}.NewRequest";
	public const string HostelManagement = $"{ShikshaMitraModulePermissions.Hostel}.HostelManagement";
	public const string RoomAllocationToStudents = $"{ShikshaMitraModulePermissions.Hostel}.RoomAllocationToStudents";
	public const string FeeManagement = $"{ShikshaMitraModulePermissions.Hostel}.FeeManagement";
	public const string AttendanceAndLeaveManagement = $"{ShikshaMitraModulePermissions.Hostel}.AttendanceAndLeaveManagement";
	public const string VisitorEntryManagement = $"{ShikshaMitraModulePermissions.Hostel}.VisitorEntryManagement";
	public const string DisciplinaryActionsTracking = $"{ShikshaMitraModulePermissions.Hostel}.DisciplinaryActionsTracking";
	public const string Maintenance = $"{ShikshaMitraModulePermissions.Hostel}.Maintenance";
	public const string CateringManagement = $"{ShikshaMitraModulePermissions.Hostel}.CateringManagement";
	public const string ReportingAnalytics = $"{ShikshaMitraModulePermissions.Hostel}.ReportingAnalytics";
	public const string HostelSettings = $"{ShikshaMitraModulePermissions.Hostel}.Settings";
	public const string HostelNotification = $"{ShikshaMitraModulePermissions.Hostel}.Notification";

	// Security Guard module submodules
	public const string SecurityGuardDashboard = $"{ShikshaMitraModulePermissions.SecurityGuard}.Dashboard";
	public const string VisitorManagement = $"{ShikshaMitraModulePermissions.SecurityGuard}.VisitorManagement";
	public const string GatePassManagement = $"{ShikshaMitraModulePermissions.SecurityGuard}.GatePassManagement";
	public const string GuardManagement = $"{ShikshaMitraModulePermissions.SecurityGuard}.GuardManagement";
	public const string Alert = $"{ShikshaMitraModulePermissions.SecurityGuard}.Alert";
	public const string SecurityGuardReports = $"{ShikshaMitraModulePermissions.SecurityGuard}.Reports";

	// Institute Setup module submodules
	public const string BasicInformation = $"{ShikshaMitraModulePermissions.InstituteSetup}.BasicInformation";
	public const string AcademicSetup = $"{ShikshaMitraModulePermissions.InstituteSetup}.AcademicSetup";
	public const string ShiftManagement = $"{ShikshaMitraModulePermissions.InstituteSetup}.ShiftManagement";
	public const string RoomsManagement = $"{ShikshaMitraModulePermissions.InstituteSetup}.RoomsManagement";

	public static readonly string[] All =
	{
		StudentDashboard,
		Enrollment,
		EnrollmentSettings,
		Records,
		RecordsSettings,
		Attendance,
		AttendanceSettings,
		Notification,
		AcademicsDashboard,
		Curriculum,
		CurriculumSettings,
		Timetable,
		TimetableSettings,
		HomeworkAssignment,
		HomeworkAssignmentSettings,
		Examination,
		ExaminationSettings,
		GroupManagement,
		LMS,
		LMSSettings,
		AcademicsNotification,
		ClassManagementDashboard,
		ElectiveAssignment,
		Chapters,
		TeacherAssignment,
		ClassTeacherAssignment,
		ClassManagementNotification,
		HRDashboard,
		EmployeeRecords,
		EmployeeRecordsSettings,
		Payroll,
		PayrollSettings,
		AttendanceManagement,
		AttendanceManagementSettings,
		ProfessionalManagement,
		HRReports,
		HRNotification,
		FinanceDashboard,
		FeeCollection,
		Charges,
		FeeRefund,
		Concession,
		InvoiceReceipt,
		Reconciliation,
		IncomeTracking,
		ExpenseTracking,
		Budgeting,
		FinanceReports,
		FinanceNotification,
		FinanceSettings,
		Fees,
		FeesSettings,
		Accounts,
		IncomeAndExpense,
		AccountsSettings,
		FinanceSettingsFeeCategory,
		FinanceSettingsFeeHead,
		FinanceSettingsReceiptBook,
		FinanceSettingsPaymentTerms,
		FinanceSettingsFeeStructure,
		FinanceSettingsConcessionType,
		FinanceSettingsChargeCategory,
		FinanceSettingsExpenseCategory,
		FinanceSettingsIncomeCategory,
		FinanceSettingsBankAccount,
		CommunicationDashboard,
		Announcements,
		CommunicationNotification,
		NotificationHistory,
		CommunicationSettings,
		CalendarDashboard,
		CalendarView,
		Events,
		Holidays,
		CalendarGroupManagement,
		CalendarSettings,
		LibraryDashboard,
		AllBooks,
		IssueReturn,
		LibraryMembers,
		LibraryCharges,
		LibraryReports,
		LibraryNotification,
		LibrarySettings,
		TransportDashboard,
		StopManagement,
		RouteManagement,
		VehicleManagement,
		TransportStaffManagement,
		ApplicationManagement,
		TransportReports,
		TransportNotification,
		TransportSettings,
		ResourceManagementDashboard,
		InventoryManagement,
		MyResources,
		MyResourceQuery,
		ReviewResourceRequest,
		OrderRequest,
		MaintenanceRequest,
		DisposedResources,
		ResourceAllocation,
		ProcurementVendors,
		ReportLossDamage,
		IncidentReport,
		AuditVerification,
		ResourceManagementReports,
		ResourceManagementSettings,
		SecurityDashboard,
		RoleAndPermission,
		AbpIdentityRolesAndPermissions,
		HostelDashboard,
		NewRequest,
		HostelManagement,
		RoomAllocationToStudents,
		FeeManagement,
		AttendanceAndLeaveManagement,
		VisitorEntryManagement,
		DisciplinaryActionsTracking,
		Maintenance,
		CateringManagement,
		ReportingAnalytics,
		HostelSettings,
		HostelNotification,
		SecurityGuardDashboard,
		VisitorManagement,
		GatePassManagement,
		GuardManagement,
		Alert,
		SecurityGuardReports,
		BasicInformation,
		AcademicSetup,
		ShiftManagement,
		RoomsManagement
	};

	private const string DisplayNamePrefix = "Permission:SubModule";

	public static readonly Dictionary<string, string> DisplayNames = new()
	{
		// Student module submodules
		{ StudentDashboard, $"{DisplayNamePrefix}.StudentDashboard" },
		{ Enrollment, $"{DisplayNamePrefix}.Enrollment" },
		{ EnrollmentSettings, $"{DisplayNamePrefix}.EnrollmentSettings" },
		{ Records, $"{DisplayNamePrefix}.Records" },
		{ RecordsSettings, $"{DisplayNamePrefix}.RecordsSettings" },
		{ Attendance, $"{DisplayNamePrefix}.Attendance" },
		{ AttendanceSettings, $"{DisplayNamePrefix}.AttendanceSettings" },
		{ Notification, $"{DisplayNamePrefix}.Notification" },

		// Academics module submodules
		{ AcademicsDashboard, $"{DisplayNamePrefix}.AcademicsDashboard" },
		{ Curriculum, $"{DisplayNamePrefix}.Curriculum" },
		{ CurriculumSettings, $"{DisplayNamePrefix}.CurriculumSettings" },
		{ Timetable, $"{DisplayNamePrefix}.Timetable" },
		{ TimetableSettings, $"{DisplayNamePrefix}.TimetableSettings" },
		{ HomeworkAssignment, $"{DisplayNamePrefix}.HomeworkAssignment" },
		{ HomeworkAssignmentSettings, $"{DisplayNamePrefix}.HomeworkAssignmentSettings" },
		{ Examination, $"{DisplayNamePrefix}.Examination" },
		{ ExaminationSettings, $"{DisplayNamePrefix}.ExaminationSettings" },
		{ GroupManagement, $"{DisplayNamePrefix}.GroupManagement" },
		{ LMS, $"{DisplayNamePrefix}.LMS" },
		{ LMSSettings, $"{DisplayNamePrefix}.LMSSettings" },
		{ AcademicsNotification, $"{DisplayNamePrefix}.AcademicsNotification" },

		// Class Management module submodules
		{ ClassManagementDashboard, $"{DisplayNamePrefix}.ClassManagementDashboard" },
		{ ElectiveAssignment, $"{DisplayNamePrefix}.ElectiveAssignment" },
		{ Chapters, $"{DisplayNamePrefix}.Chapters" },
		{ TeacherAssignment, $"{DisplayNamePrefix}.TeacherAssignment" },
		{ ClassTeacherAssignment, $"{DisplayNamePrefix}.ClassTeacherAssignment" },
		{ ClassManagementNotification, $"{DisplayNamePrefix}.ClassManagementNotification" },

		// HR module submodules
		{ HRDashboard, $"{DisplayNamePrefix}.HRDashboard" },
		{ EmployeeRecords, $"{DisplayNamePrefix}.EmployeeRecords" },
		{ EmployeeRecordsSettings, $"{DisplayNamePrefix}.EmployeeRecordsSettings" },
		{ Payroll, $"{DisplayNamePrefix}.Payroll" },
		{ PayrollSettings, $"{DisplayNamePrefix}.PayrollSettings" },
		{ AttendanceManagement, $"{DisplayNamePrefix}.AttendanceManagement" },
		{ AttendanceManagementSettings, $"{DisplayNamePrefix}.AttendanceManagementSettings" },
		{ ProfessionalManagement, $"{DisplayNamePrefix}.ProfessionalManagement" },
		{ HRReports, $"{DisplayNamePrefix}.HRReports" },
		{ HRNotification, $"{DisplayNamePrefix}.HRNotification" },

		// Finance module submodules
		{ FinanceDashboard, $"{DisplayNamePrefix}.FinanceDashboard" },
		{ FeeCollection, $"{DisplayNamePrefix}.FeeCollection" },
		{ Charges, $"{DisplayNamePrefix}.Charges" },
		{ FeeRefund, $"{DisplayNamePrefix}.FeeRefund" },
		{ Concession, $"{DisplayNamePrefix}.Concession" },
		{ InvoiceReceipt, $"{DisplayNamePrefix}.InvoiceReceipt" },
		{ Reconciliation, $"{DisplayNamePrefix}.Reconciliation" },
		{ IncomeTracking, $"{DisplayNamePrefix}.IncomeTracking" },
		{ ExpenseTracking, $"{DisplayNamePrefix}.ExpenseTracking" },
		{ Budgeting, $"{DisplayNamePrefix}.Budgeting" },
		{ FinanceReports, $"{DisplayNamePrefix}.FinanceReports" },
		{ FinanceNotification, $"{DisplayNamePrefix}.FinanceNotification" },
		{ FinanceSettings, $"{DisplayNamePrefix}.FinanceSettings" },
		{ Fees, $"{DisplayNamePrefix}.Fees" },
		{ FeesSettings, $"{DisplayNamePrefix}.FeesSettings" },
		{ Accounts, $"{DisplayNamePrefix}.Accounts" },
		{ IncomeAndExpense, $"{DisplayNamePrefix}.IncomeAndExpense" },
		{ AccountsSettings, $"{DisplayNamePrefix}.AccountsSettings" },
		{ FinanceSettingsFeeCategory, $"{DisplayNamePrefix}.FinanceSettingsFeeCategory" },
		{ FinanceSettingsFeeHead, $"{DisplayNamePrefix}.FinanceSettingsFeeHead" },
		{ FinanceSettingsReceiptBook, $"{DisplayNamePrefix}.FinanceSettingsReceiptBook" },
		{ FinanceSettingsPaymentTerms, $"{DisplayNamePrefix}.FinanceSettingsPaymentTerms" },
		{ FinanceSettingsFeeStructure, $"{DisplayNamePrefix}.FinanceSettingsFeeStructure" },
		{ FinanceSettingsConcessionType, $"{DisplayNamePrefix}.FinanceSettingsConcessionType" },
		{ FinanceSettingsChargeCategory, $"{DisplayNamePrefix}.FinanceSettingsChargeCategory" },
		{ FinanceSettingsExpenseCategory, $"{DisplayNamePrefix}.FinanceSettingsExpenseCategory" },
		{ FinanceSettingsIncomeCategory, $"{DisplayNamePrefix}.FinanceSettingsIncomeCategory" },
		{ FinanceSettingsBankAccount, $"{DisplayNamePrefix}.FinanceSettingsBankAccount" },

		// Communication module submodules
		{ CommunicationDashboard, $"{DisplayNamePrefix}.CommunicationDashboard" },
		{ Announcements, $"{DisplayNamePrefix}.Announcements" },
		{ CommunicationNotification, $"{DisplayNamePrefix}.CommunicationNotification" },
		{ NotificationHistory, $"{DisplayNamePrefix}.NotificationHistory" },
		{ CommunicationSettings, $"{DisplayNamePrefix}.CommunicationSettings" },

		// Calendar module submodules
		{ CalendarDashboard, $"{DisplayNamePrefix}.CalendarDashboard" },
		{ CalendarView, $"{DisplayNamePrefix}.CalendarView" },
		{ Events, $"{DisplayNamePrefix}.Events" },
		{ Holidays, $"{DisplayNamePrefix}.Holidays" },
		{ CalendarGroupManagement, $"{DisplayNamePrefix}.CalendarGroupManagement" },
		{ CalendarSettings, $"{DisplayNamePrefix}.CalendarSettings" },

		// Library module submodules
		{ LibraryDashboard, $"{DisplayNamePrefix}.LibraryDashboard" },
		{ AllBooks, $"{DisplayNamePrefix}.AllBooks" },
		{ IssueReturn, $"{DisplayNamePrefix}.IssueReturn" },
		{ LibraryMembers, $"{DisplayNamePrefix}.LibraryMembers" },
		{ LibraryCharges, $"{DisplayNamePrefix}.LibraryCharges" },
		{ LibraryReports, $"{DisplayNamePrefix}.LibraryReports" },
		{ LibraryNotification, $"{DisplayNamePrefix}.LibraryNotification" },
		{ LibrarySettings, $"{DisplayNamePrefix}.LibrarySettings" },

		// Transport module submodules
		{ TransportDashboard, $"{DisplayNamePrefix}.TransportDashboard" },
		{ StopManagement, $"{DisplayNamePrefix}.StopManagement" },
		{ RouteManagement, $"{DisplayNamePrefix}.RouteManagement" },
		{ VehicleManagement, $"{DisplayNamePrefix}.VehicleManagement" },
		{ TransportStaffManagement, $"{DisplayNamePrefix}.TransportStaffManagement" },
		{ ApplicationManagement, $"{DisplayNamePrefix}.ApplicationManagement" },
		{ TransportReports, $"{DisplayNamePrefix}.TransportReports" },
		{ TransportNotification, $"{DisplayNamePrefix}.TransportNotification" },
		{ TransportSettings, $"{DisplayNamePrefix}.TransportSettings" },

		// Resource Management module submodules
		{ ResourceManagementDashboard, $"{DisplayNamePrefix}.ResourceManagementDashboard" },
		{ InventoryManagement, $"{DisplayNamePrefix}.InventoryManagement" },
		{ MyResources, $"{DisplayNamePrefix}.MyResources" },
		{ MyResourceQuery, $"{DisplayNamePrefix}.MyResourceQuery" },
		{ ReviewResourceRequest, $"{DisplayNamePrefix}.ReviewResourceRequest" },
		{ OrderRequest, $"{DisplayNamePrefix}.OrderRequest" },
		{ MaintenanceRequest, $"{DisplayNamePrefix}.MaintenanceRequest" },
		{ DisposedResources, $"{DisplayNamePrefix}.DisposedResources" },
		{ ResourceAllocation, $"{DisplayNamePrefix}.ResourceAllocation" },
		{ ProcurementVendors, $"{DisplayNamePrefix}.ProcurementVendors" },
		{ ReportLossDamage, $"{DisplayNamePrefix}.ReportLossDamage" },
		{ IncidentReport, $"{DisplayNamePrefix}.IncidentReport" },
		{ AuditVerification, $"{DisplayNamePrefix}.AuditVerification" },
		{ ResourceManagementReports, $"{DisplayNamePrefix}.ResourceManagementReports" },
		{ ResourceManagementSettings, $"{DisplayNamePrefix}.ResourceManagementSettings" },

		// Security module submodules
		{ SecurityDashboard, $"{DisplayNamePrefix}.SecurityDashboard" },
		{ RoleAndPermission, $"{DisplayNamePrefix}.RoleAndPermission" },
		{ AbpIdentityRolesAndPermissions, $"{DisplayNamePrefix}.AbpIdentityRolesAndPermissions" },

		// Hostel module submodules
		{ HostelDashboard, $"{DisplayNamePrefix}.HostelDashboard" },
		{ NewRequest, $"{DisplayNamePrefix}.NewRequest" },
		{ HostelManagement, $"{DisplayNamePrefix}.HostelManagement" },
		{ RoomAllocationToStudents, $"{DisplayNamePrefix}.RoomAllocationToStudents" },
		{ FeeManagement, $"{DisplayNamePrefix}.FeeManagement" },
		{ AttendanceAndLeaveManagement, $"{DisplayNamePrefix}.AttendanceAndLeaveManagement" },
		{ VisitorEntryManagement, $"{DisplayNamePrefix}.VisitorEntryManagement" },
		{ DisciplinaryActionsTracking, $"{DisplayNamePrefix}.DisciplinaryActionsTracking" },
		{ Maintenance, $"{DisplayNamePrefix}.Maintenance" },
		{ CateringManagement, $"{DisplayNamePrefix}.CateringManagement" },
		{ ReportingAnalytics, $"{DisplayNamePrefix}.ReportingAnalytics" },
		{ HostelSettings, $"{DisplayNamePrefix}.HostelSettings" },
		{ HostelNotification, $"{DisplayNamePrefix}.HostelNotification" },

		// Security Guard module submodules
		{ SecurityGuardDashboard, $"{DisplayNamePrefix}.SecurityGuardDashboard" },
		{ VisitorManagement, $"{DisplayNamePrefix}.VisitorManagement" },
		{ GatePassManagement, $"{DisplayNamePrefix}.GatePassManagement" },
		{ GuardManagement, $"{DisplayNamePrefix}.GuardManagement" },
		{ Alert, $"{DisplayNamePrefix}.Alert" },
		{ SecurityGuardReports, $"{DisplayNamePrefix}.SecurityGuardReports" },

		// Institute Setup module submodules
		{ BasicInformation, $"{DisplayNamePrefix}.BasicInformation" },
		{ AcademicSetup, $"{DisplayNamePrefix}.AcademicSetup" },
		{ ShiftManagement, $"{DisplayNamePrefix}.ShiftManagement" },
		{ RoomsManagement, $"{DisplayNamePrefix}.RoomsManagement" }
	};
}
