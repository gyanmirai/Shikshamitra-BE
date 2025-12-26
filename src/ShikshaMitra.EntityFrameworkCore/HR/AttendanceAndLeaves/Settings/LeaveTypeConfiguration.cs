using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.AttendanceAndLeaves.Settings;

/// <summary>
/// EF Core configuration for LeaveType entity
/// </summary>
public class LeaveTypeConfiguration : IEntityTypeConfiguration<LeaveType>
{
	public void Configure(EntityTypeBuilder<LeaveType> builder)
	{
		builder.ToTable("AppEmployeeLeaveTypes");

		builder.ConfigureByConvention();

		// Configure properties
		builder.Property(x => x.EducationMediumStageId)
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

		// Navigation property
		builder.HasOne(x => x.EducationMediumStage)
			.WithMany()
			.HasForeignKey(x => x.EducationMediumStageId)
			.OnDelete(DeleteBehavior.Restrict);

		// Indexes for performance
		// Unique index scoped per tenant and organization
		builder.HasIndex(x => new { x.TenantId, x.EducationMediumStageId, x.Type })
			.IsUnique()
			.HasDatabaseName("IX_EmployeeLeaveTypes_TenantId_MediumStageId_Type");

		builder.HasIndex(x => new { x.TenantId, x.EducationMediumStageId, x.IsActive })
			.HasDatabaseName("IX_EmployeeLeaveTypes_TenantId_MediumStageId_IsActive");
	}
}
