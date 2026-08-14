using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.Admissions;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.Admissions;

public class AdmissionOfferingConfiguration : IEntityTypeConfiguration<AdmissionOffering>
{
	public void Configure(EntityTypeBuilder<AdmissionOffering> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionOfferings", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(x => x.Code).IsRequired().HasMaxLength(AdmissionOfferingConsts.MaxCodeLength);
		builder.Property(x => x.OpenDate).IsRequired();
		builder.Property(x => x.CloseDate).IsRequired();
		builder.Property(x => x.IsActive).IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.AdmissionCycleId, x.CampusId, x.BoardId, x.MediumId, x.StageId, x.StandardId }).IsUnique();
		builder.HasIndex(x => new { x.TenantId, x.AdmissionCycleId, x.IsActive });
	}
}
