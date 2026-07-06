using AutoMapper;
using ShikshaMitra.Academic.Timetable.RoomAvailability;

namespace ShikshaMitra.Academic.Timetable;

public class TimetableAutoMapperProfile : Profile
{
    public TimetableAutoMapperProfile()
    {
        CreateMap<RoomReservation, RoomReservationDto>();
        CreateMap<ConflictCheckResult, ConflictCheckResultDto>();
        CreateMap<ValidationPass, ValidationPassDto>();
        CreateMap<ConflictItem, ConflictItemDto>();
        CreateMap<SuggestedRoom, SuggestedRoomDto>();
        CreateMap<RoomMaintenanceBlock, RoomMaintenanceBlockDto>();
        CreateMap<RoomAuditLog, RoomAuditLogDto>()
            .ForMember(dest => dest.User, opt => opt.MapFrom(src => src.UserName))
            .ForMember(dest => dest.DateTime, opt => opt.MapFrom(src => src.Timestamp))
            .ForMember(dest => dest.RoomName, opt => opt.Ignore());
    }
}
