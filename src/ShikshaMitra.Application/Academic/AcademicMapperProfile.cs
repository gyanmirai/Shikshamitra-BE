using AutoMapper;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.SubjectAssignment;
using ShikshaMitra.Academic.Syllabus;

namespace ShikshaMitra.Academic;

public class AcademicMapperProfile : Profile
{
	public AcademicMapperProfile()
	{
		#region Curriculum
		CreateMap<Subject, SubjectDto>();

		CreateMap<StandardSubjectAssignment, StandardSubjectAssignmentResponseDto>();

		CreateMap<SubjectChapter, ChapterDto>();
		#endregion
	}
}
