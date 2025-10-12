using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.HomeworkAssignment.Setting;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.HomeworkAssignment.Setting;

public class HomeworkTypeConfiguration : IEntityTypeConfiguration<HomeWorkType>
{
    public void Configure(EntityTypeBuilder<HomeWorkType> builder)
    {
        builder.ToTable(
            ShikshaMitraConsts.DbTablePrefix + "HomeworkTypes",
            ShikshaMitraConsts.DbSchema
        );

        builder.ConfigureByConvention();
    }
}
