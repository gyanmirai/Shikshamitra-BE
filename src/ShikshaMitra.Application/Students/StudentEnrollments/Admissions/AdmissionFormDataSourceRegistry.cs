using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Allow-listed master-data sources for dynamic choice fields.</summary>
public sealed class AdmissionFormDataSourceRegistry(
	IEducationBoardRepository educationBoardRepository,
	IEducationMediumRepository educationMediumRepository,
	IStandardRepository standardRepository) : ITransientDependency
{
	public static readonly IReadOnlyList<AdmissionFormDataSourceMetadataDto> Metadata =
	[
		new() { Key = "EducationBoards", DisplayName = "Education boards", Description = "Tenant-visible education boards.", SupportsParent = false },
		new() { Key = "EducationMediums", DisplayName = "Education mediums", Description = "Tenant-visible mediums, optionally filtered by board.", SupportsParent = true },
		new() { Key = "StandardsByMediumStage", DisplayName = "Standards", Description = "Tenant-visible standards for an education-medium stage.", SupportsParent = true }
	];

	public static bool IsAllowed(string? sourceKey) =>
		!string.IsNullOrWhiteSpace(sourceKey) && Metadata.Any(item => string.Equals(item.Key, sourceKey, StringComparison.OrdinalIgnoreCase));

	public async Task<IReadOnlyList<AdmissionFormOptionDto>> GetOptionsAsync(string sourceKey, Guid? parentId, Guid? academicYearId = null)
	{
		var source = Metadata.FirstOrDefault(item => string.Equals(item.Key, sourceKey, StringComparison.OrdinalIgnoreCase));
		if (source == null) throw new ArgumentException("Unknown admission form data source.", nameof(sourceKey));
		if (source.SupportsParent && !parentId.HasValue) return [];

		return source.Key switch
		{
			"EducationBoards" => (await educationBoardRepository.GetListAsync(item =>
				!academicYearId.HasValue || item.AcademicYearId == academicYearId.Value, includeDetails: false))
				.GroupBy(item => item.Type)
				.Select(group => group.OrderBy(item => item.Id).First())
				.OrderBy(item => item.Type)
				.Select((item, index) => new AdmissionFormOptionDto { Value = item.Id.ToString(), Label = item.Type.ToString(), DisplayOrder = index })
				.ToList(),
			"EducationMediums" => (await educationMediumRepository.GetListAsync(item => item.EducationBoardId == parentId!.Value))
				.OrderBy(item => item.Name)
				.Select((item, index) => new AdmissionFormOptionDto { Value = item.Id.ToString(), Label = item.Name, DisplayOrder = index })
				.ToList(),
			"StandardsByMediumStage" => (await standardRepository.GetListAsync(item => item.EducationMediumStageId == parentId!.Value))
				.OrderBy(item => item.Name)
				.Select((item, index) => new AdmissionFormOptionDto { Value = item.Id.ToString(), Label = item.Name, DisplayOrder = index })
				.ToList(),
			_ => []
		};
	}
}
