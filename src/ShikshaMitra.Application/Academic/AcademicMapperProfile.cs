using System.Linq;
using AutoMapper;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Curriculum.SubjectAssignment;
using ShikshaMitra.Academic.Syllabus;

namespace ShikshaMitra.Academic;

public class AcademicMapperProfile : Profile
{
	public AcademicMapperProfile()
	{
		#region Curriculum
		CreateMap<Subject, SubjectDto>();

		CreateMap<AssignedSubject, StandardSubjectAssignmentResponseDto>();

		CreateMap<AssignedSubjectChapter, ChapterDto>();

		CreateMap<ElectiveGroup, ElectiveGroupDto>()
			.ForMember(
				dest => dest.EducationMediumId,
				opt => opt.MapFrom(src => src.AcademicMediumId)
			)
			.ForMember(
				dest => dest.Subjects,
				opt => opt.MapFrom(src => src.Subjects.Select(s => s.Subject))
			);
		#endregion
	}
}
