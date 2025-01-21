using AutoMapper;
using ShikshaMitra.Security.OrganizationUnits.Dtos;
using Volo.Abp.Identity;

namespace ShikshaMitra.Security;

public class SecurityMapperProfile : Profile
{
	public SecurityMapperProfile()
	{
		CreateMap<OrganizationUnit, OrganizationUnitDto>();
		CreateMap<IdentityUser, GetOrganizationUnitUserOutput>();
		CreateMap<IdentityUser, GetUnAddUserOutput>();
		CreateMap<IdentityRole, GetOrganizationUnitRoleOutput>();
		CreateMap<IdentityRole, GetUnAddRoleOutput>();
	}
}
