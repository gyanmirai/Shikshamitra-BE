using AutoMapper;

namespace ShikshaMitra.Academic.HomeworkAssignment;

/// <summary>
/// AutoMapper profile for HomeworkType mappings
/// </summary>
public class HomeworkTypeMapperProfile : Profile
{
	public HomeworkTypeMapperProfile()
	{
		// Entity to DTO
		CreateMap<HomeworkType, HomeworkTypeDto>()
			.ForMember(
				dest => dest.HomeworkTypeName,
				opt => opt.MapFrom(src => src.Name)
			);

		// DTO to Entity (for creation)
		CreateMap<CreateHomeworkTypeDto, HomeworkType>()
			.ForMember(dest => dest.Name, opt => opt.MapFrom(src => src.HomeworkTypeName));
	}
}
