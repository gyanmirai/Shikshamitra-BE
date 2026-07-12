using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmSeatReservationConfiguration : IEntityTypeConfiguration<AdmissionCrmSeatReservation>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmSeatReservation> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmSeatReservations", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Status).HasConversion<int>().IsRequired();
		builder.Property(x => x.ReleaseReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.Property(x => x.CapacityOverrideReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.HasIndex(x => new { x.TenantId, x.SeatOfferId }).IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.LeadId, x.SeatPoolId })
			.IsUnique().HasFilter("\"IsDeleted\" = false AND \"Status\" = 0");
		builder.HasIndex(x => new { x.TenantId, x.SeatPoolId, x.Status });
		builder.HasIndex(x => new { x.TenantId, x.ExpiresAt, x.Status });
	}
}
