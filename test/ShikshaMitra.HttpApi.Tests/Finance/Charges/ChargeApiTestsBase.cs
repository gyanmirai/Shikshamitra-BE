using System;
using System.Threading.Tasks;

namespace ShikshaMitra.Finance.Charges;

/// <summary>
/// Abstract base for all Charge API test classes.
/// Seeds <see cref="ChargeTestHelper"/> defaults from the shared factory
/// and provides convenience methods for common charge lifecycle setups.
/// </summary>
public abstract class ChargeApiTestsBase : ShikshaMitraApiTestBase
{
	protected const string BaseUrl = "/api/app/charges";
	protected const string ChargeCategoriesUrl = "/api/charge-categories";

	protected ChargeApiTestsBase(ShikshaMitraApiTestFactory factory) : base(factory)
	{
		ChargeTestHelper.DefaultAcademicYearId = factory.SeededAcademicYearId;
		ChargeTestHelper.DefaultMediumId = factory.SeededMediumId;
		ChargeTestHelper.DefaultStandardId = factory.SeededStandardId;
		ChargeTestHelper.DefaultDivisionId = factory.SeededDivisionId;
	}

	/// <summary>
	/// Creates a student, an approval-required category, and a charge.
	/// Returns the charge ID and the student's board ID (for filter tests).
	/// </summary>
	protected async Task<(Guid ChargeId, Guid BoardId, Guid EducationMediumId)> CreatePendingChargeAsync()
	{
		var (studentId, boardId) = await ChargeTestHelper.CreateStudentAsync(Client);
		var categoryId = await ChargeTestHelper.CreateChargeCategoryAsync(Client, isApprovalRequired: true);
		var chargeId = await ChargeTestHelper.CreateChargeAsync(Client, studentId, categoryId);
		return (chargeId, boardId, ChargeTestHelper.DefaultMediumId);
	}

	/// <summary>
	/// Creates a student, a no-approval category, and a charge that is immediately Approved.
	/// </summary>
	protected async Task<Guid> CreateAutoApprovedChargeAsync()
	{
		var (studentId, _) = await ChargeTestHelper.CreateStudentAsync(Client);
		var categoryId = await ChargeTestHelper.CreateChargeCategoryAsync(Client, isApprovalRequired: false);
		return await ChargeTestHelper.CreateChargeAsync(Client, studentId, categoryId);
	}
}
