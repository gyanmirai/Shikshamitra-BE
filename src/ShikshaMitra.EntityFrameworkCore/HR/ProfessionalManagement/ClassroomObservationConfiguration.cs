using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.HR.ProfessionalManagement;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.HR.ProfessionalManagement;
public class ClassroomObservationConfiguration : IEntityTypeConfiguration<ClassroomObservation>
{
	public void Configure(EntityTypeBuilder<ClassroomObservation> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ClassroomObservations", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.HasKey(x => x.Id);
		builder.Property(x => x.ClassName).HasMaxLength(120);
		builder.Property(x => x.SubjectName).HasMaxLength(120);
		builder.Property(x => x.Notes).HasMaxLength(2000);
		builder.HasIndex(x => new { x.TenantId, x.ReviewAssignmentId, x.ObservationDate });
	}
}
