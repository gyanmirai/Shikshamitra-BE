using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.Accounts.Transactions;
using ShikshaMitra.TenantSettings.AcademicYears;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Uow;

namespace ShikshaMitra.Domain.Finance.Accounts.Settings;

/// <summary>Creates persisted, tenant/year-owned routing choices without special IDs in financial commands.</summary>
public class NoneCategoryInitializer(
	IRepository<TransactionCategory, Guid> categories,
	ITransactionCategoryReferenceLock referenceLock) : DomainService
{
	[UnitOfWork(isTransactional: true)]
	public virtual async Task InitializeAsync(AcademicYear year)
	{
		using (CurrentTenant.Change(year.TenantId))
		{
			await referenceLock.AcquireAsync();
			foreach (var type in new[] { TransactionType.Income, TransactionType.Expense })
			{
				if (await categories.AnyAsync(x => x.TenantId == year.TenantId && x.AcademicYearId == year.Id &&
					x.CategoryType == type && (x.NormalizedName == "NONE" || Enumerable.Contains(new[] { "NONE" }, x.Name.ToUpper())))) continue;
				var id = GuidGenerator.Create();
				await categories.InsertAsync(new TransactionCategory(id, year.TenantId, year.Id, type,
					"None", $"No specific {type.ToString().ToLowerInvariant()} category.", code: id.ToString("N").ToUpperInvariant()));
			}
		}
	}
}
