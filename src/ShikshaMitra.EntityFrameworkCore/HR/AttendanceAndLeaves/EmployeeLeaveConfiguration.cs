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
	
		// PublicId: Stable, auto-increment ID for API routes and display
		// Uses DB identity generation for scalability
		builder.Property(e => e.PublicId)
			.UseIdentityAlwaysColumn()
			.ValueGeneratedOnAdd();
		
		// Unique index on PublicId per tenant for fast lookups
		builder.HasIndex(e => new { e.TenantId, e.PublicId })
			.IsUnique()
			.HasDatabaseName("IX_EmployeeLeaves_Tenant_PublicId");
	
		builder.HasOne(e => e.Employee)
			.WithMany()
			.HasForeignKey(e => e.EmployeeId)
			.OnDelete(DeleteBehavior.Restrict);
	
		// Auto Include 
		builder.Navigation(e => e.Employee).AutoInclude();
		
		// reference to the leavePolicy
		builder.HasOne(e => e.LeavePolicy)
			.WithMany()
			.HasForeignKey(e => e.LeaveTypeId)
			.OnDelete(DeleteBehavior.Restrict);
		
		// Auto Include 
		builder.Navigation(e => e.LeavePolicy).AutoInclude();
	}
}