using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.Charges;

public class ChargeRequestConfiguration : IEntityTypeConfiguration<ChargeRequest>
{
	public void Configure(EntityTypeBuilder<ChargeRequest> builder)
	{
		builder.ToTable("ChargeRequests", ShikshaMitraConsts.DbSchema);
		builder.ConfigureByConvention();

		builder.Property(x => x.ChargeName)
			.IsRequired()
			.HasMaxLength(ChargeRequest.MaxChargeNameLength);

		builder.Property(x => x.PurposeDescription)
			.IsRequired()
			.HasMaxLength(1000);

		builder.Property(x => x.Amount)
			.HasColumnType("decimal(18,2)")
			.IsRequired();

		builder.Property(x => x.AdditionalNotes)
			.HasMaxLength(2000);

		builder.Property(x => x.AudienceSnapshot)
			.IsRequired()
			.HasColumnType("text");

		builder.Property(x => x.DueDate).IsRequired();
		builder.Property(x => x.AppliedOn).IsRequired();
		builder.Property(x => x.Priority).IsRequired();

		builder.HasIndex(x => new { x.AcademicYearId, x.BoardId, x.EducationMediumId })
			.HasDatabaseName("IX_ChargeRequests_AcademicYear_Board_Medium");
	}
}
