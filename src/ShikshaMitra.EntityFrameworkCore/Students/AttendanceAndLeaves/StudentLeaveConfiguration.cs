using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Attendance;
using ShikshaMitra.Students.StudentRecords;

namespace ShikshaMitra.Students.AttendanceAndLeaves;

public class StudentLeaveConfiguration : IEntityTypeConfiguration<StudentLeave>
{
	public void Configure(EntityTypeBuilder<StudentLeave> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentLeaves",ShikshaMitraConsts.DbSchema);

		builder.HasOne(e => e.Student)
		.WithMany()
		.HasForeignKey(e => e.StudentId)
		.OnDelete(DeleteBehavior.Restrict);

		// Auto Include
		builder.Navigation(e => e.Student).AutoInclude();

		// reference to the leavePolicy
		builder.HasOne(e => e.LeavePolicy)
			.WithMany()
			.HasForeignKey(e => e.LeaveTypeId)
			.OnDelete(DeleteBehavior.Restrict);

		// Auto Include 
		builder.Navigation(e => e.LeavePolicy).AutoInclude();
	}
}
