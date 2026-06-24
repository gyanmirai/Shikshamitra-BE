using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Transport;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Transport;

public class TransportStaffConfiguration : IEntityTypeConfiguration<TransportStaff>
{
	public void Configure(EntityTypeBuilder<TransportStaff> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "TransportStaff", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicMediumId).IsRequired();
		builder.Property(x => x.EmployeeId).IsRequired();
		builder.Property(x => x.Role).IsRequired();
		builder.Property(x => x.LicenseNo).HasMaxLength(50);
		builder.Property(x => x.LicenseExpiryDate);
		builder.Property(x => x.ExperienceYears);
		builder.Property(x => x.Status).IsRequired();
		builder.Property(x => x.Remarks).HasMaxLength(500);

		builder.HasIndex(x => x.AcademicMediumId);
		builder.HasIndex(x => x.EmployeeId);
		builder.HasIndex(x => new { x.AcademicMediumId, x.EmployeeId, x.Role }).IsUnique();
		builder.HasIndex(x => new { x.AcademicMediumId, x.Role, x.Status });
	}
}
