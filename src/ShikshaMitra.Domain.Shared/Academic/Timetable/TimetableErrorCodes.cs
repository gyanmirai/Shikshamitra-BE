namespace ShikshaMitra.Academic.Timetable;

public static class TimetableErrorCodes
{
    public const string WeeklyLectureSlotAlreadyExists = "Academic:Timetable:010001";
    public const string WeeklyLectureSlotNotFound = "Academic:Timetable:010002";
    public const string LectureSlotConfigAlreadyExists = "Academic:Timetable:020001";
    public const string LectureSlotConfigNotFound = "Academic:Timetable:020002";
    public const string LectureSlotAlreadyAssigned = "Academic:Timetable:020003";
    public const string LectureSlotConfigHasTimetable = "Academic:Timetable:020004";
    public const string TimetableNotFound = "Academic:Timetable:030001";
    public const string TimetableAlreadyExists = "Academic:Timetable:030002";
    public const string TeacherConflict = "Academic:Timetable:030003";
    public const string RoomConflict = "Academic:Timetable:030004";
    public const string TimetableSlotNotFound = "Academic:Timetable:030005";
    public const string InvalidSlotConfiguration = "Academic:Timetable:030006";
    public const string StandardNotFound = "Academic:Timetable:040001";
    public const string LectureSlotNotCreated = "Academic:Timetable:040002";
}
