using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.HomeworkAssignment.Setting;
public class AssigmentTypeConfiguration : IEntityTypeConfiguration<AssigmentType>
{
    public void Configure(EntityTypeBuilder<AssigmentType> builder)
    {
        builder.ToTable(
            ShikshaMitraConsts.DbTablePrefix + "AssignmentTypes",
            ShikshaMitraConsts.DbSchema
        );
        builder.ConfigureByConvention();
    }
}
