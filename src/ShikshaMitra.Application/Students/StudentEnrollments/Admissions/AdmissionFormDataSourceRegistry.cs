using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationMediums;
using ShikshaMitra.TenantSettings.AcademicSetups.Standards;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace ShikshaMitra.Students.StudentEnrollments.Admissions;

/// <summary>Allow-listed master-data sources for dynamic choice fields.</summary>
public sealed class AdmissionFormDataSourceRegistry(
	IEducationBoardRepository educationBoardRepository,
	IEducationMediumRepository educationMediumRepository,
	IStandardRepository standardRepository,
	IStudentRepository studentRepository,
	StudentScopeAccessService studentScope,
	IAsyncQueryableExecuter asyncExecuter,
	ICurrentTenant currentTenant) : ITransientDependency
{
	public static readonly IReadOnlyList<AdmissionFormDataSourceMetadataDto> Metadata =
	[
		new() { Key = "StudentSiblings", DisplayName = "Sibling students", Description = "Student identities within the signed-in staff member's school scope.", SupportsParent = false },
		new() { Key = "EducationBoards", DisplayName = "Education boards", Description = "Tenant-visible education boards.", SupportsParent = false },
		new() { Key = "EducationMediums", DisplayName = "Education mediums", Description = "Tenant-visible mediums, optionally filtered by board.", SupportsParent = true },
		new() { Key = "StandardsByMediumStage", DisplayName = "Standards", Description = "Tenant-visible standards for an education-medium stage.", SupportsParent = true }
	];

	public static bool IsAllowed(string? sourceKey) =>
		!string.IsNullOrWhiteSpace(sourceKey) && Metadata.Any(item => string.Equals(item.Key, sourceKey, StringComparison.OrdinalIgnoreCase));

	internal async Task<bool> AreSiblingIdsAllowedAsync(IReadOnlyCollection<Guid> ids)
	{
		if (ids.Count == 0) return true;
		var selected = ids.Distinct().ToArray();
		var query = await studentScope.ApplyCurrentUserScopeAsync(await studentRepository.GetQueryableWithoutAutoIncludesAsync());
		return await asyncExecuter.CountAsync(query.Where(student => student.TenantId == currentTenant.Id &&
			Enumerable.Contains(selected, student.Id))) == selected.Length;
	}

	public async Task<IReadOnlyList<AdmissionFormOptionDto>> GetOptionsAsync(string sourceKey, Guid? parentId, Guid? academicYearId = null,
		string? searchTerm = null, int skipCount = 0, int maxResultCount = 20, List<Guid>? selectedIds = null)
	{
		var source = Metadata.FirstOrDefault(item => string.Equals(item.Key, sourceKey, StringComparison.OrdinalIgnoreCase));
		if (source == null) throw new ArgumentException("Unknown admission form data source.", nameof(sourceKey));
		if (source.SupportsParent && !parentId.HasValue) return [];
		if (source.Key == "StudentSiblings")
		{
			var query = await studentScope.ApplyCurrentUserScopeAsync(await studentRepository.GetQueryableWithoutAutoIncludesAsync());
			query = query.Where(student => student.TenantId == currentTenant.Id);
			if (selectedIds is { Count: > 0 })
			{
				if (selectedIds.Count > 20 || selectedIds.Contains(Guid.Empty)) throw new Volo.Abp.BusinessException("Admissions:InvalidSiblingSelection");
				var selected = selectedIds.Distinct().ToArray();
				query = query.Where(student => Enumerable.Contains(selected, student.Id));
				skipCount = 0;
				maxResultCount = 20;
			}
			else if (!string.IsNullOrWhiteSpace(searchTerm))
			{
				var term = searchTerm.Trim().ToLowerInvariant();
				if (term.Length > 100) throw new ArgumentException("Search term must be at most 100 characters.", nameof(searchTerm));
				query = query.Where(student => (student.FirstName + " " + student.LastName).ToLower().Contains(term) ||
					student.GrNumber != null && student.GrNumber.ToLower().Contains(term));
			}
			var students = await asyncExecuter.ToListAsync(query
				.OrderBy(student => student.FirstName).ThenBy(student => student.LastName)
				.ThenBy(student => student.Id).Skip(Math.Max(0, skipCount)).Take(Math.Clamp(maxResultCount, 1, 21))
				.Select(student => new { student.Id, student.FirstName, student.LastName, student.GrNumber }));
			return students.Select((student, index) => new AdmissionFormOptionDto
			{
				Value = student.Id.ToString(), Label = $"{student.FirstName} {student.LastName}".Trim() +
					(string.IsNullOrWhiteSpace(student.GrNumber) ? string.Empty : $" ({student.GrNumber})"), DisplayOrder = index
			}).ToList();
		}

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
