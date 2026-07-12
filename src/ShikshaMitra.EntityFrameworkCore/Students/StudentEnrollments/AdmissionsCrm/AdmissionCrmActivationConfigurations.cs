using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmNumberSeriesConfiguration : IEntityTypeConfiguration<AdmissionCrmNumberSeries>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmNumberSeries> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmNumberSeries", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Code).HasMaxLength(AdmissionCrmConsts.MaxPolicyCodeLength).IsRequired();
		builder.Property(x => x.Name).HasMaxLength(AdmissionCrmConsts.MaxNameLength).IsRequired();
		builder.Property(x => x.Prefix).HasMaxLength(AdmissionCrmConsts.MaxNumberPatternLength).IsRequired();
		builder.Property(x => x.Suffix).HasMaxLength(AdmissionCrmConsts.MaxNumberPatternLength).IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Code }).IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.IdentifierType, x.Status });
	}
}

public class AdmissionCrmIdentifierAllocationConfiguration : IEntityTypeConfiguration<AdmissionCrmIdentifierAllocation>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmIdentifierAllocation> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmIdentifierAllocations", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Value).HasMaxLength(AdmissionCrmConsts.MaxIdentifierValueLength).IsRequired();
		builder.Property(x => x.LifecycleReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.HasIndex(x => new { x.TenantId, x.IdentifierType, x.Value }).IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.ActivationId, x.IdentifierType }).IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.NumberSeriesId, x.Sequence }).IsUnique().HasFilter("\"IsDeleted\" = false");
	}
}

public class AdmissionCrmActivationConfiguration : IEntityTypeConfiguration<AdmissionCrmActivation>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmActivation> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmActivations", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.ActivationReference).HasMaxLength(AdmissionCrmConsts.MaxActivationReferenceLength).IsRequired();
		builder.Property(x => x.ReadinessSnapshotJson).HasColumnType("text").IsRequired();
		builder.Property(x => x.StepsJson).HasColumnType("text").IsRequired();
		builder.Property(x => x.ServiceHandoffRequestsJson).HasColumnType("text").IsRequired();
		builder.Property(x => x.RepairStep).HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.Property(x => x.RepairReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.Property(x => x.CancellationReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.HasIndex(x => new { x.TenantId, x.LeadId }).IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.ActivationReference }).IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status });
	}
}

public class AdmissionCrmServiceHandoffConfiguration : IEntityTypeConfiguration<AdmissionCrmServiceHandoff>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmServiceHandoff> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmServiceHandoffs", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.DestinationModule).HasMaxLength(AdmissionCrmConsts.MaxNameLength).IsRequired();
		builder.Property(x => x.PayloadJson).HasColumnType("text").IsRequired();
		builder.Property(x => x.ExternalReference).HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.Property(x => x.LastError).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.HasIndex(x => new { x.TenantId, x.ActivationId, x.Type }).IsUnique().HasFilter("\"IsDeleted\" = false");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Status });
	}
}
