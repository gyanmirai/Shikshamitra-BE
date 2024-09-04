using AutoMapper;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.Students;

namespace ShikshaMitra.HR;

public class HRMapperProfile :  Profile
{
	public HRMapperProfile()
	{
		// Mapping for Employee
		CreateMap<Employee, EmployeeDto>();
		CreateMap<CreateUpdateEmployeeDto, Employee>();

		CreateMap<WorkExperience, WorkExperienceDto>();
		CreateMap<CreateUpdateWorkExperienceDto, WorkExperience>();

		CreateMap<ContactDetails, ContactDetailsDto>().ReverseMap();
		CreateMap<BankDetails, BankDetailsDto>().ReverseMap();
	}
}