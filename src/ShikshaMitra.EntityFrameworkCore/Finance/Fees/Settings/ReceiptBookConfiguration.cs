using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Domain.Finance.Fees.Settings;

namespace ShikshaMitra.Finance.Fees.Settings;

public class ReceiptBookConfiguration : IEntityTypeConfiguration<ReceiptBook>
{
	public void Configure(EntityTypeBuilder<ReceiptBook> builder)
	{
		builder.ToTable("Finance_ReceiptBooks");

		builder.HasOne(r => r.EducationBoard)
			.WithMany()
			.HasForeignKey(r => r.BoardId)
			.IsRequired();

		builder.HasOne(r => r.EducationMedium)
			.WithMany()
			.HasForeignKey(r => r.MediumId)
			.IsRequired();

		builder.HasOne(r => r.EducationMediumStage)
			.WithMany()
			.HasForeignKey(r => r.MediumStageId)
			.IsRequired();

		builder.HasOne(r => r.AcademicYear)
			.WithMany()
			.HasForeignKey(r => r.AcademicYearId)
			.IsRequired();
	}
}