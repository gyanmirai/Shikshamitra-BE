using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.ClassTeacher;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Homework;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Academic.Syllabus;
using ShikshaMitra.Finance.Setting.GeneralSetting.FeeHead;
using ShikshaMitra.Finance.Setting.GeneralSetting.PaymentStatusOption;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.NotificationHistories;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Employees.Settings;
using ShikshaMitra.HR.EventAndHolidays;
using ShikshaMitra.HR.Payroll.PayrollStructures;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.Students;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicSetups.AcademicYears;
using ShikshaMitra.TenantSettings.AcademicSetups.Divisions;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.Divisions;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using ShikshaMitra.TenantSettings.RoomsManagement.Buildings;
using ShikshaMitra.TenantSettings.RoomsManagement.RoomTypes;
using ShikshaMitra.TenantSettings.RoomsManagement.Rooms;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.BlobStoring.Database.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.TenantManagement;
using Volo.Abp.TenantManagement.EntityFrameworkCore;

namespace ShikshaMitra.EntityFrameworkCore;

[ReplaceDbContext(typeof(IIdentityDbContext))]
[ReplaceDbContext(typeof(ITenantManagementDbContext))]
[ConnectionStringName("Default")]
public class ShikshaMitraDbContext(DbContextOptions<ShikshaMitraDbContext> options)
	: AbpDbContext<ShikshaMitraDbContext>(options),
		IIdentityDbContext,
		ITenantManagementDbContext
{
	/* Add DbSet properties for your Aggregate Roots / Entities here. */

	#region Entities from the modules

	/* Notice: We only implemented IIdentityDbContext and ITenantManagementDbContext
	 * and replaced them for this DbContext. This allows you to perform JOIN
	 * queries for the entities of these modules over the repositories easily. You
	 * typically don't need that for other modules. But, if you need, you can
	 * implement the DbContext interface of the needed module and use ReplaceDbContext
	 * attribute just like IIdentityDbContext and ITenantManagementDbContext.
	 *
	 * More info: Replacing a DbContext of a module ensures that the related module
	 * uses this DbContext on runtime. Otherwise, it will use its own DbContext class.
	 */

	//Identity
	public DbSet<IdentityUser> Users { get; set; }
	public DbSet<IdentityRole> Roles { get; set; }
	public DbSet<IdentityClaimType> ClaimTypes { get; set; }
	public DbSet<OrganizationUnit> OrganizationUnits { get; set; }
	public DbSet<IdentitySecurityLog> SecurityLogs { get; set; }
	public DbSet<IdentityLinkUser> LinkUsers { get; set; }
	public DbSet<IdentityUserDelegation> UserDelegations { get; set; }
	public DbSet<IdentitySession> Sessions { get; set; }

	#region Tenant Management

	// Tenant Management
	public DbSet<Tenant> Tenants { get; set; }
	public DbSet<TenantConnectionString> TenantConnectionStrings { get; set; }

	#endregion

	#endregion

	#region Security

	// UserScope
	public DbSet<UserScopeAssignment> UserScopeAssignments { get; set; }

	#endregion

	#region Tenant Settings

	public DbSet<AcademicYear> AcademicYears { get; set; }
	public DbSet<AcademicYearPermission> AcademicYearPermissions { get; set; }
	public DbSet<EducationBoard> EducationBoards { get; set; }
	public DbSet<EducationBoardPermission> EducationBoardPermissions { get; set; }
	public DbSet<EducationMediumStage> EducationBoardStages { get; set; }
	public DbSet<EducationMediumStagePermission> EducationBoardStagePermissions { get; set; }
	public DbSet<EducationMedium> EducationMediums { get; set; }
	public DbSet<EducationMediumPermission> EducationMediumPermissions { get; set; }
	public DbSet<Standard> Standards { get; set; }
	public DbSet<StandardPermission> StandardPermissions { get; set; }
	public DbSet<Division> Divisions { get; set; }
	public DbSet<DivisionPermission> DivisionPermissions { get; set; }

	public DbSet<ShiftManagement> ShiftManagements { get; set; }

	public DbSet<InstituteSetupExtraProperties> InstituteSetupExtraProperties { get; set; }

	#region Rooms Management
	public DbSet<Building> Buildings { get; set; }
	public DbSet<Floor> Floors { get; set; }
	public DbSet<RoomType> RoomTypes { get; set; }
	public DbSet<Room> Rooms { get; set; }
	#endregion

	#endregion

	#region Student
	public DbSet<Student> Students { get; set; }
	public DbSet<StudentAdditionalDetail> StudentAdditionalDetails { get; set; }
	public DbSet<StudentContactDetail> StudentContactDetails { get; set; }
	public DbSet<StudentFamilyDetail> StudentFamilyDetails { get; set; }
	public DbSet<StudentCommunicationDetail> StudentCommunicationDetails { get; set; }
	public DbSet<StudentMedicalRecord> StudentMedicalRecords { get; set; }
	public DbSet<StudentPreviousSchoolDetail> StudentPreviousSchoolDetails { get; set; }
	public DbSet<StudentBankDetail> StudentBankDetails { get; set; }
	public DbSet<StudentDocument> StudentDocuments { get; set; }
	public DbSet<StudentNote> StudentNotes { get; set; }

	#region Student Enrollment

	#region Settings
	public DbSet<StudentDocumentSetting> StudentDocumentSettings { get; set; }
	public DbSet<InquiryPreference> InquiryPreferences { get; set; }
	public DbSet<EntranceExamSlot> EntranceExamSlots { get; set; }
	public DbSet<DocumentType> DocumentTypes { get; set; }

	#endregion

	#endregion

	#region Student Record

	#endregion

	#region Attendance

	public DbSet<AttendanceDay> AttendanceDays { get; set; }
	public DbSet<AttendanceEntry> AttendanceEntries { get; set; }

	#region Settings

	public DbSet<ShikshaMitra.Students.Attendance.Settings.StudentLeaveType> StudentLeaveTypes { get; set; }

	#endregion
	
	public DbSet<ShikshaMitra.Students.Attendances.StudentLeave> StudentLeaves { get; set; }

	#endregion

	#region Dashboard
	#endregion

	#endregion

	#region HR

	#region Employee

	DbSet<Employee> Employees { get; set; }
	DbSet<DocumentSetting> DocumentSettings { get; set; }
	
	// Employee Payroll
	public DbSet<EmployeePayrollDetails> EmployeePayrollDetails { get; set; }
	public DbSet<EmployeePayrollComponent> EmployeePayrollComponents { get; set; }

	#region AttedenceAndLeaves

	DbSet<EmployeeAttendance> EmployeeAttendances { get; set; }
	public DbSet<EmployeeLeave> EmployeeLeaves { get; set; }
	DbSet<CustomAttendanceRule> CustomAttendanceRules { get; set; }
	DbSet<AutomatedNotificationsSettings> AutomatedNotificationsSettings { get; set; }
	DbSet<NotificationInfo> NotificationInfos { get; set; }

	// Employee Leave Management
	public DbSet<LeaveType> EmployeeLeaveTypes { get; set; }
	public DbSet<LeavePolicy> EmployeeLeavePolicies { get; set; }

	#endregion

	#endregion

	#region Payroll

	DbSet<PayElement> PayElements { get; set; }
	DbSet<PayrollStructure> PayrollStructures { get; set; }
	DbSet<ProvidentFund> ProvidentFunds { get; set; }
	DbSet<ProfessionalTax> ProfessionalTaxes { get; set; }
	public DbSet<EarningComponent> EarningComponents { get; set; }
	public DbSet<DeductionComponent> DeductionComponents { get; set; }

	#endregion

	#region  Events & Holidays

	DbSet<EventAndHoliday> EventAndHolidays { get; set; }

	#endregion


	#endregion

	#region Finance

	#region Setting

	#region GeneralSetting
	#region FeeHead
	DbSet<FeeHead> FeeHeads { get; set; }
	#endregion


	#region PaymentStatusOption
	DbSet<PaymentStatusOption> PaymentStatusOptions { get; set; }
	#endregion
	#endregion
	#endregion
	#endregion

	#region Academic

	#region Curriculum

	public DbSet<Subject> Subjects { get; set; }
	public DbSet<ElectiveGroup> ElectiveGroups { get; set; }
	public DbSet<StandardSubjectAssignment> StandardSubjectAssignments { get; set; }
	public DbSet<TeacherSubjectAllocation> TeacherSubjectAllocations { get; set; }
	#endregion

	#region ClassTeacher

	public DbSet<ClassTeacher> ClassTeachers { get; set; }
	#endregion

	#region Syllabus
	public DbSet<SubjectChapter> SubjectChapters { get; set; }
	#endregion

	#region HomeworkAssignment
	public DbSet<HomeworkAssignmentType> HomeworkAssignmentTypes { get; set; }
	public DbSet<HomeworkAssignmentEntity> HomeworkAssignments { get; set; }
	public DbSet<StudentSubmissionEntity> StudentSubmissions { get; set; }
	public DbSet<HomeworkType> HomeworkTypes { get; set; }
	public DbSet<AssignmentType> AssignmentTypes { get; set; }
	public DbSet<HomeworkDivisionSchedule> HomeworkDivisionSchedules { get; set; }
	public DbSet<AssignmentEntity> Assignments { get; set; }
	public DbSet<AssignmentDivisionSchedule> AssignmentDivisionSchedules { get; set; }
	#endregion

	#endregion

	#region Library
	#region GeneralSetting
	public DbSet<LibraryTiming> LibraryTimings { get; set; }
	public DbSet<BookCategory> BookCategories { get; set; } //here <BookCategory> is the entity class name and BookCategory is the table name
	#endregion
	#endregion

	// Apply BookCategory configuration

	// Optionally, apply all configurations in assembly:
	// builder.ApplyConfigurationsFromAssembly(typeof(ShikshaMitraDbContext).Assembly);

	// Add more entity configurations here as needed

	protected override void OnModelCreating(ModelBuilder builder)
	{
		base.OnModelCreating(builder);

		/* Include modules to your migration db context */

		builder.ConfigurePermissionManagement();
		builder.ConfigureSettingManagement();
		builder.ConfigureBackgroundJobs();
		builder.ConfigureAuditLogging();
		builder.ConfigureIdentity();
		builder.ConfigureOpenIddict();
		builder.ConfigureFeatureManagement();
		builder.ConfigureTenantManagement();
		
		// Ignore owned entities BEFORE ApplyConfigurationsFromAssembly
		// EmployeeDocument is configured as an owned entity in EmployeeConfiguration
		builder.Ignore<EmployeeDocument>();
		// AllocatedTeacher is configured as an owned entity in TeacherSubjectAllocationConfiguration
		builder.Ignore<AllocatedTeacher>();
		
		builder.ApplyConfigurationsFromAssembly(typeof(ShikshaMitraDbContext).Assembly);
		builder.ConfigureBlobStoring();
	}
}
