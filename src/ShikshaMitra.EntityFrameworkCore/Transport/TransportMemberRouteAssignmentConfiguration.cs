using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Transport;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Transport;

public class TransportMemberRouteAssignmentConfiguration : IEntityTypeConfiguration<TransportMemberRouteAssignment>
{
	public void Configure(EntityTypeBuilder<TransportMemberRouteAssignment> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "TransportMemberRouteAssignments", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicMediumId).IsRequired();
		builder.Property(x => x.MemberId).IsRequired();
		builder.Property(x => x.RouteId).IsRequired();
		builder.Property(x => x.StopId).IsRequired();
		builder.Property(x => x.EffectiveFrom).IsRequired();
		builder.Property(x => x.EffectiveTo);
		builder.Property(x => x.FeeAmountSnapshot).HasPrecision(18, 2);
		builder.Property(x => x.Remarks).HasMaxLength(500);

		builder.HasIndex(x => x.AcademicMediumId);
		builder.HasIndex(x => new { x.AcademicMediumId, x.MemberId, x.EffectiveTo });
		builder.HasIndex(x => new { x.AcademicMediumId, x.RouteId, x.EffectiveTo });
		builder.HasIndex(x => new { x.AcademicMediumId, x.StopId, x.EffectiveTo });
		builder.HasIndex(x => new { x.AcademicMediumId, x.ApplicationId });
	}
}
