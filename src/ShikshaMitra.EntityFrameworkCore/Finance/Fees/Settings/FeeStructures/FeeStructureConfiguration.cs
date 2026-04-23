using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra;
using ShikshaMitra.Finance.Fees.Settings.FeeStructures;
using ShikshaMitra.Finance.Setting.FeeStructures;
using Volo.Abp.EntityFrameworkCore.Modeling;

public class FeeStructureConfiguration : IEntityTypeConfiguration<FeeStructure>
{
	public void Configure(EntityTypeBuilder<FeeStructure> builder)
	{
		builder.ToTable("FeeStructures", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		// FeeStructureDetail is a child entity of FeeStructure aggregate root.
		// Cascade delete ensures details are removed when the parent structure is deleted.
		builder.HasMany(x => x.FeeStructureDetails)
			.WithOne()
			.HasForeignKey(x => x.FeeStructureId)
			.OnDelete(DeleteBehavior.Cascade);
		builder.Navigation(x => x.FeeStructureDetails).AutoInclude();
	}
}
