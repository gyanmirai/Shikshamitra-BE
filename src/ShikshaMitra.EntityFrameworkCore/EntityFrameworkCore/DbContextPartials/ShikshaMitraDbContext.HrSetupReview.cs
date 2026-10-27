using System;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.Data;

namespace ShikshaMitra.EntityFrameworkCore;

public partial class ShikshaMitraDbContext
{
	private static bool IsAdvisorySetupReviewOnly(EntityEntry entry)
	{
		if (entry.State != EntityState.Modified || entry.Entity is not AcademicYear) return false;
		var extra = entry.Property(nameof(AcademicYear.ExtraProperties));
		if (extra.OriginalValue is not ExtraPropertyDictionary before || extra.CurrentValue is not ExtraPropertyDictionary after)
			return false;
		before.TryGetValue(AcademicYearHrSetupReview.Property, out var previousReview);
		after.TryGetValue(AcademicYearHrSetupReview.Property, out var currentReview);
		if (JsonSerializer.Serialize(previousReview) == JsonSerializer.Serialize(currentReview)) return false;
		var previousOther = before.Where(x => x.Key != AcademicYearHrSetupReview.Property).OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
		var currentOther = after.Where(x => x.Key != AcademicYearHrSetupReview.Property).OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
		if (JsonSerializer.Serialize(previousOther) != JsonSerializer.Serialize(currentOther)) return false;
		// Repository Update can mark every column modified. Compare actual values, allowing only
		// review evidence plus its ordinary concurrency/audit bookkeeping to bypass the payroll fence.
		return entry.Properties.Where(x => x.Metadata.Name is not (nameof(AcademicYear.ExtraProperties)
			or nameof(AcademicYear.ConcurrencyStamp) or nameof(AcademicYear.LastModificationTime) or nameof(AcademicYear.LastModifierId)))
			.All(x => Equals(x.OriginalValue, x.CurrentValue));
	}
}
