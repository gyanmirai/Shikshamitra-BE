using AutoMapper;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.SubjectAssignment;

namespace ShikshaMitra.Academic;

public class AcademicMapperProfile : Profile
{
	public AcademicMapperProfile()
	{
		#region Curriculum
		CreateMap<Subject, SubjectDto>();

		CreateMap<StandardSubjectAssignment, StandardSubjectAssignmentResponseDto>();
		#endregion
	}
}
