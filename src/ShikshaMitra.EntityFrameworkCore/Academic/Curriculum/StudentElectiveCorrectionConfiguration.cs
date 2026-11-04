using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Curriculum;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Academic.Curriculum;

public class StudentElectiveCorrectionConfiguration : IEntityTypeConfiguration<StudentElectiveCorrection>
{
	public void Configure(EntityTypeBuilder<StudentElectiveCorrection> builder)
	{
		builder.ToTable(ShikshaMitraConsts.AcademicDbTablePrefix + "StudentElectiveCorrections", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.BeforeJson).IsRequired();
		builder.Property(x => x.AfterJson).IsRequired();
		builder.Property(x => x.ImpactsJson).IsRequired();
		builder.Property(x => x.Reason).HasMaxLength(1000);
		builder.HasIndex(x => new { x.TenantId, x.StudentId, x.ElectiveGroupId, x.CreationTime });
	}
}
