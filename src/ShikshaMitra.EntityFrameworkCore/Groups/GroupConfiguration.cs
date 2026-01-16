using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShikshaMitra.Groups;
using System.Text.Json;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace ShikshaMitra.EntityFrameworkCore.Groups;

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.ToTable(ShikshaMitraConsts.DbTablePrefix + "AppGroups", ShikshaMitraConsts.DbSchema);
        builder.ConfigureByConvention(); 

        builder.Property(x => x.Name).IsRequired().HasMaxLength(128);
        builder.Property(x => x.Description).HasMaxLength(500);

        // JSON properties setup with ValueConverters
        builder.Property(e => e.AcademicRules)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => JsonSerializer.Deserialize<List<AcademicRule>>(v, (JsonSerializerOptions)null));

        builder.Property(e => e.Roles)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions)null));

        builder.Property(e => e.StudentFilters)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => JsonSerializer.Deserialize<List<UserFilter>>(v, (JsonSerializerOptions)null));

        builder.Property(e => e.EmployeeFilters)
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => JsonSerializer.Deserialize<List<UserFilter>>(v, (JsonSerializerOptions)null));
        
        builder.Property(e => e.SelectedUserIds)
             .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                v => JsonSerializer.Deserialize<List<Guid>>(v, (JsonSerializerOptions)null));
    }
}
