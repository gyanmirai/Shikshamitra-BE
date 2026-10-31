using System;
using System.Linq;
using System.Text.Json;
using AutoMapper;
using ShikshaMitra.Academic.HomeworkAssignment;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// AutoMapper profile for Assignment mappings
/// </summary>
public class AssignmentMapperProfile : Profile
{
	public AssignmentMapperProfile()
	{
		// Entity to DTO
		CreateMap<AssignmentEntity, AssignmentDto>()
			.ForMember(dest => dest.ClassId, opt => opt.MapFrom(src => src.StandardId))
			.ForMember(dest => dest.ClassName, opt => opt.Ignore()) // Will be populated in service
			.ForMember(dest => dest.SubjectName, opt => opt.Ignore()) // Will be populated in service
			.ForMember(dest => dest.AssignmentTypeName, opt => opt.Ignore()) // Will be populated in service
			.ForMember(
				dest => dest.SubmissionTypeName,
				opt => opt.MapFrom(src => src.SubmissionType.ToString())
			)
			.ForMember(dest => dest.Chapters, opt => opt.Ignore()) // Will be populated in service
			.ForMember(dest => dest.Attachments, opt => opt.Ignore()) // Will be populated in service
			.ForMember(
				dest => dest.Divisions,
				opt => opt.MapFrom(src => src.DivisionSchedules)
			)
			.ForMember(dest => dest.CreatedBy, opt => opt.Ignore()) // Will be populated in service
			.ForMember(
				dest => dest.PublishedAt,
				opt =>
					opt.MapFrom(
						src =>
							src.DivisionSchedules.Any(d => d.PublishedAt.HasValue)
								? src.DivisionSchedules
									.Where(d => d.PublishedAt.HasValue)
									.Min(d => d.PublishedAt)
								: null
					)
			)
			.ForMember(
				dest => dest.Status,
				opt =>
					opt.MapFrom(
						src =>
							src.AllDivisionsPublished
								? "Published"
								: src.HasPublishedDivisions
									? "Partially Published"
									: "Draft"
					)
			);

		// AssignmentDivisionSchedule to DTO
		CreateMap<AssignmentDivisionSchedule, AssignmentDivisionScheduleDto>()
			.ForMember(dest => dest.DivisionName, opt => opt.Ignore()); // Will be populated in service

		// Create DTO to Entity
		CreateMap<CreateAssignmentDto, AssignmentEntity>()
			.ForMember(dest => dest.Id, opt => opt.Ignore()) // Set in service
			.ForMember(dest => dest.StandardId, opt => opt.MapFrom(src => src.ClassId))
			.ForMember(
				dest => dest.ChapterIdsJson,
				opt =>
					opt.MapFrom(
						src =>
							src.Chapters != null && src.Chapters.Any()
								? JsonSerializer.Serialize(src.Chapters, (JsonSerializerOptions?)null)
								: null
					)
			)
			.ForMember(
				dest => dest.AttachmentsJson,
				opt =>
					opt.MapFrom(
						src =>
							src.Attachments != null && src.Attachments.Any()
								? JsonSerializer.Serialize(src.Attachments, (JsonSerializerOptions?)null)
								: null
					)
			)
			.ForMember(dest => dest.SubmissionType, opt => opt.MapFrom(src => (SubmissionType)src.SubmissionType))
			.ForMember(dest => dest.DivisionSchedules, opt => opt.Ignore()) // Set in service
			.ForMember(dest => dest.AssignmentType, opt => opt.Ignore())
			.ForMember(dest => dest.TenantId, opt => opt.Ignore())
			.ForMember(dest => dest.AcademicYearId, opt => opt.Ignore())
			.ForMember(dest => dest.EducationBoardId, opt => opt.Ignore())
			.ForMember(dest => dest.EducationMediumId, opt => opt.Ignore())
			.ForMember(dest => dest.EducationMediumStageId, opt => opt.Ignore())
			.ForMember(dest => dest.DivisionId, opt => opt.Ignore());

		// Update DTO to Entity
		CreateMap<UpdateAssignmentDto, AssignmentEntity>()
			.ForMember(dest => dest.Id, opt => opt.Ignore())
			.ForMember(dest => dest.StandardId, opt => opt.MapFrom(src => src.ClassId))
			.ForMember(
				dest => dest.ChapterIdsJson,
				opt =>
					opt.MapFrom(
						src =>
							src.Chapters != null && src.Chapters.Any()
								? JsonSerializer.Serialize(src.Chapters, (JsonSerializerOptions?)null)
								: null
					)
			)
			.ForMember(
				dest => dest.AttachmentsJson,
				opt =>
					opt.MapFrom(
						src =>
							src.Attachments != null && src.Attachments.Any()
								? JsonSerializer.Serialize(src.Attachments,(JsonSerializerOptions?)null)
								: null
					)
			)
			.ForMember(dest => dest.SubmissionType, opt => opt.MapFrom(src => (SubmissionType)src.SubmissionType))
			.ForMember(dest => dest.DivisionSchedules, opt => opt.Ignore())
			.ForMember(dest => dest.AssignmentType, opt => opt.Ignore())
			.ForMember(dest => dest.TenantId, opt => opt.Ignore())
			.ForMember(dest => dest.AcademicYearId, opt => opt.Ignore())
			.ForMember(dest => dest.EducationBoardId, opt => opt.Ignore())
			.ForMember(dest => dest.EducationMediumId, opt => opt.Ignore())
			.ForMember(dest => dest.EducationMediumStageId, opt => opt.Ignore())
			.ForMember(dest => dest.DivisionId, opt => opt.Ignore());
	}
}
