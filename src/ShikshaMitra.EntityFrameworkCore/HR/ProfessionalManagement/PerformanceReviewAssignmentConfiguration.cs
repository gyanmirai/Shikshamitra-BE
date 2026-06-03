using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.ProfessionalManagement;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.ProfessionalManagement;
public class PerformanceReviewAssignmentConfiguration : IEntityTypeConfiguration<PerformanceReviewAssignment>
{
	public void Configure(EntityTypeBuilder<PerformanceReviewAssignment> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PerformanceReviewAssignments", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.FinalScore).HasPrecision(7, 2);
		builder.Property(x => x.FinalRating).HasMaxLength(80);
		builder.HasIndex(x => new { x.TenantId, x.CycleId, x.EmployeeId }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.Status });
	}
}
