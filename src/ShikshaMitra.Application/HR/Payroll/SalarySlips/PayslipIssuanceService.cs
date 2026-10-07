using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ShikshaMitra.HR.Employees;
using ShikshaMitra.HR.Employees.Payroll;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Payroll.SalarySlips;

/// <summary>Called within the fenced payroll transaction, before finalization is committed.</summary>
[RemoteService(false)]
public class PayslipIssuanceService(
	IRepository<PayslipDocument, Guid> documents,
	IRepository<PayslipSettings, Guid> settings,
	IEmployeeRepository employees,
	IEmployeePayrollDetailsRepository payrollDetails,
	SalarySlipSnapshotReader snapshots,
	PayslipDeliveryQueue deliveryQueue,
	PayslipPdfService pdfService) : ApplicationService
{
	public async Task<PayslipDocument> IssueAsync(SalarySlip slip, bool fromPayrollLock = false)
	{
		var existing = await documents.FindAsync(x => x.SalarySlipId == slip.Id && x.Revision == 1);
		if (existing != null) return existing;
		ShikshaMitra.HR.Payroll.Statutory.EsiPayrollReadiness.EnsureInsuranceNumber(
			slip.EmployeeStateInsuranceRuleId, slip.EmployeeStateInsuranceCoverageDecision, slip.EmployeeStateInsuranceNumber);
		var employee = await employees.GetAsync(slip.EmployeeId, includeDetails: false);
		var details = await payrollDetails.GetByEmployeeIdAsync(slip.EmployeeId);
		var snapshot = await snapshots.GetAsync(slip.EmployeeId, slip.MonthYear);
		var dto = SalarySlipProjection.ToDto(slip, employee, snapshot, details);
		var documentId = GuidGenerator.Create();
		dto.DocumentId = documentId;
		dto.DocumentRevision = 1;
		dto.Status = SalarySlipStatus.Finalized;
		dto.ProcessedDate = Clock.Now;
		dto.SlipNumber = $"PS-{slip.Id:N}-R1";
		var school = await settings.FindAsync(x => x.TenantId == slip.TenantId)
			?? throw new BusinessException(HRErrorCodes.PayslipSchoolDetailsRequired);
		var model = SalarySlipProjection.ToPdfModel(dto, school.SchoolName) with { InstituteAddress = school.SchoolAddress };
		var bytes = pdfService.Generate(model);
		var document = new PayslipDocument(documentId, slip.TenantId, slip.Id, slip.EmployeeId,
			slip.MonthYear, 1, dto.ProcessedDate, CurrentUser.Id, JsonSerializer.Serialize(dto), bytes);
		await documents.InsertAsync(document, autoSave: false);
		if (school.AutomaticEmail) await deliveryQueue.QueueAsync(document, document.Id, automatic: true,
			fromPayrollLock ? ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR.PayrollManagement_RunPayroll.Lock
			: ShikshaMitra.Constants.Permissions.ShikshaMitraSubmodulePermissions.HR.PayrollManagement_GenerateSalarySlip.Add);
		return document;
	}

	public async Task<PayslipDocument> GetCurrentAsync(Guid slipId)
	{
		var query = await documents.GetQueryableAsync();
		return await AsyncExecuter.FirstOrDefaultAsync(query.Where(x => x.SalarySlipId == slipId)
			.OrderByDescending(x => x.Revision)) ?? throw new BusinessException(HRErrorCodes.PayslipDocumentRequired);
	}

	public async Task<PayslipDocument> IssueCorrectionAsync(SalarySlip slip, PayslipDocument previous,
		Guid requestId, string reason)
	{
		if (slip.Status != SalarySlipStatus.Finalized || (await GetCurrentAsync(slip.Id)).Id != previous.Id)
			throw new BusinessException(HRErrorCodes.PayslipConflict);
		var employee = await employees.GetAsync(slip.EmployeeId, includeDetails: false);
		var details = await payrollDetails.GetByEmployeeIdAsync(slip.EmployeeId);
		var snapshot = await snapshots.GetAsync(slip.EmployeeId, slip.MonthYear);
		var dto = SalarySlipProjection.ToDto(slip, employee, snapshot, details);
		var school = await EnsureSchoolConfiguredAsync();
		dto.DocumentId = GuidGenerator.Create();
		dto.DocumentRevision = previous.Revision + 1;
		dto.ProcessedDate = Clock.Now;
		dto.SlipNumber = $"PS-{slip.Id:N}-R{dto.DocumentRevision}";
		var model = SalarySlipProjection.ToPdfModel(dto, school.SchoolName) with { InstituteAddress = school.SchoolAddress };
		var document = new PayslipDocument(dto.DocumentId.Value, slip.TenantId, slip.Id, slip.EmployeeId,
			slip.MonthYear, dto.DocumentRevision.Value, dto.ProcessedDate, CurrentUser.Id,
			JsonSerializer.Serialize(dto), pdfService.Generate(model), previous.Id, reason, requestId);
		await documents.InsertAsync(document);
		if (school.AutomaticEmail) await deliveryQueue.QueueAsync(document, document.Id, automatic: true);
		return document;
	}

	public async Task<PayslipDocument> ReplaceFactsAsync(SalarySlip slip, PayslipDocument previous, Guid requestId, string reason)
	{
		if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 1000 || requestId == Guid.Empty ||
			slip.Status is not (SalarySlipStatus.Finalized or SalarySlipStatus.Paid))
			throw new BusinessException(HRErrorCodes.PayslipConflict);
		var repeated = await documents.FindAsync(x => x.SalarySlipId == slip.Id && x.RequestId == requestId);
		if (repeated != null)
		{
			if (repeated.ReplacesDocumentId != previous.Id || repeated.ReplacementReason != reason.Trim())
				throw new BusinessException(HRErrorCodes.PayslipConflict);
			return repeated;
		}
		if ((await GetCurrentAsync(slip.Id)).Id != previous.Id) throw new BusinessException(HRErrorCodes.PayslipConflict);
		var employee = await employees.GetAsync(slip.EmployeeId, includeDetails: false);
		var details = await payrollDetails.GetByEmployeeIdAsync(slip.EmployeeId);
		var school = await EnsureSchoolConfiguredAsync();
		// Start from the issued monetary/attendance evidence. Only document identity facts change.
		var dto = ReadFacts(previous, SalarySlipStatus.Finalized);
		dto.NetPayInWords = IndianCurrencyWords.Format(dto.NetSalary);
		dto.EmployeeName = employee.EmployeeName;
		dto.EmployeeCode = employee.EmployeeCode;
		dto.Department = employee.Department;
		dto.Designation = employee.Designation;
		dto.DateOfJoining = employee.DateOfJoining;
		dto.BankName = details?.BankName;
		dto.MaskedAccountNumber = SalarySlipProjectionFormatting.MaskAccountNumber(details?.AccountNumber);
		dto.IfscCode = details?.IfscCode;
		// Statutory identities remain calculated payroll evidence, rather than today's enrollment.
		dto.DocumentId = GuidGenerator.Create();
		dto.DocumentRevision = previous.Revision + 1;
		dto.ProcessedDate = Clock.Now;
		dto.SlipNumber = $"PS-{slip.Id:N}-R{dto.DocumentRevision}";
		var model = SalarySlipProjection.ToPdfModel(dto, school.SchoolName) with { InstituteAddress = school.SchoolAddress };
		var document = new PayslipDocument(dto.DocumentId.Value, slip.TenantId, slip.Id, slip.EmployeeId, slip.MonthYear,
			dto.DocumentRevision.Value, dto.ProcessedDate, CurrentUser.Id, JsonSerializer.Serialize(dto), pdfService.Generate(model),
			previous.Id, reason, requestId);
		await documents.InsertAsync(document);
		if (school.AutomaticEmail) await deliveryQueue.QueueAsync(document, document.Id, automatic: true);
		return document;
	}

	public async Task<PayslipSettings> EnsureSchoolConfiguredAsync() =>
		await settings.FindAsync(x => x.TenantId == CurrentTenant.Id)
			?? throw new BusinessException(HRErrorCodes.PayslipSchoolDetailsRequired);

	public static SalarySlipDto ReadFacts(PayslipDocument document, SalarySlipStatus status)
	{
		var facts = JsonSerializer.Deserialize<SalarySlipDto>(document.FactsJson)
			?? throw new BusinessException(HRErrorCodes.PayslipDocumentInvalid);
		// Payment status changes independently; none of the document's frozen facts change.
		facts.Status = status;
		return facts;
	}
}
