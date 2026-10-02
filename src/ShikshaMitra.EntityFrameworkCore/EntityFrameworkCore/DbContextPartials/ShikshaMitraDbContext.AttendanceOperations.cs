using Microsoft.EntityFrameworkCore;
using ShikshaMitra.Students.Attendance;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	public DbSet<StudentAttendanceCorrection> StudentAttendanceCorrections { get; set; }
	public DbSet<StudentAttendanceOperation> StudentAttendanceOperations { get; set; }
}
