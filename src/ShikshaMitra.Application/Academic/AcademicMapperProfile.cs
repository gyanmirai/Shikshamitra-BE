using AutoMapper;
using ShikshaMitra.Academic.Curriculum;
using Volo.Abp.Application.Dtos;

namespace ShikshaMitra.Academic;

public class AcademicMapperProfile : Profile
{
	public AcademicMapperProfile()
	{
		#region Curriculum

		CreateMap<TempSubject, TempSubjectDto>()
			.ForMember(dest => dest.ClassId, opt => opt.MapFrom(src => src.StandardId));

		CreateMap<Subject, SubjectDto>();

		CreateMap<PagedResultDto<TempSubject>, PagedResultDto<TempSubjectDto>>()
			.ForMember(dest => dest.Items, opt => opt.MapFrom(src => src.Items));
		#endregion
	}
}
