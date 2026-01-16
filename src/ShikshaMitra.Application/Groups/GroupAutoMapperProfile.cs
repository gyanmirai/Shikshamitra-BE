using AutoMapper;
using ShikshaMitra.Groups.Dtos;

namespace ShikshaMitra.Groups;

public class GroupAutoMapperProfile : Profile
{
    public GroupAutoMapperProfile()
    {
        CreateMap<Group, GroupDto>();
        CreateMap<Group, GroupDetailsDto>();
        CreateMap<CreateGroupDto, Group>(MemberList.Source);
        CreateMap<UpdateGroupDto, Group>(MemberList.Source);
    }
}
