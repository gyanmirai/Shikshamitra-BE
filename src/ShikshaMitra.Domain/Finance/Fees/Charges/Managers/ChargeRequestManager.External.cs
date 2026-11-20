using System;
using System.Threading.Tasks;
using ShikshaMitra.Finance.ExternalReceivables;
using Volo.Abp.Data;

namespace ShikshaMitra.Finance.Fees.Charges;

public partial class ChargeRequestManager
{
	public async Task<(ChargeRequest Request, Charge Charge)> CreateExternalAssessmentAsync(
		Guid tenantId, Guid yearId, Guid categoryId, FinanceExternalPayerIdentity payer,
		decimal amount, DateOnly assessmentDate, DateOnly dueDate, string reason,
		string sourceSnapshot, Guid requestorId)
	{
		var category = await GetActiveCategoryAsync(categoryId, yearId);
		var request = ChargeRequest.CreateExternal(guidGenerator.Create(), tenantId, yearId,
			"Library assessment", category.Id, reason, amount, dueDate, requestorId, sourceSnapshot);
		request.SetProperty("IsApprovalRequiredAtCreation", category.IsApprovalRequired);
		await SetIncomeCategoryFromChargeCategoryAsync(request, category);
		await chargeRequestRepository.InsertAsync(request, autoSave: false);
		var charge = Charge.CreateExternal(guidGenerator.Create(), tenantId, yearId, request.Id,
			request.ChargeName, category.Id, payer, reason, amount, assessmentDate, dueDate, requestorId);
		charge.SetIncomeCategory(request.IncomeCategoryId!.Value, request.IncomeCategoryName!);
		charge.SetAccountingSnapshot(request.IncomeCategoryId.Value, category.IncomeAccountHeadId);
		await chargeRepository.InsertAsync(charge, autoSave: false);
		return (request, charge);
	}
}
