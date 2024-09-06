using AutoMapper;
using ShikshaMitra.HR.AttendanceAndLeaves;
using ShikshaMitra.HR.Employees;
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

		CreateMap<EmployeeLeave, EmployeeLeaveDto>();
		CreateMap<EmployeeCreateUpdateLeaveDto, EmployeeLeave>();

		# endregion
	}
}