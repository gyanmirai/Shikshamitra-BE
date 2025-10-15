using AutoMapper;
using ShikshaMitra.Academic.Curriculum;
using ShikshaMitra.Academic.Curriculum.Setting;
using ShikshaMitra.Academic.Curriculum.SubjectAssignment;
using ShikshaMitra.Academic.Examination.Setting;
using ShikshaMitra.Academic.Homework;
using ShikshaMitra.Academic.HomeworkAssignment;
using ShikshaMitra.Academic.HomeworkAssignment.Setting;
using ShikshaMitra.Academic.Syllabus;
using System.Collections.Generic;
using System.Linq;
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
            .ForMember(dest => dest.Subjects, opt => opt.MapFrom(src =>
                src.Subjects.Select(s => new SubjectDto
                {
                    Id = s.SubjectId,
                    Name = s.Subject != null ? s.Subject.Name : string.Empty,
                    Color = s.Subject != null ? s.Subject.Color : string.Empty
                }).ToList()));
		CreateMap<ElectiveGroupDto, ElectiveGroup>()
            .ForMember(dest => dest.Subjects, opt => opt.Ignore());

		CreateMap<Chapter, ChapterrDto>()
	       .ForMember(dest => dest.Documents,
		    opt => opt.MapFrom(src => src.Documents));

		CreateMap<ChapterDocument, ChapterDocumentDto>()
			.ForMember(dest => dest.Base64Content,
				opt => opt.MapFrom(src => src.Base64Content));



		#endregion

		#region HomeworkAssignment
		CreateMap<HomeWorkType, HomeWorkTypeDto>();
		CreateMap<AssigmentType, AssigmentTypeDto>();

		CreateMap<HomeworkAssignmentType, HomeworkAssignmentTypeDto>();
		CreateMap<CreateUpdateHomeworkAssignmentTypeDto, HomeworkAssignmentType>();

		CreateMap<HomeworkAssignmentType, HomeworkAssignmentTypeDto>();
		CreateMap<CreateUpdateHomeworkAssignmentTypeDto, HomeworkAssignmentType>();

		CreateMap<HomeworkAssignmentEntity, HomeworkAssignmentDto>();
		CreateMap<HomeworkAssignmentDto, HomeworkAssignmentEntity>();

		CreateMap<HomeworkAssignmentEntity, CreateUpdateHomeworkAssignmentDto>();
		CreateMap<CreateUpdateHomeworkAssignmentDto, HomeworkAssignmentEntity>();
		#endregion

		#region Examinations
		CreateMap<ExamType, ExamTypeDto>();
		// Example using AutoMapper
		CreateMap<GradeScale, GradeScaleDto>()
            .ForMember(dest => dest.GradeRanges, opt => opt.MapFrom(src => src.GradeRanges));
		CreateMap<GradeRange, GradeRangeDto>();

		#endregion
	}
}
