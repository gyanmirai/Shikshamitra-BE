using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Transport;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Transport;

public class TransportFeeRunConfiguration : IEntityTypeConfiguration<TransportFeeRun>
{
	public void Configure(EntityTypeBuilder<TransportFeeRun> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "TransportFeeRuns", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicMediumId).IsRequired();
		builder.Property(x => x.AcademicYearId).IsRequired();
		builder.Property(x => x.FeeConfigurationId).IsRequired();
		builder.Property(x => x.Status).IsRequired();
		builder.Property(x => x.PeriodFrom).IsRequired();
		builder.Property(x => x.PeriodTo).IsRequired();
		builder.Property(x => x.DueDate).IsRequired();
		builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
		builder.Property(x => x.Remarks).HasMaxLength(500);

		builder.HasIndex(x => x.AcademicMediumId);
		builder.HasIndex(x => new { x.AcademicMediumId, x.AcademicYearId, x.Status });
		builder.HasIndex(x => new { x.AcademicMediumId, x.FeeConfigurationId, x.AcademicYearId, x.PeriodFrom, x.PeriodTo });
	}
}
