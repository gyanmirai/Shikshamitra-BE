using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Transport;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Transport;

public class TransportFeeDueConfiguration : IEntityTypeConfiguration<TransportFeeDue>
{
	public void Configure(EntityTypeBuilder<TransportFeeDue> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "TransportFeeDues", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicMediumId).IsRequired();
		builder.Property(x => x.AcademicYearId).IsRequired();
		builder.Property(x => x.FeeRunId).IsRequired();
		builder.Property(x => x.FeeConfigurationId).IsRequired();
		builder.Property(x => x.ChargeCategoryId).IsRequired();
		builder.Property(x => x.MemberId).IsRequired();
		builder.Property(x => x.MemberType).IsRequired();
		builder.Property(x => x.AssignmentId).IsRequired();
		builder.Property(x => x.RouteId).IsRequired();
		builder.Property(x => x.StopId).IsRequired();
		builder.Property(x => x.FeeModelType).IsRequired();
		builder.Property(x => x.FeeFrequency).IsRequired();
		builder.Property(x => x.PeriodFrom).IsRequired();
		builder.Property(x => x.PeriodTo).IsRequired();
		builder.Property(x => x.DueDate).IsRequired();
		builder.Property(x => x.Amount).HasPrecision(18, 2);
		builder.Property(x => x.Status).IsRequired();
		builder.Property(x => x.MemberName).IsRequired().HasMaxLength(150);
		builder.Property(x => x.MemberCode).HasMaxLength(50);
		builder.Property(x => x.RouteName).IsRequired().HasMaxLength(150);
		builder.Property(x => x.RouteCode).IsRequired().HasMaxLength(50);
		builder.Property(x => x.StopName).IsRequired().HasMaxLength(150);

		builder.HasIndex(x => x.AcademicMediumId);
		builder.HasIndex(x => new { x.AcademicMediumId, x.AcademicYearId, x.Status });
		builder.HasIndex(x => new { x.AcademicMediumId, x.FeeRunId });
		builder.HasIndex(x => new { x.AcademicMediumId, x.MemberId });
		builder.HasIndex(x => new { x.AcademicMediumId, x.MemberType });
		builder.HasIndex(x => new { x.AcademicMediumId, x.RouteId });
		builder.HasIndex(x => new { x.AcademicMediumId, x.StopId });
		builder.HasIndex(x => new { x.AcademicMediumId, x.AcademicYearId, x.FeeConfigurationId, x.MemberId, x.AssignmentId, x.PeriodFrom, x.PeriodTo });
		builder.HasIndex(x => x.FinanceChargeRequestId);
		builder.HasIndex(x => x.FinanceChargeLineId);
	}
}
