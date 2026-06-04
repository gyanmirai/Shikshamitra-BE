using Microsoft.AspNetCore.Identity;
using ShikshaMitra.Common;
using ShikshaMitra.Groups;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Calendar.HolidayTypes;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Settings;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Policies;
using ShikshaMitra.HR.Payroll.RunPayroll;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.HR.ProfessionalManagement;
using ShikshaMitra.TenantSettings;
using ShikshaMitra.TenantSettings.AcademicYears;
using ShikshaMitra.TenantSettings.EducationBoards;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace ShikshaMitra.Seeders;

/// <summary>
/// Seeds default HR module data for new tenants.
/// Uses constructor injection to preserve tenant context from parent seeder.
/// </summary>
public partial class HRSeederContributor : BaseSeederContributor
{
	public HRSeederContributor(
		ICurrentTenant currentTenant,
		IGuidGenerator guidGenerator,
		IUnitOfWork unitOfWork,
		IRepository<LeaveType, Guid> leaveTypeRepository,
		LeaveTypeManager leaveTypeManager,
		IRepository<EarningComponent, Guid> earningComponentRepository,
		EarningComponentManager earningComponentManager,
		IRepository<DeductionComponent, Guid> deductionComponentRepository,
		DeductionComponentManager deductionComponentManager,
		IProvidentFundRepository providentFundRepository,
		IProfessionalTaxRepository professionalTaxRepository,
		IRepository<WorkCalendarPolicy, Guid> workCalendarPolicyRepository,
		IRepository<WorkCalendarHoliday, Guid> workCalendarHolidayRepository,
		IRepository<PayrollPolicy, Guid> payrollPolicyRepository,
		IRepository<AttendancePolicy, Guid> attendancePolicyRepository,
		IRepository<HrPolicyAssignment, Guid> hrPolicyAssignmentRepository,
		IGroupRepository groupRepository,
		IRepository<Holiday, Guid> holidayRepository,
		IRepository<HolidayType, Guid> holidayTypeRepository,
		IRepository<LeavePolicy, Guid> leavePolicyRepository,
		LeavePolicyManager leavePolicyManager,
		ILeaveTypeRepository leaveTypeQueryRepository,
		IAcademicYearRepository academicYearRepository,
		IRepository<EducationMediumStage, Guid> educationMediumStageRepository,
			IRepository<ShiftManagement, Guid> shiftRepository,
			IEmployeeRepository employeeRepository,
			IRepository<EmployeeOrganizationAssignment, Guid> employeeOrganizationAssignmentRepository,
			IEmployeeFamilyMemberRepository familyMemberRepository,
			IDocumentSettingRepository documentSettingRepository,
			EmployeeManager employeeManager,
			EmployeeDocumentManager employeeDocumentManager,
			IRepository<IdentityRole, Guid> identityRoleRepository,
			IdentityUserManager userManager,
			IIdentityUserRepository userRepository,
			ILookupNormalizer lookupNormalizer,
			IEmployeeAttendanceRepository employeeAttendanceRepository,
			IEmployeeLeaveRepository employeeLeaveRepository,
			EmployeeLeaveManager employeeLeaveManager,
			IEmployeePayrollDetailsRepository employeePayrollDetailsRepository,
			IEmployeePayrollComponentRepository employeePayrollComponentRepository,
			IRepository<PayrollRunItemSnapshot, Guid> payrollRunItemSnapshotRepository,
			IRepository<PerformanceTemplate, Guid> performanceTemplateRepository,
			IRepository<PerformanceTemplateSection, Guid> performanceTemplateSectionRepository,
			IRepository<PerformanceTemplateCriterion, Guid> performanceTemplateCriterionRepository,
			IRepository<ProfessionalReviewCycle, Guid> performanceCycleRepository,
			IRepository<PerformanceReviewAssignment, Guid> performanceAssignmentRepository,
			IRepository<PerformanceReviewStage, Guid> performanceStageRepository,
			IRepository<PerformanceReviewResponse, Guid> performanceResponseRepository,
			IRepository<ClassroomObservation, Guid> classroomObservationRepository,
			IRepository<PerformanceEvidence, Guid> performanceEvidenceRepository,
			IRepository<PerformanceFeedback, Guid> performanceFeedbackRepository,
			IRepository<PerformanceOutcome, Guid> performanceOutcomeRepository,
			IRepository<DevelopmentPlan, Guid> developmentPlanRepository,
			IRepository<PerformanceAuditEvent, Guid> performanceAuditRepository,
			IConfiguration configuration,
			ILogger<HRSeederContributor> logger,
			IDataFilter dataFilter,
			IClock clock)
			: base(currentTenant, guidGenerator, unitOfWork)
	{
		_leaveTypeRepository = leaveTypeRepository;
		_leaveTypeManager = leaveTypeManager;
		_earningComponentRepository = earningComponentRepository;
		_earningComponentManager = earningComponentManager;
		_deductionComponentRepository = deductionComponentRepository;
		_deductionComponentManager = deductionComponentManager;
		_providentFundRepository = providentFundRepository;
		_professionalTaxRepository = professionalTaxRepository;
		_workCalendarPolicyRepository = workCalendarPolicyRepository;
		_workCalendarHolidayRepository = workCalendarHolidayRepository;
		_payrollPolicyRepository = payrollPolicyRepository;
		_attendancePolicyRepository = attendancePolicyRepository;
		_hrPolicyAssignmentRepository = hrPolicyAssignmentRepository;
		_groupRepository = groupRepository;
		_holidayRepository = holidayRepository;
		_holidayTypeRepository = holidayTypeRepository;
		_leavePolicyRepository = leavePolicyRepository;
		_leavePolicyManager = leavePolicyManager;
		_leaveTypeQueryRepository = leaveTypeQueryRepository;
		_academicYearRepository = academicYearRepository;
		_educationMediumStageRepository = educationMediumStageRepository;
		_shiftRepository = shiftRepository;
		_employeeRepository = employeeRepository;
		_employeeOrganizationAssignmentRepository = employeeOrganizationAssignmentRepository;
		_familyMemberRepository = familyMemberRepository;
		_documentSettingRepository = documentSettingRepository;
		_employeeManager = employeeManager;
		_employeeDocumentManager = employeeDocumentManager;
		_identityRoleRepository = identityRoleRepository;
		_userManager = userManager;
		_userRepository = userRepository;
		_lookupNormalizer = lookupNormalizer;
		_employeeAttendanceRepository = employeeAttendanceRepository;
		_employeeLeaveRepository = employeeLeaveRepository;
		_employeeLeaveManager = employeeLeaveManager;
		_employeePayrollDetailsRepository = employeePayrollDetailsRepository;
		_employeePayrollComponentRepository = employeePayrollComponentRepository;
		_payrollRunItemSnapshotRepository = payrollRunItemSnapshotRepository;
		_performanceTemplateRepository = performanceTemplateRepository;
		_performanceTemplateSectionRepository = performanceTemplateSectionRepository;
		_performanceTemplateCriterionRepository = performanceTemplateCriterionRepository;
		_performanceCycleRepository = performanceCycleRepository;
		_performanceAssignmentRepository = performanceAssignmentRepository;
		_performanceStageRepository = performanceStageRepository;
		_performanceResponseRepository = performanceResponseRepository;
		_classroomObservationRepository = classroomObservationRepository;
		_performanceEvidenceRepository = performanceEvidenceRepository;
		_performanceFeedbackRepository = performanceFeedbackRepository;
		_performanceOutcomeRepository = performanceOutcomeRepository;
		_developmentPlanRepository = developmentPlanRepository;
		_performanceAuditRepository = performanceAuditRepository;
		_configuration = configuration;
		_logger = logger;
		_dataFilter = dataFilter;
		_clock = clock;
	}

}
