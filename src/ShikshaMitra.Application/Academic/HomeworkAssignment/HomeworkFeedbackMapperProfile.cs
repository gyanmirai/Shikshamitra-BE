using AutoMapper;
using ShikshaMitra.Academic.HomeworkAssignment;

namespace ShikshaMitra.Academic.HomeworkAssignment;

public class HomeworkFeedbackMapperProfile : Profile
{
	public HomeworkFeedbackMapperProfile()
	{
		// StudentSubmissionEntity to HomeworkSubmissionDto
		CreateMap<StudentSubmissionEntity, HomeworkSubmissionDto>()
			.ForMember(dest => dest.Id, opt => opt.MapFrom(src => src.Id.ToString()))
			.ForMember(dest => dest.RollNumber, opt => opt.Ignore()) // Populated from Student
			.ForMember(dest => dest.GrNumber, opt => opt.Ignore()) // Populated from Student
			.ForMember(dest => dest.StudentName, opt => opt.Ignore()) // Populated from Student
			.ForMember(dest => dest.Homework, opt => opt.Ignore()) // Populated from Homework
			.ForMember(dest => dest.HomeworkType, opt => opt.Ignore()) // Populated from HomeworkType
			.ForMember(
				dest => dest.SubmissionType,
				opt => opt.Ignore()
			) // Populated from Homework
			.ForMember(
				dest => dest.SubmissionDate,
				opt => opt.MapFrom(src => src.SubmissionDate.HasValue ? src.SubmissionDate.Value.ToString("dd-MM-yyyy") : null)
			)
			.ForMember(dest => dest.DueDate, opt => opt.MapFrom(src => src.DueDate.ToString("dd-MM-yyyy")))
			.ForMember(dest => dest.Status, opt => opt.MapFrom(src => (int)src.Status));
	}
}
