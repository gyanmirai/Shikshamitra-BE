using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using ShikshaMitra.Finance.Fees.Settings;

namespace ShikshaMitra.Domain.Finance.Fees.Settings.Managers;

public class FeeCategoryManager(
	IRepository<FeeCategory, Guid> feeCategoryRepository,
	IRepository<FeeHead, Guid> feeHeadRepository
) : DomainService
{
	public async Task EnsureNameIsUniqueAsync(Guid academicYearId, string name, Guid? excludeId = null)
	{
		var normalizedName = NormalizeName(name);

		var existing = await feeCategoryRepository.FirstOrDefaultAsync(x =>
			x.AcademicYearId == academicYearId &&
			x.NormalizedName == normalizedName &&
			(!excludeId.HasValue || x.Id != excludeId.Value));

		if (existing != null)
		{
			throw new BusinessException(FinanceFeeCategoryErrorCodes.DuplicateName)
				.WithData("Name", normalizedName)
				.WithData("Field", "Name");
		}
	}

	public async Task EnsureFeeHeadNameIsUniqueAsync(Guid academicYearId, Guid feeCategoryId, string name, Guid? excludeId = null)
	{
		var normalizedName = NormalizeName(name);

		var existing = await feeHeadRepository.FirstOrDefaultAsync(x =>
			x.AcademicYearId == academicYearId &&
			x.FeeCategoryId == feeCategoryId &&
			x.NormalizedName == normalizedName &&
			x.IsCurrentRevision &&
			(!excludeId.HasValue || x.Id != excludeId.Value));

		if (existing != null)
		{
			throw new BusinessException(FinanceFeeCategoryErrorCodes.DuplicateFeeHeadName)
				.WithData("Name", normalizedName)
				.WithData("Field", "Name");
		}
	}

	public static string NormalizeName(string name) =>
		FeeSetupNameNormalizer.ForComparison(name);
}
