using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Transport;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Transport;

public class TransportMemberConfiguration : IEntityTypeConfiguration<TransportMember>
{
	public void Configure(EntityTypeBuilder<TransportMember> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "TransportMembers", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicMediumId).IsRequired();
		builder.Property(x => x.MemberType).IsRequired();
		builder.Property(x => x.Status).IsRequired();
		builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(150);
		builder.Property(x => x.MemberCode).HasMaxLength(50);
		builder.Property(x => x.Role).HasMaxLength(50);
		builder.Property(x => x.ClassName).HasMaxLength(100);
		builder.Property(x => x.DivisionName).HasMaxLength(100);
		builder.Property(x => x.PhoneNo).HasMaxLength(30);
		builder.Property(x => x.PhotoUrl).HasMaxLength(500);
		builder.Property(x => x.Remarks).HasMaxLength(500);

		builder.HasIndex(x => x.AcademicMediumId);
		builder.HasIndex(x => new { x.AcademicMediumId, x.MemberType, x.StudentId });
		builder.HasIndex(x => new { x.AcademicMediumId, x.MemberType, x.EmployeeId });
		builder.HasIndex(x => new { x.AcademicMediumId, x.Status });
		builder.HasIndex(x => new { x.AcademicMediumId, x.CurrentRouteId });
		builder.HasIndex(x => new { x.AcademicMediumId, x.CurrentStopId });
	}
}
