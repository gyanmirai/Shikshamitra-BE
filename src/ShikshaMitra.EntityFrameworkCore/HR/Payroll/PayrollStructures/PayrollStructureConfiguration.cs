using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.Domain.Entities;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.HR.Payroll.PayrollStructures;

public class PayrollStructureConfiguration : IEntityTypeConfiguration<PayrollStructure>
{
	public void Configure(EntityTypeBuilder<PayrollStructure> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "PayrollStructures", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.PayrollStructureType).IsRequired();
		builder.Property(x => x.Amount).IsRequired();

		builder.HasOne(x => x.PayElement).WithMany().HasForeignKey(x => x.PayElementId).IsRequired()
			.OnDelete(DeleteBehavior.Restrict);

		builder.HasOne(x => x.EducationMediumStage).WithMany().HasForeignKey(x => x.EducationMediumStageId).IsRequired()
			.OnDelete(DeleteBehavior.Restrict);

		builder.Navigation(x => x.PayElement).AutoInclude();
		// builder.Navigation(x => x.EducationMediumStage).AutoInclude();
	}
}