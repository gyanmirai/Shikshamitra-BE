using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Students;

public class AcademicTermConfiguration : IEntityTypeConfiguration<AcademicTerm>
{
	public void Configure(EntityTypeBuilder<AcademicTerm> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AcademicTerms", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.Name)
			.IsRequired()
			.HasMaxLength(100);

		builder.HasOne<AcademicYear>()
			.WithMany()
			.HasForeignKey(x => x.AcademicYearId)
			.OnDelete(DeleteBehavior.Restrict)
			.IsRequired();

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Sequence })
			.IsUnique();

		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.IsCurrent });
	}
}
