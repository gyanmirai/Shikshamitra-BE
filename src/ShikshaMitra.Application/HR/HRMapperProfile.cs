using AutoMapper;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.NotificationHistories;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.Application.Contracts.HR.Calendar.Events;
using ShikshaMitra.Application.Contracts.HR.Calendar.Holidays;
using ShikshaMitra.HR.Calendar.Events;
using ShikshaMitra.HR.Calendar.EventTypes;
using ShikshaMitra.HR.Calendar.Holidays;
using ShikshaMitra.HR.Calendar.HolidayTypes;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.AdditionalDetails;
using ShikshaMitra.HR.Employees.Payroll;
using ShikshaMitra.HR.Employees.Settings;
using ShikshaMitra.HR.EventAndHolidays;
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

		# region Employee Settings

		CreateMap<AutomatedNotificationsSettings, AutomatedNotificationsSettingsDto>().ReverseMap();

		CreateMap<NotificationInfo, NotificationInfoDto>();

		CreateMap<CustomAttendanceRule, CustomAttendanceRuleDto>();
		CreateMap<CreateUpdateCustomAttendanceRuleDto, CustomAttendanceRule>();

		#endregion

		# region Leave Policy

		CreateMap<LeavePolicy, LeavePolicyDto>();
		CreateMap<CreateUpdateLeavePolicyDto, LeavePolicy>();

		# endregion

		# endregion

		#region Events & Holidays (legacy)

		CreateMap<EventAndHoliday, EventAndHolidayDto>();
		CreateMap<CreateUpdateEventAndHolidayDto, EventAndHoliday>();

		#endregion

		#region Calendar — Events, EventTypes, Holidays, HolidayTypes

		CreateMap<Event, Application.Contracts.HR.Calendar.Events.EventDto>();
		CreateMap<EventType, Calendar.EventTypes.EventTypeDto>();
		CreateMap<Holiday, Application.Contracts.HR.Calendar.Holidays.HolidayDto>();
		CreateMap<HolidayType, ShikshaMitra.HR.Calendar.HolidayTypeDto>();

		#endregion

		#region Employee Additional Details

		CreateMap<EmployeeAdditionalDetails, Employees.AdditionalDetails.EmployeeAdditionalDetailsDto>();

		#endregion

		#region Payroll

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