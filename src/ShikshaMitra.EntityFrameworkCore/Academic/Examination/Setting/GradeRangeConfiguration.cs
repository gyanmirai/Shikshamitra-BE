using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShikshaMitra.Academic.Examination.Setting;
public class GradeRangeConfiguration : IEntityTypeConfiguration<GradeRange>
{
	public void Configure(EntityTypeBuilder<GradeRange> builder)
	{ 
		builder.ToTable(
			ShikshaMitraConsts.DbTablePrefix + "GradeRanges",
			ShikshaMitraConsts.DbSchema
		);
	}
}
