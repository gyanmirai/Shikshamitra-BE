using AutoMapper;
using ShikshaMitra.Academic.Curriculum;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic;

public class AcademicMapperProfile : Profile
{
	public AcademicMapperProfile()
	{
		#region Curriculum

		CreateMap<Subject, SubjectDto>()
			.ForMember(dest => dest.ClassId, opt => opt.MapFrom(src => src.StandardId));

		CreateMap<PagedResultDto<Subject>, PagedResultDto<SubjectDto>>()
			.ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items));
		#endregion
	}
}
