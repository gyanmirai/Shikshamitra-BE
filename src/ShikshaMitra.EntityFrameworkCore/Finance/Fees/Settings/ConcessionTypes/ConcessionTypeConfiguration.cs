using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra;
using ShikshaMitra.Finance.Fees.Settings.ConcessionTypes;
using Volo.Abp.EntityFrameworkCore.Modeling;


public class ConcessionTypeConfiguration : IEntityTypeConfiguration<ConcessionType>
{
	public void Configure(EntityTypeBuilder<ConcessionType> builder)
	{
		builder.ToTable("ConcessionTypes", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Navigation(x => x.ConcessionTypeDocuments).AutoInclude();
	}
}
