using AutoMapper;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.AttendanceAndLeaves.NotificationHistories;
using ShikshaMitra.HR.AttendanceAndLeaves.Settings;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Payroll.Settings;
using ShikshaMitra.Students;

namespace ShikshaMitra.HR;

public class HRMapperProfile : Profile
{
	public HRMapperProfile()
	{
		# region employees

		// Mapping for Employee
		CreateMap<Employee, EmployeeDto>();
		CreateMap<CreateUpdateEmployeeDto, Employee>();

		CreateMap<WorkExperience, WorkExperienceDto>();
		CreateMap<CreateUpdateWorkExperienceDto, WorkExperience>();

		CreateMap<ContactDetails, ContactDetailsDto>().ReverseMap();
		CreateMap<BankDetails, BankDetailsDto>().ReverseMap();

		# endregion

		# region Attendeance and Leaves

		# region Employee Attendance

		CreateMap<EmployeeAttendance, EmployeeAttendanceDto>()
			.ForMember(x => x.EmployeeName,
				opt => opt.MapFrom(x => x.Employee.FirstName + " " + x.Employee.LastName));
		CreateMap<CreateUpdateEmployeeAttendanceDto, EmployeeAttendance>();

		#endregion

		# region Employee Settings

		CreateMap<AutomatedNotificationsSettings, AutomatedNotificationsSettingsDto>().ReverseMap();

		CreateMap<CustomAttendanceRule, CustomAttendanceRuleDto>();
		CreateMap<CreateUpdateCustomAttendanceRuleDto, CustomAttendanceRule>();

		#endregion

		# region Employee Leave

		CreateMap<EmployeeLeave, EmployeeLeaveDto>();
		CreateMap<CreateUpdateEmployeeLeaveDto, EmployeeLeave>();

		# endregion

		# endregion

		#region Payroll

		#region Settings

		CreateMap<PayElement, PayElementDto>();
		CreateMap<PayElement.DependentProperties, DependentPropertiesDto>();
		CreateMap<CreateUpdatePayElementDto, PayElement>();
		CreateMap<DependentPropertiesDto, PayElement.DependentProperties>();

		#endregion

		#endregion
	}
}