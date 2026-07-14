using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmWaitlistEntryConfiguration : IEntityTypeConfiguration<AdmissionCrmWaitlistEntry>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmWaitlistEntry> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmWaitlistEntries", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Status).HasConversion<int>().IsRequired();
		builder.Property(x => x.ClosureReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.Property(x => x.ProtectionReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.Property(x => x.PromotionPauseReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.HasIndex(x => new { x.TenantId, x.LeadId, x.SeatPoolId })
			.IsUnique().HasFilter("\"IsDeleted\" = false AND \"Status\" = 0");
		builder.HasIndex(x => new { x.TenantId, x.SeatPoolId, x.Status, x.PriorityScore, x.PositionSequence });
	}
}

public class AdmissionCrmWaitlistHistoryConfiguration : IEntityTypeConfiguration<AdmissionCrmWaitlistHistory>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmWaitlistHistory> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmWaitlistHistories", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Reason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength).IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.WaitlistEntryId, x.OccurredAt });
		builder.HasIndex(x => new { x.TenantId, x.SeatPoolId, x.Action });
	}
}
