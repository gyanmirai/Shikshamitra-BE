using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.Attendance;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<StudentAttendanceConfiguration> StudentAttendanceConfigurations { get; set; }
	public DbSet<StudentAttendancePolicyVersion> StudentAttendancePolicyVersions { get; set; }
	public DbSet<StudentAttendanceSchedule> StudentAttendanceSchedules { get; set; }
}
