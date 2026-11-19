using System;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

// Only operational rules are enrolled. Retired, referenced rules remain in their original rows.
public class LeavePolicySnapshotDto : LeavePolicyCreateRequest
{
	public int SchemaVersion { get; set; } = 1;
	public Guid Id { get; set; }
	public Guid AcademicYearId { get; set; }
	public int Version { get; set; }
}
