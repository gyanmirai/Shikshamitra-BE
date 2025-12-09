using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace ShikshaMitra.Students.StudentEnrollments.Settings;

/// <summary>
/// Domain service for InquiryPreference aggregate
/// Handles business rules and validations
/// </summary>
public class InquiryPreferenceManager : DomainService
{
	private readonly IRepository<InquiryPreference, Guid> _inquiryPreferenceRepository;

	public InquiryPreferenceManager(IRepository<InquiryPreference, Guid> inquiryPreferenceRepository)
	{
		_inquiryPreferenceRepository = inquiryPreferenceRepository;
	}

	/// <summary>
	/// Creates a new inquiry preference with uniqueness validation
	/// </summary>
	public async Task<InquiryPreference> CreateAsync(
		Guid? tenantId,
		Guid academicYearId,
		string preferenceName)
	{
		// Check if inquiry preference with same name already exists for this tenant
		await CheckNameUniquenessAsync(tenantId, academicYearId, preferenceName);

		var inquiryPreference = new InquiryPreference(
			GuidGenerator.Create(),
			tenantId,
			academicYearId,
			preferenceName
		);

		return inquiryPreference;
	}

	/// <summary>
	/// Updates an existing inquiry preference with uniqueness validation
	/// </summary>
	public async Task<InquiryPreference> UpdateAsync(
		InquiryPreference inquiryPreference,
		Guid academicYearId,
		string preferenceName)
	{
		// Check uniqueness only if name is changing
		if (inquiryPreference.PreferenceName != preferenceName || inquiryPreference.AcademicYearId != academicYearId)
		{
			await CheckNameUniquenessAsync(inquiryPreference.TenantId, academicYearId, preferenceName, inquiryPreference.Id);
		}

		inquiryPreference.Update(academicYearId, preferenceName);

		return inquiryPreference;
	}

	/// <summary>
	/// Checks if an inquiry preference name is unique within the tenant
	/// </summary>
	private async Task CheckNameUniquenessAsync(
		Guid? tenantId,
		Guid academicYearId,
		string preferenceName,
		Guid? excludeId = null)
	{
		var query = await _inquiryPreferenceRepository.GetQueryableAsync();

		if (await AsyncExecuter.AnyAsync(query.Where(x =>
			x.TenantId == tenantId &&
			x.AcademicYearId == academicYearId &&
			x.PreferenceName == preferenceName &&
			(excludeId == null || x.Id != excludeId))))
		{
			throw new BusinessException("InquiryPreference:DuplicateName")
				.WithData("PreferenceName", preferenceName);
		}
	}
}
