using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Transport;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Transport;

public class TransportApplicationConfiguration : IEntityTypeConfiguration<TransportApplication>
{
	public void Configure(EntityTypeBuilder<TransportApplication> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "TransportApplications", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.AcademicMediumId).IsRequired();
		builder.Property(x => x.ApplicationType).IsRequired();
		builder.Property(x => x.Status).IsRequired();
		builder.Property(x => x.MemberType).IsRequired();
		builder.Property(x => x.PickupType).HasMaxLength(50);
		builder.Property(x => x.Landmark).HasMaxLength(200);
		builder.Property(x => x.AreaLocality).HasMaxLength(200);
		builder.Property(x => x.AddressLine1).HasMaxLength(300);
		builder.Property(x => x.Description).HasMaxLength(1000);
		builder.Property(x => x.EstimatedFeeAmount).HasPrecision(18, 2);
		builder.Property(x => x.ApplicantName).IsRequired().HasMaxLength(150);
		builder.Property(x => x.ApplicantCode).HasMaxLength(50);
		builder.Property(x => x.ApplicantRole).HasMaxLength(50);
		builder.Property(x => x.ApplicantClassName).HasMaxLength(100);
		builder.Property(x => x.ApplicantDivisionName).HasMaxLength(100);
		builder.Property(x => x.ApplicantPhoneNo).HasMaxLength(30);
		builder.Property(x => x.ApplicantPhotoUrl).HasMaxLength(500);
		builder.Property(x => x.StatusRemarks).HasMaxLength(500);

		builder.HasIndex(x => x.AcademicMediumId);
		builder.HasIndex(x => new { x.AcademicMediumId, x.ApplicationType });
		builder.HasIndex(x => new { x.AcademicMediumId, x.Status });
		builder.HasIndex(x => new { x.AcademicMediumId, x.MemberType, x.StudentId, x.Status });
		builder.HasIndex(x => new { x.AcademicMediumId, x.MemberType, x.EmployeeId, x.Status });
		builder.HasIndex(x => new { x.AcademicMediumId, x.AssignedRouteId });
		builder.HasIndex(x => new { x.AcademicMediumId, x.AssignedStopId });
	}
}
