using AutoMapper;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.SubjectAssignment;
using ShikshaMitra.Academic.Homework;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Academic.Syllabus;
using System.Linq;

namespace ShikshaMitra.Academic;

public class AcademicMapperProfile : Profile
{
	public AcademicMapperProfile()
	{
		#region Curriculum
		CreateMap<Subject, SubjectDto>();

		CreateMap<StandardSubjectAssignment, StandardSubjectAssignmentResponseDto>();

		CreateMap<SubjectChapter, ChapterDto>();
		CreateMap<ElectiveGroup, ElectiveGroupDto>()
			.ForMember(
				dest => dest.Subjects,
				opt => opt.MapFrom(src => src.Subjects.Select(s => s.Subject))
			);
		#endregion

		#region HomeworkAssignment
		CreateMap<HomeworkAssignmentType, HomeworkAssignmentTypeDto>();
		CreateMap<CreateUpdateHomeworkAssignmentTypeDto, HomeworkAssignmentType>();

		CreateMap<HomeworkAssignmentType, HomeworkAssignmentTypeDto>();
		CreateMap<CreateUpdateHomeworkAssignmentTypeDto, HomeworkAssignmentType>();

		CreateMap<HomeworkAssignmentEntity, HomeworkAssignmentDto>();
		CreateMap<HomeworkAssignmentDto, HomeworkAssignmentEntity>();

		CreateMap<HomeworkAssignmentEntity, CreateUpdateHomeworkAssignmentDto>();
		CreateMap<CreateUpdateHomeworkAssignmentDto, HomeworkAssignmentEntity>();
		#endregion
	}
}
