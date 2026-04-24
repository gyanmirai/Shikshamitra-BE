using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.Fees;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.Finance.Fees.FeeCollection;

public class StudentFeeRecordConfiguration : IEntityTypeConfiguration<StudentFeeRecord>
{
    public void Configure(EntityTypeBuilder<StudentFeeRecord> builder)
    {
        builder.ToTable("StudentFeeRecords", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention();

        builder.Property(x => x.Remarks)
            .HasMaxLength(1000);

        builder.HasIndex(x => new { x.StudentId, x.AcademicYearId })
            .IsUnique()
            .HasDatabaseName("UX_StudentFeeRecords_StudentId_AcademicYearId");
    }
}
