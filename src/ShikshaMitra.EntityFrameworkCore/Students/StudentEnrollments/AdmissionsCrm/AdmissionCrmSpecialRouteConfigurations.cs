using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmRouteDefinitionConfiguration : IEntityTypeConfiguration<AdmissionCrmRouteDefinition>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmRouteDefinition> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmRouteDefinitions", ShikshaMitraConsts.DbSchema); builder.ConfigureByConvention();
		builder.Property(x => x.Code).HasMaxLength(AdmissionCrmConsts.MaxPolicyCodeLength).IsRequired();
		builder.Property(x => x.Name).HasMaxLength(AdmissionCrmConsts.MaxNameLength).IsRequired();
		builder.Property(x => x.ConfigurationJson).HasColumnType("text").IsRequired();
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.Code, x.Version }).IsUnique().HasFilter("\"IsDeleted\" = false");
	}
}

public class AdmissionCrmRouteAssignmentConfiguration : IEntityTypeConfiguration<AdmissionCrmRouteAssignment>
{
	public void Configure(EntityTypeBuilder<AdmissionCrmRouteAssignment> builder)
	{
		builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AdmissionCrmRouteAssignments", ShikshaMitraConsts.DbSchema); builder.ConfigureByConvention();
		builder.Property(x => x.RouteDataJson).HasColumnType("text").IsRequired(); builder.Property(x => x.EvidenceJson).HasColumnType("text").IsRequired();
		builder.Property(x => x.AssignmentReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength).IsRequired();
		builder.Property(x => x.EvaluationReason).HasMaxLength(AdmissionCrmConsts.MaxNotesLength);
		builder.HasIndex(x => new { x.TenantId, x.LeadId, x.IsPrimary }).IsUnique().HasFilter("\"IsDeleted\" = false AND \"IsPrimary\" = true");
		builder.HasIndex(x => new { x.TenantId, x.AcademicYearId, x.RouteType, x.Status });
	}
}
