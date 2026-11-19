using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class AdmissionParentRegistrationConfiguration : IEntityTypeConfiguration<AdmissionParentRegistration>
{
	public void Configure(EntityTypeBuilder<AdmissionParentRegistration> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionParentRegistrations", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.ConfigureUtcInstants(nameof(AdmissionParentRegistration.ExpiresAt), nameof(AdmissionParentRegistration.LastSavedAt),
			nameof(AdmissionParentRegistration.HandedInAt), nameof(AdmissionParentRegistration.DeliveryStartedAt));
		builder.Property(row => row.TokenHash).HasMaxLength(64).IsRequired();
		builder.Property(row => row.RecipientFingerprint).HasMaxLength(64).IsRequired();
		builder.Property(row => row.RequestFingerprint).HasMaxLength(64).IsRequired();
		builder.Property(row => row.ProposalBaseline).HasMaxLength(64);
		builder.Property(row => row.Status).HasMaxLength(32).IsRequired();
		builder.Property(row => row.DeliveryStatus).HasMaxLength(32).IsRequired();
		builder.Property(row => row.Purpose).HasMaxLength(16).IsRequired();
		builder.Property(row => row.Notes).HasMaxLength(1024);
		builder.Property(row => row.DeliveryError).HasMaxLength(1024);
		builder.HasIndex(row => row.TokenHash).IsUnique();
		builder.HasIndex(row => new { row.TenantId, row.ApplicationId, row.CreationTime });
		builder.HasOne<AdmissionApplication>().WithMany().HasForeignKey(row => row.ApplicationId).OnDelete(DeleteBehavior.Restrict);
	}
}
