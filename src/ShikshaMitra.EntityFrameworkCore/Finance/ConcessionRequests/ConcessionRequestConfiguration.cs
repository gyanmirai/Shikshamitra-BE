using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.ConcessionRequests;

public class ConcessionRequestConfiguration : IEntityTypeConfiguration<ConcessionRequest>
{
    public void Configure(EntityTypeBuilder<ConcessionRequest> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "ConcessionRequests", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();
    }
}
