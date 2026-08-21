using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class AdmissionNoteConfiguration : IEntityTypeConfiguration<AdmissionNote>
{
	public void Configure(EntityTypeBuilder<AdmissionNote> b) { b.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionNotes", ShikshaMitraConsts.DbSchema); b.ConfigureByConvention(); b.ConfigureSoftDeleteDefault(); b.Property(x => x.Text).IsRequired().HasColumnType("text"); b.HasIndex(x => new { x.TenantId, x.ApplicantId, x.CreationTime }); }
}
