using AutoMapper;
using ShikshaMitra.Security.UserScope;

namespace ShikshaMitra.Application.Security.UserScope;

/// <summary>
/// AutoMapper profile for UserScope mappings.
/// </summary>
public class UserScopeApplicationAutoMapperProfile : Profile
{
    public UserScopeApplicationAutoMapperProfile()
    {
        // UserScopeAssignment -> UserScopeAssignmentDto
        CreateMap<ShikshaMitra.Security.UserScope.UserScopeAssignment, UserScopeAssignmentDto>()
            .ForMember(dest => dest.ScopeLevel, opt => opt.MapFrom(src => src.GetScopeLevel()));

        // UserScopeContext -> UserScopeContextDto
        CreateMap<ShikshaMitra.Security.UserScope.UserScopeContext, UserScopeContextDto>();

        // CreateUserScopeAssignmentDto -> No direct mapping (handled in service)
    }
}
