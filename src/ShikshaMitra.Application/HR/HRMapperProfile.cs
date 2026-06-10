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
using ShikshaMitra.HR.Payroll.SalarySlips;
using ShikshaMitra.HR.Payroll.SalaryStructure;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.Students;
using System.Linq;

namespace ShikshaMitra.HR;

public class HRMapperProfile : Profile
{
	public HRMapperProfile()
	{
		CreateMap<Employee, EmployeeDto>()
			.ForMember(dest => dest.OrganizationIds,
				opt => opt.Ignore())
			.ForMember(dest => dest.Organizations,
				opt => opt.Ignore())
			.ForMember(dest => dest.OrganizationNames,
				opt => opt.Ignore());
		CreateMap<CreateUpdateEmployeeDto, Employee>();

		CreateMap<WorkExperience, WorkExperienceDto>();
		CreateMap<CreateUpdateWorkExperienceDto, WorkExperience>();

		CreateMap<ContactDetails, ContactDetailsDto>().ReverseMap();
		CreateMap<BankDetails, BankDetailsDto>().ReverseMap();

		CreateMap<EmployeePayrollComponent, SalaryComponentDto>();
		CreateMap<SalaryComponentDto, EmployeePayrollComponent>();

		CreateMap<EmployeePayrollComponent, Employees.Payroll.PayrollComponentDto>();
		CreateMap<Employees.Payroll.PayrollComponentDto, EmployeePayrollComponent>();

		CreateMap<DocumentSetting, DocumentSettingDto>();
		CreateMap<CreateUpdateDocumentSettingDto, DocumentSetting>();

		CreateMap<AutomatedNotificationsSettings, AutomatedNotificationsSettingsDto>().ReverseMap();
		CreateMap<NotificationInfo, NotificationInfoDto>();

		CreateMap<LeavePolicy, LeavePolicyDto>()
			.ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
			.ForMember(dest => dest.RuleCount, opt => opt.MapFrom(src => src.Rules.Count));
		CreateMap<CreateUpdateLeavePolicyDto, LeavePolicy>();

		CreateMap<Event, Application.Contracts.HR.Calendar.Events.EventDto>();
		CreateMap<EventType, Calendar.EventTypes.EventTypeDto>();
		CreateMap<Holiday, Application.Contracts.HR.Calendar.Holidays.HolidayDto>();
		CreateMap<HolidayType, ShikshaMitra.HR.Calendar.HolidayTypeDto>();

		CreateMap<EmployeeAdditionalDetails, Employees.AdditionalDetails.EmployeeAdditionalDetailsDto>()
			.ForMember(dest => dest.CreatedAt,
				opt => opt.MapFrom(src => src.CreationTime))
			.ForMember(dest => dest.LastModifiedAt,
				opt => opt.MapFrom(src => src.LastModificationTime ?? src.CreationTime));

		CreateMap<SalarySlip, SalarySlipDto>();
		CreateMap<SalarySlipLineItem, SalarySlipLineItemDto>();

		CreateMap<PayElement, PayElementDto>();
		CreateMap<PayElement.DependentProperties, DependentPropertiesDto>();
		CreateMap<CreateUpdatePayElementDto, PayElement>();
		CreateMap<DependentPropertiesDto, PayElement.DependentProperties>();
		CreateMap<ProvidentFund, ProvidentFundDto>();
		CreateMap<CreateUpdateProvidentFundDto, ProvidentFund>();
		CreateMap<ProfessionalTax, ProfessionalTaxDto>();
		CreateMap<CreateUpdateProfessionalTaxDto, ProfessionalTax>();
	}
}
