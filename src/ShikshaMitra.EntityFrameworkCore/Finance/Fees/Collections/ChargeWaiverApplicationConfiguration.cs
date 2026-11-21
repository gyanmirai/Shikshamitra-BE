using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Finance.Fees.FeeCollection;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Finance.Fees.FeeCollection;

public class ChargeWaiverApplicationConfiguration : IEntityTypeConfiguration<ChargeWaiverApplication>
{
	public void Configure(EntityTypeBuilder<ChargeWaiverApplication> builder)
	{
		builder.ToTable("Finance_ChargeWaiverApplications", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();
		builder.Property(row => row.LegacyConcurrencyStamp).HasColumnName("ConcurrencyStamp").HasMaxLength(40).IsRequired();
		builder.Property(row => row.LegacyExtraPropertiesJson).HasColumnName("ExtraProperties").HasColumnType("text").IsRequired();
		builder.ToTable(table => table.HasCheckConstraint("CK_ChargeWaiverApplications_Payer", ShikshaMitra.Finance.ExternalReceivables.FinancePayerConstraintSql.StudentOrEmployee));
		builder.HasOne<ShikshaMitra.HR.Employees.Employee>().WithMany().HasForeignKey(row => row.EmployeeId).OnDelete(DeleteBehavior.Restrict);
		builder.HasIndex(row => new { row.TenantId, row.EmployeeId }).HasFilter("\"EmployeeId\" IS NOT NULL");
		builder.Property(x => x.ChargeAmount).HasColumnType("numeric(18,2)").IsRequired();
		builder.Property(x => x.EffectiveAmount).HasColumnType("numeric(18,2)").IsRequired();
		builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
		builder.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
		builder.HasIndex(x => x.ChargeId).HasDatabaseName("IX_ChargeWaiverApplications_ChargeId");
		builder.HasIndex(x => new { x.AppliedByUserId, x.IdempotencyKey })
			.IsUnique().HasDatabaseName("UX_ChargeWaiverApplications_Actor_IdempotencyKey");
	}
}
