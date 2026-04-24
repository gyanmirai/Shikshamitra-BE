using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

public class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
	public void Configure(EntityTypeBuilder<LeaveType> builder)
	{
		builder.ToTable("AppEmployeeLeaveTypes");

		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicYearId)
			.IsRequired();

		builder.Property(x => x.Type)
			.IsRequired()
			.HasMaxLength(LeaveTypeConsts.MaxTypeLength);

		builder.Property(x => x.Description)
			.HasMaxLength(LeaveTypeConsts.MaxDescriptionLength);

		builder.Property(x => x.Color)
			.IsRequired()
			.HasMaxLength(LeaveTypeConsts.MaxColorLength);

		builder.Property(x => x.IsActive)
			.IsRequired()
			.HasDefaultValue(true);

		builder.HasOne(x => x.AcademicYear)
			.WithMany()
			.HasForeignKey(x => x.AcademicYearId)
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Type })
			.IsUnique()
			.HasDatabaseName("IX_EmployeeLeaveTypes_TenantId_AcademicYearId_Type");

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.IsActive })
			.HasDatabaseName("IX_EmployeeLeaveTypes_TenantId_AcademicYearId_IsActive");
	}
}
