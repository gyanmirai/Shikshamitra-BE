using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Setting.GeneralSetting.LibraryTimings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Setting.GeneralSetting;

public class LibraryTimingConfiguration : IEntityTypeConfiguration<LibraryTiming>
{
	public void Configure(EntityTypeBuilder<LibraryTiming> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "LibraryTimings", ShikshaMitraConsts.DbSchema);

		builder.ConfigureByConvention();

		builder.Property(x => x.Day)
			.IsRequired();

		builder.Property(x => x.OpeningTime)
			.IsRequired();

		builder.Property(x => x.ClosingTime)
			.IsRequired();
	}
}
