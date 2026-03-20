using AutoMapper;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.NotificationHistories;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Employees.Settings;
using ShikshaMitra.HR.EventAndHolidays;
using ShikshaMitra.HR.Payroll.PayrollStructures;
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.SalaryStructure;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.Students;

namespace ShikshaMitra.HR;

public class HRMapperProfile : Profile
{
	public HRMapperProfile()
	{
		# region Employees

		// Mapping for Employee
		CreateMap<Employee, EmployeeDto>();
		CreateMap<CreateUpdateEmployeeDto, Employee>();

		CreateMap<WorkExperience, WorkExperienceDto>();
		CreateMap<CreateUpdateWorkExperienceDto, WorkExperience>();

		CreateMap<ContactDetails, ContactDetailsDto>().ReverseMap();
		CreateMap<BankDetails, BankDetailsDto>().ReverseMap();

		// Employee Payroll Component mappings for Salary Structure API
		CreateMap<EmployeePayrollComponent, SalaryComponentDto>();
		CreateMap<SalaryComponentDto, EmployeePayrollComponent>();

		// Also map to the legacy PayrollComponentDto for backward compatibility
		CreateMap<EmployeePayrollComponent, Employees.Payroll.PayrollComponentDto>();
		CreateMap<Employees.Payroll.PayrollComponentDto, EmployeePayrollComponent>();

		#region settings

		CreateMap<DocumentSetting, DocumentSettingDto>();
		CreateMap<CreateUpdateDocumentSettingDto, DocumentSetting>();

		#endregion

		#endregion

		#region Attendeance and Leaves

		#region Employee Attendance

		CreateMap<EmployeeAttendance, EmployeeAttendanceDto>()
			.ForMember(x => x.EmployeeName,
				opt => opt.MapFrom(x => x.Employee.FirstName + " " + x.Employee.LastName));
		CreateMap<CreateUpdateEmployeeAttendanceDto, EmployeeAttendance>();

		#endregion

		# region Employee Settings

		CreateMap<AutomatedNotificationsSettings, AutomatedNotificationsSettingsDto>().ReverseMap();

		CreateMap<NotificationInfo, NotificationInfoDto>();

		CreateMap<CustomAttendanceRule, CustomAttendanceRuleDto>();
		CreateMap<CreateUpdateCustomAttendanceRuleDto, CustomAttendanceRule>();

		#endregion

		# region Employee Leave

		CreateMap<EmployeeLeave, EmployeeLeaveDto>();
		CreateMap<CreateUpdateEmployeeLeaveDto, EmployeeLeave>();
		CreateMap<LeavePolicy, LeavePolicyDto>();
		CreateMap<CreateUpdateLeavePolicyDto, LeavePolicy>();

		# endregion

		# endregion

		#region Events & Holidays

		CreateMap<EventAndHoliday, EventAndHolidayDto>();
		CreateMap<CreateUpdateEventAndHolidayDto, EventAndHoliday>();

		#endregion

		#region Payroll

		#region Payroll Structure

		CreateMap<PayrollStructure, PayrollStructureDto>();
		CreateMap<CreateUpdatePayrollStructureDto, PayrollStructure>();

		#endregion

		#region Salary Slips

		CreateMap<SalarySlip, SalarySlipDto>();
		CreateMap<SalarySlipLineItem, SalarySlipLineItemDto>();

		#endregion

		#region Settings

		CreateMap<PayElement, PayElementDto>();
		CreateMap<PayElement.DependentProperties, DependentPropertiesDto>();
		CreateMap<CreateUpdatePayElementDto, PayElement>();
		CreateMap<DependentPropertiesDto, PayElement.DependentProperties>();
		CreateMap<ProvidentFund, ProvidentFundDto>();
		CreateMap<CreateUpdateProvidentFundDto, ProvidentFund>();
		CreateMap<ProfessionalTax, ProfessionalTaxDto>();
		CreateMap<CreateUpdateProfessionalTaxDto, ProfessionalTax>();

		#endregion

		#endregion
	}
}