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
	private readonly IInquiryPreferenceRepository _inquiryPreferenceRepository;

	public InquiryPreferenceManager(IInquiryPreferenceRepository inquiryPreferenceRepository)
	{
		_inquiryPreferenceRepository = inquiryPreferenceRepository;
	}

	/// <summary>
	/// Creates a new inquiry preference with uniqueness validation
	/// </summary>
	public async Task<InquiryPreference> CreateAsync(
		Guid? tenantId,
		string preferenceName)
	{
		// Check if inquiry preference with same name already exists for this tenant
		await CheckNameUniquenessAsync(tenantId, preferenceName);

		var inquiryPreference = new InquiryPreference(
			GuidGenerator.Create(),
			tenantId,
			preferenceName
		);

		return inquiryPreference;
	}

	/// <summary>
	/// Updates an existing inquiry preference with uniqueness validation
	/// </summary>
	public async Task<InquiryPreference> UpdateAsync(
		InquiryPreference inquiryPreference,
		string preferenceName)
	{
		// Check uniqueness only if name is changing
		if (inquiryPreference.PreferenceName != preferenceName)
		{
			await CheckNameUniquenessAsync(inquiryPreference.TenantId, preferenceName, inquiryPreference.Id);
		}

		inquiryPreference.Update(preferenceName);

		return inquiryPreference;
	}

	/// <summary>
	/// Checks if an inquiry preference name is unique within the tenant
	/// </summary>
	private async Task CheckNameUniquenessAsync(
		Guid? tenantId,
		string preferenceName,
		Guid? excludeId = null)
	{
		var query = await _inquiryPreferenceRepository.GetQueryableAsync();

		if (await AsyncExecuter.AnyAsync(query.Where(x =>
			x.TenantId == tenantId &&
			x.PreferenceName == preferenceName &&
			(excludeId == null || x.Id != excludeId))))
		{
			throw new BusinessException("InquiryPreference:DuplicateName")
				.WithData("PreferenceName", preferenceName);
		}
	}
}
