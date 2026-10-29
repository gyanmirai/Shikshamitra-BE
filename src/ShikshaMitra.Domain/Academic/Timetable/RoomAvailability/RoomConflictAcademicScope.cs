using System;

namespace ShikshaMitra.Academic.Timetable.RoomAvailability;

// The Medium's Board year is read from the server hierarchy, never supplied by a booking DTO.
public sealed record RoomConflictAcademicScope(Guid AcademicMediumId, Guid AcademicYearId, Guid StandardId, Guid MediumAcademicYearId);
