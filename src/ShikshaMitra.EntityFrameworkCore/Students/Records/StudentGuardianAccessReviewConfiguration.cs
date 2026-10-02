using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Records;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students;

public class StudentGuardianAccessReviewConfiguration : IEntityTypeConfiguration<StudentGuardianAccessReview>
{
	public void Configure(EntityTypeBuilder<StudentGuardianAccessReview> b)
	{
		b.ToTable(ShikshaMitraConsts.DbTablePrefix + "StudentGuardianAccessReviews", ShikshaMitraConsts.DbSchema); b.ConfigureByConvention();
		b.Property(x => x.Status).HasMaxLength(20).IsRequired(); b.Property(x => x.PreviousLinksJson).HasColumnType("jsonb").IsRequired(); b.Property(x => x.ResolutionJson).HasColumnType("jsonb");
		b.Property(x => x.OnApprovedChangeMode).HasMaxLength(30).IsRequired();
		b.HasIndex(x => new { x.TenantId, x.ChangeRequestId }).IsUnique(); b.HasIndex(x => new { x.TenantId, x.StudentId, x.CreationTime });
	}
}
