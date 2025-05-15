using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Authors;
using ShikshaMitra.Books;
using ShikshaMitra.Finance.Setting.GeneralSetting.FeeHead;
using ShikshaMitra.Finance.Setting.GeneralSetting.PaymentStatusOption;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.NotificationHistories;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Settings;
using ShikshaMitra.HR.EventAndHolidays;
using ShikshaMitra.HR.Payroll.PayrollStructures;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.Library.Setting.General_Setting;
using ShikshaMitra.Library.Setting.GeneralSetting.BookCategory;
using ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.Attendance.settings;
using ShikshaMitra.Students.Attendance.Settings;
using ShikshaMitra.Students.StudentEnrollments;
using ShikshaMitra.Students.StudentEnrollments.EntranceExam;
using ShikshaMitra.Students.StudentEnrollments.FollowUps;
using ShikshaMitra.Students.StudentEnrollments.Registration;
using ShikshaMitra.Students.StudentEnrollments.Settings;
using ShikshaMitra.Students.StudentRecords;
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
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.BlobStoring.Database.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
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
public class ShikshaMitraDbContext(DbContextOptions<ShikshaMitraDbContext> options) :
	AbpDbContext<ShikshaMitraDbContext>(options),
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

	#region BookStore

	public DbSet<Book> Books { get; set; }
	public DbSet<Author> Authors { get; set; }

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

	#endregion

	#region Student

	#region Student Enrollment
	DbSet<StudentEnrollment> StudentEnrollments { get; set; }
	DbSet<FollowUpMessage> FollowUpMessages { get; set; }


	public DbSet<StudentDocument> StudentDocuments { get; set; }
	DbSet<EntranceExam> EntranceExams { get; set; }


	#region Settings
	DbSet<StudentDocumentSetting> StudentDocumentSettings { get; set; }
	DbSet<InquiryPreference> InquiryPreferences { get; set; }
	DbSet<EntranceExamSlot> EntranceExamSlots { get; set; }

	#endregion

	#endregion

	#region Student Record
	DbSet<Student> Students { get; set; }
	public DbSet<StudentGroup> StudentGroups { get; set; }
	public DbSet<StudentAttendance> StudentAttendances { get; set; }
	public DbSet<StudentLeave> StudentLeaves { get; set; }
	public DbSet<StudentGroupMember> StudentGroupMembers { get; set; }

	public DbSet<StudentCertificate> StudentCertificates { get; set; }

	#endregion

	#region Attendance

	#region Settings

	DbSet<AttendanceType> AttendanceTypes { get; set; }
	DbSet<StudentAttendanceRule> StudentAttendanceRules { get; set; }


	#endregion

	#endregion

	#endregion

	#region HR

	#region Employee

	DbSet<Employee> Employees { get; set; }
	DbSet<EmployeeDocument> EmployeeDocuments { get; set; }
	DbSet<DocumentSetting> DocumentSettings { get; set; }

	#region AttedenceAndLeaves

	DbSet<EmployeeAttendance> EmployeeAttendances { get; set; }

	public DbSet<EmployeeLeave> EmployeeLeaves { get; set; }

	DbSet<CustomAttendanceRule> CustomAttendanceRules { get; set; }

	DbSet<AutomatedNotificationsSettings> AutomatedNotificationsSettings { get; set; }

	DbSet<NotificationInfo> NotificationInfos { get; set; }

	DbSet<LeavePolicy> LeavePolicies { get; set; }


	#endregion

	#endregion

	#region Payroll

	DbSet<PayElement> PayElements { get; set; }
	DbSet<PayrollStructure> PayrollStructures { get; set; }
	DbSet<ProvidentFund> ProvidentFunds { get; set; }
	DbSet<ProfessionalTax> ProfessionalTaxes { get; set; }

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

	public DbSet<StandardSubjectAssignment> Subjects { get; set; }
	public DbSet<SubjectTeacher> SubjectTeachers { get; set; }
	#endregion

	#endregion

	#region Library
	#region GeneralSetting
	public DbSet<BookCategory> BookCategory { get; set; } //here <BookCategory> is the entity class name and BookCategory is the table name
	#endregion
	#endregion

	#region Library
	#region GeneralSetting
	public DbSet<LibraryTiming> LibraryTiming { get; set; }
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

		/* Configure your own tables/entities inside here */

		//builder.Entity<YourEntity>(b =>
		//{
		//    b.ToTable(ShikshaMitraConsts.DbTablePrefix + "YourEntities", ShikshaMitraConsts.DbSchema);
		//    b.ConfigureByConvention(); //auto configure for the base class props
		//    //...
		//})


		builder.Entity<Book>(b =>
		{
			b.ToTable(ShikshaMitraConsts.DbTablePrefix + "Books",
				ShikshaMitraConsts.DbSchema);
			b.ConfigureByConvention(); //auto configure for the base class props
			b.Property(x => x.Name).IsRequired().HasMaxLength(128);
		});

		builder.Entity<Author>(b =>
		{
			b.ToTable(ShikshaMitraConsts.DbTablePrefix + "Authors",
				ShikshaMitraConsts.DbSchema);

			b.ConfigureByConvention();

			b.Property(x => x.Name)
				.IsRequired()
				.HasMaxLength(AuthorConsts.MaxNameLength);

			b.HasIndex(x => x.Name);
		});

		//builder.Entity<Country>().HasNoKey();
		//builder.Entity<State>().HasNoKey();


		builder.ApplyConfigurationsFromAssembly(typeof(ShikshaMitraDbContext).Assembly);
		builder.ConfigureBlobStoring();
	}
}