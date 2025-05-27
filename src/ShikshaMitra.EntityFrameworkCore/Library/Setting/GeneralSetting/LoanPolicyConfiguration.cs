using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Library.Setting.GeneralSetting.LoanPolicies;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Library.Setting.GeneralSetting;

public class LoanPolicyConfiguration : IEntityTypeConfiguration<LoanPolicy>
{
	public void Configure(EntityTypeBuilder<LoanPolicy> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "LoanPolicies", ShikshaMitraConsts.DbSchema);

		builder.ConfigureByConvention();

		builder.Property(x => x.RoleId)
			.IsRequired()
			.HasMaxLength(64);

		builder.Property(x => x.LoanDurationInDays)
			.IsRequired();

		builder.Property(x => x.MaxBooksAllowed)
			.IsRequired();

		builder.Property(x => x.LateReturnFinePerDay)
			.IsRequired()
			.HasColumnType("decimal(18,2)");
	}
}
