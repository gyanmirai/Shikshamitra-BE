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
		CreateMap<StudentParentDetails, StudentParentDetailsDto>().ReverseMap();
		CreateMap<CommunicationDetails, CommunicationDetailsDto>().ReverseMap();
		CreateMap<AdmissionDetails, AdmissionDetailsDto>().ReverseMap();
	}
}