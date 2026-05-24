using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.ProfessionalManagement;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.ProfessionalManagement;

public class ProfessionalReviewConfiguration : IEntityTypeConfiguration<ProfessionalReview>
{
	public void Configure(EntityTypeBuilder<ProfessionalReview> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ProfessionalReviews", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.Comments).HasMaxLength(2000);
		builder.Property(x => x.Feedback).HasMaxLength(2000);
		builder.HasIndex(x => new { x.TenantId, x.CycleId, x.EmployeeId }).IsUnique();
	}
}
