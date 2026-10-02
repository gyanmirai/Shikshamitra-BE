using AutoMapper;

namespace ShikshaMitra.Students.Attendance;

public class AttendanceScheduleMappingProfile : Profile
{
	public AttendanceScheduleMappingProfile() => CreateMap<StudentAttendanceSchedule, AttendanceScheduleDto>()
		.ForMember(x => x.WeekDays, o => o.MapFrom(x => x.WeekDays()));
}
