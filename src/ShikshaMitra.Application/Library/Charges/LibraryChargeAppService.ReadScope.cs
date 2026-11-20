using System;
using System.Linq;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using ShikshaMitra.Library.Books;
using ShikshaMitra.Security.UserScope;
using Volo.Abp;

namespace ShikshaMitra.Library.Charges;

public partial class LibraryChargeAppService
{
	private async Task<IQueryable<LibraryCharge>> GetReadableChargesAsync()
	{
		var context = await libraryAccess.GetContextAsync();
		var allowedYearIds = context.AllowedAcademicYearIds.ToArray();
		// The Library-owned source projection applies the exact recorded loan scope
		// for linked assessments and current borrower scope for direct assessments.
		// Reading history does not require the assessment to be Finance-postable.
		var readable = await LazyServiceProvider.LazyGetRequiredService<IFinanceExternalSourceScope>()
			.GetReadableSourcesAsync(FinanceExternalSourceKind.LibraryAssessment);
		return (await chargeRepository.GetQueryableAsync()).Where(charge =>
			charge.TenantId == tenant.Id &&
			(!charge.AcademicYearId.HasValue || Enumerable.Contains(allowedYearIds, charge.AcademicYearId.Value)) &&
			readable.Any(source => source.SourceId == charge.Id));
	}

	private async Task EnsureChargeReadableAsync(Guid id)
	{
		if (!await AsyncExecuter.AnyAsync((await GetReadableChargesAsync()).Where(charge => charge.Id == id)))
			throw new BusinessException(UserScopeErrorCodes.AccessDenied);
	}
}
