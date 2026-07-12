using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmSeatOfferConfiguration : IEntityTypeConfiguration<AdmissionCrmSeatOffer>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmSeatOffer> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmSeatOffers", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.OfferNumber).HasMaxLength(AdmissionCrmConsts.MaxOfferNumberLength).IsRequired();
		builder.Property(x => x.OfferType).HasConversion<int>().IsRequired();
		builder.Property(x => x.Status).HasConversion<int>().IsRequired();
		builder.Property(x => x.TermsSnapshotJson).HasMaxLength(AdmissionCrmConsts.MaxOfferTermsJsonLength).IsRequired();
		builder.Property(x => x.ResponseSnapshotJson).HasMaxLength(AdmissionCrmConsts.MaxOfferTermsJsonLength).IsRequired();
		builder.Property(x => x.Language).HasMaxLength(AdmissionCrmConsts.MaxLanguageCodeLength).IsRequired();
		builder.Property(x => x.ResponseSource).HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.Property(x => x.ResponseReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.HasIndex(x => new { x.TenantId, x.OfferNumber }).IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.LeadId, x.Version }).IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.SeatPoolId, x.Status });
		builder.HasIndex(x => new { x.TenantId, x.ValidUntil, x.Status });
	}
}
