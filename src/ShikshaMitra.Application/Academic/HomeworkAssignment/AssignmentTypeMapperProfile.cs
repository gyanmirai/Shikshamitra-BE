using AutoMapper;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// AutoMapper profile for AssignmentType mappings
/// </summary>
public class AssignmentTypeMapperProfile : Profile
{
	public AssignmentTypeMapperProfile()
	{
		// Entity to DTO
		CreateMap<AssignmentType, AssignmentTypeDto>()
			.ForMember(
				dest => dest.AssignmentTypeName,
				opt => opt.MapFrom(src => src.Name)
			);

		// DTO to Entity (for creation)
		CreateMap<CreateAssignmentTypeDto, AssignmentType>()
			.ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.AssignmentTypeName));
	}
}
