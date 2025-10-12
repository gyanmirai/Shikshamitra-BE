using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Academic.Curriculum.Setting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Academic.Examination.Setting;
public class ExamTypeConfiguration : IEntityTypeConfiguration<ExamType>
{
    public void Configure(EntityTypeBuilder<ExamType> builder)
    {
        builder.ToTable(
            ShikshaMitraConsts.DbTablePrefix + "ExamTypes",
            ShikshaMitraConsts.DbSchema
        );
    }
}
