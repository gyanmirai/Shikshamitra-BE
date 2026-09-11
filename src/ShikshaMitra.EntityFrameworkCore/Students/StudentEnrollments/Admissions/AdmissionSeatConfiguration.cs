using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class AdmissionSeatConfiguration :
	IEntityTypeConfiguration<AdmissionSeatPool>,
	IEntityTypeConfiguration<AdmissionSeatReservation>
{
	public void Configure(EntityTypeBuilder<AdmissionSeatPool> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionSeatPools", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.ConfigureSoftDeleteDefault();
		builder.HasIndex(item => new { item.TenantId, item.AdmissionOfferingId })
			.IsUnique().HasDatabaseName("UX_AdmissionSeatPools_Offering").HasFilter("\"IsDeleted\" = false");
		builder.HasOne<AdmissionOffering>().WithMany().HasForeignKey(item => item.AdmissionOfferingId)
			.OnDelete(DeleteBehavior.Restrict);
	}

	public void Configure(EntityTypeBuilder<AdmissionSeatReservation> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionSeatReservations", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.ConfigureSoftDeleteDefault();
		builder.Property(item => item.LastReason).HasMaxLength(512);
		builder.HasIndex(item => new { item.TenantId, item.ApplicationId, item.StageInstanceId })
			.IsUnique().HasDatabaseName("UX_AdmissionSeatReservations_ApplicationStage").HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(item => new { item.TenantId, item.PoolId, item.Status, item.ExpiresAt });
		builder.HasIndex(item => new { item.TenantId, item.PoolId, item.Status, item.CreationTime });
		builder.HasOne<AdmissionSeatPool>().WithMany().HasForeignKey(item => item.PoolId)
			.OnDelete(DeleteBehavior.Restrict);
		builder.HasOne<AdmissionApplication>().WithMany().HasForeignKey(item => item.ApplicationId)
			.OnDelete(DeleteBehavior.Cascade);
		builder.HasOne<AdmissionApplicationStageInstance>().WithMany().HasForeignKey(item => item.StageInstanceId)
			.OnDelete(DeleteBehavior.Restrict);
	}
}
