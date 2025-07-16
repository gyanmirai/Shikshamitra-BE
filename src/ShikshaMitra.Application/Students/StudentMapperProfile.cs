using AutoMapper;
using ShikshaMitra.Students.StudentEnrollments;

namespace ShikshaMitra.Students;

public class StudentMapperProfile : Profile
{
	public StudentMapperProfile()
	{
		CreateMap<Student, StudentInquiryDto>();
	}
}