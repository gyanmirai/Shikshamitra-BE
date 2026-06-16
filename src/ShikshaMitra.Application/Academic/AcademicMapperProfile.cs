using System.Linq;
using AutoMapper;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Settings;
using ShikshaMitra.Academic.Curriculum.SubjectAssignment;
using ShikshaMitra.Academic.Examination.Settings;
using ShikshaMitra.Academic.Syllabus;

namespace ShikshaMitra.Academic;

public class AcademicMapperProfile : Profile
{
	public AcademicMapperProfile()
	{
		#region Curriculum
		CreateMap<Subject, SubjectDto>();

		CreateMap<AssignedSubject, StandardSubjectAssignmentResponseDto>();

		CreateMap<AssignedSubjectChapter, ChapterDto>()
			.ForMember(
				dest => dest.Documents,
				opt => opt.MapFrom(src => src.Documents.Select(d => d.BlobName))
			);

		CreateMap<ElectiveGroup, ElectiveGroupDto>()
			.ForMember(
				dest => dest.EducationMediumId,
				opt => opt.MapFrom(src => src.AcademicMediumId)
			)
			.ForMember(
				dest => dest.Subjects,
				opt => opt.MapFrom(src => src.Subjects.Select(s => s.Subject))
			);

		#region Examination
		CreateMap<GradeRange, GradeRangeDto>();
		CreateMap<GradingScale, GradingScaleDto>();

		// Exam Type mappings
		CreateMap<ExamType, ExamTypeDto>()
			.ForMember(dest => dest.ExamType, opt => opt.MapFrom(src => src.Name))
			.ForMember(dest => dest.CreatedAt, opt => opt.MapFrom(src => src.CreationTime));
		#endregion
		#endregion
	}
}
