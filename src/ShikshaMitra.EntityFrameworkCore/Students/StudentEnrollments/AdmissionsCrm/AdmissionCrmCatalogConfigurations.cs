using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmInquirySourceConfiguration : IEntityTypeConfiguration<AdmissionCrmInquirySource>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmInquirySource> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmInquirySources", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Code).HasMaxLength(AdmissionCrmConsts.MaxPlaybookCodeLength).IsRequired();
		builder.Property(x => x.DisplayName).HasMaxLength(AdmissionCrmConsts.MaxSourceSnapshotLength).IsRequired();
		builder.Property(x => x.Description).HasMaxLength(AdmissionCrmConsts.MaxDescriptionLength);
		builder.Property(x => x.DeactivationReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Code }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.IsActive, x.DisplayOrder });
	}
}

public class AdmissionCrmExamSlotConfiguration : IEntityTypeConfiguration<AdmissionCrmExamSlot>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmExamSlot> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmExamSlots", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.SlotTemplateCode).HasMaxLength(AdmissionCrmConsts.MaxPlaybookCodeLength);
		builder.Property(x => x.ExamDefinitionCode).HasMaxLength(AdmissionCrmConsts.MaxPlaybookCodeLength);
		builder.Property(x => x.SlotName).HasMaxLength(AdmissionCrmConsts.MaxNameLength).IsRequired();
		builder.Property(x => x.Campus).HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.Property(x => x.RoomOrHall).HasMaxLength(AdmissionCrmConsts.MaxNameLength);
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.StartDate });
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.RoomNo, x.StartDate, x.EndDate });
	}
}

public class AdmissionCrmDocumentTypeConfiguration : IEntityTypeConfiguration<AdmissionCrmDocumentType>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmDocumentType> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmDocumentTypes", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Code).HasMaxLength(AdmissionCrmConsts.MaxPlaybookCodeLength).IsRequired();
		builder.Property(x => x.Name).HasMaxLength(AdmissionCrmConsts.MaxNameLength).IsRequired();
		builder.Property(x => x.MaximumSizeMb).HasPrecision(8, 2);
		builder.Property(x => x.AcceptedExtensions).HasMaxLength(200).IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Code }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.IsActive, x.DisplayOrder });
	}
}
