using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.Identity;

namespace ShikshaMitra.HR.AttendanceAndLeaves;

public class EmployeeLeaveConfiguration : IEntityTypeConfiguration<EmployeeLeave>
{
	public void Configure(EntityTypeBuilder<EmployeeLeave> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "EmployeeLeaves", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
	
		// builder.HasOne(e => e.User)
		// 	.WithMany()
		// 	.HasForeignKey(e => e.UserId) // Explicitly set the foreign key
		// 	.OnDelete(DeleteBehavior.Restrict);
	
		builder.HasOne(e => e.Employee)
			.WithMany()
			.HasForeignKey(e => e.EmployeeId)
			.OnDelete(DeleteBehavior.Restrict);
	
		// Auto Include 
		// builder.Navigation(e => e.User).AutoInclude();
	
		// Auto Include 
		builder.Navigation(e => e.Employee).AutoInclude();
		
		
		// reference to the leavePolicy
		builder.HasOne(e => e.LeavePolicy)
			.WithMany()
			.HasForeignKey(e => e.LeaveTypeId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}