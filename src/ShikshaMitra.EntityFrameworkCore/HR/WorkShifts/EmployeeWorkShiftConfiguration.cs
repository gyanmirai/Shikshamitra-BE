using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.WorkShifts;
using ShikshaMitra.HR.Employees;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.WorkShifts;

public class EmployeeWorkShiftConfiguration : IEntityTypeConfiguration<EmployeeWorkShift>
{
	public void Configure(EntityTypeBuilder<EmployeeWorkShift> b)
	{
		b.ToTable("HR_WorkShifts"); b.ConfigureByConvention();
		b.Property(x => x.TenantId).IsRequired(); b.Property(x => x.ShiftName).HasMaxLength(128).IsRequired();
		b.HasIndex(x => new { x.TenantId, x.LegacySchoolShiftId }).IsUnique();
		b.ToTable(t => t.HasCheckConstraint("CK_HR_WorkShifts_Daytime", "\"EndTime\" > \"StartTime\""));
	}
}
public class EmployeeShiftAssignmentConfiguration : IEntityTypeConfiguration<EmployeeShiftAssignment>
{
	public void Configure(EntityTypeBuilder<EmployeeShiftAssignment> b)
	{
		b.ToTable("HR_EmployeeShiftAssignments"); b.ConfigureByConvention();
		b.Property(x => x.TenantId).IsRequired(); b.Property(x => x.ShiftName).HasMaxLength(128).IsRequired();
		b.Property(x => x.Provenance).HasMaxLength(64).IsRequired();
		b.HasIndex(x => new { x.TenantId, x.EmployeeId, x.EffectiveFrom });
		b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
		b.HasOne<EmployeeWorkShift>().WithMany().HasForeignKey(x => x.WorkShiftId).OnDelete(DeleteBehavior.Restrict);
		b.ToTable(t => t.HasCheckConstraint("CK_HR_ShiftAssignment_Range", "\"EffectiveTo\" IS NULL OR \"EffectiveTo\" > \"EffectiveFrom\""));
	}
}