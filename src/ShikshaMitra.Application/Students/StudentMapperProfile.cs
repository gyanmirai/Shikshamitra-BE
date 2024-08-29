using AutoMapper;

namespace ShikshaMitra.Students;

public class StudentMapperProfile : Profile
{
	public StudentMapperProfile()
	{
		// Mapping for Student
		CreateMap<Student, StudentDto>();
		CreateMap<CreateUpdateStudentDto, Student>();
		
		CreateMap<Address, AddressDto>().ReverseMap();
		CreateMap<ParentDetails, ParentDetailsDto>().ReverseMap();
	}
}