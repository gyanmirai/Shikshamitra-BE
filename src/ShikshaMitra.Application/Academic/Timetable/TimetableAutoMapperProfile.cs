using AutoMapper;
using ShikshaMitra.Academic.Timetable.RoomAvailability;
using ShikshaMitra.Academic.Timetable.Substitutions;

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
        CreateMap<TeacherAvailability, TeacherAvailabilityDto>()
            .ForMember(dest => dest.StartTime, opt => opt.MapFrom(src => src.StartTime.ToString("HH:mm")))
            .ForMember(dest => dest.EndTime, opt => opt.MapFrom(src => src.EndTime.ToString("HH:mm")))
            .ForMember(dest => dest.AvailabilityStatus, opt => opt.MapFrom(src => src.AvailabilityStatus.ToString()))
            .ForMember(dest => dest.SourceType, opt => opt.MapFrom(src => src.SourceType.ToString()));
    }
}
