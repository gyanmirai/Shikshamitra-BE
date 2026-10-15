using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Finance.Fees.Collections.Dtos;
using ShikshaMitra.Finance.Fees.FeeCollection;
using ShikshaMitra.Finance.Fees.StudentFeeAssignment;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Finance.Fees.Collections;

public partial class AdmissionFeeCollectionAppService
{
	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add)]
	public async Task<AdmissionFeeCollectionDto> PrepareAsync(Guid applicationId, PrepareAdmissionFeeCollectionDto input)
	{
		if (input?.VariableFeeHeads == null || input.VariableFeeHeads.Count > 200)
			throw new Volo.Abp.Validation.AbpValidationException("Invalid variable fee amounts.");
		if (input.VariableFeeHeads.Any(v => v == null || v.FeeHeadId == Guid.Empty || !v.Amount.HasValue ||
			v.Amount < 0m || v.Amount > 9_999_999_999.99m || decimal.Round(v.Amount.Value, 2) != v.Amount || v.ZeroAmountReason?.Length > 500 ||
			v.Amount == 0m && string.IsNullOrWhiteSpace(v.ZeroAmountReason)) ||
			input.VariableFeeHeads.Select(v => v.FeeHeadId).Distinct().Count() != input.VariableFeeHeads.Count)
			throw new Volo.Abp.Validation.AbpValidationException("Invalid variable fee amounts.");
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.View);
		using var unit = units.Begin(new Volo.Abp.Uow.AbpUnitOfWorkOptions
		{ IsTransactional = true, IsolationLevel = System.Data.IsolationLevel.Serializable }, requiresNew: true);
		var source = await contextReader.GetAsync(applicationId);
		if (!source.CanCollect || !source.StructureId.HasValue) throw new BusinessException(AdmissionFeeCollectionErrorCodes.InvalidState, source.Blocker);
		var account = await accounts.FindAsync(a => a.ApplicationId == applicationId && a.AcademicYearId == source.AcademicYearId);
		if (account != null) await scope.EnsureFeeAccountAccessibleAsync(account);
		var saved = account?.AssignmentSnapshotJson == null ? null : ReadAssignment(account);
		if (saved != null && SelectionMatches(source, saved) && (input.VariableFeeHeads.Count == 0 || VariableAmountsMatch(saved, input.VariableFeeHeads)))
		{
			if (source.StudentId.HasValue)
			{
				EnsureHandoffContext(source, account!, source.StudentId.Value);
				await LinkAccountStudentAsync(source, account!, source.StudentId.Value);
			}
			account!.SetDivision(source.DivisionId);
			await accounts.UpdateAsync(account, false);
			if (source.EnrollmentId.HasValue) await BindEnrolledStudentAsync(source, account);
			await unit.CompleteAsync();
			return await GetSummaryAsync(applicationId);
		}
		if (account == null)
		{
			account = new FinanceFeeAccount(GuidGenerator.Create(), CurrentTenant.Id, source.ApplicationId, source.ApplicantId,
				source.AcademicYearId, source.BoardId, source.MediumId, source.StageId, source.StandardId, source.ApplicantName,
				new ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos.StudentFeeAssignmentPreviewDto().Currency, source.StudentId);
			account.SetDivision(source.DivisionId);
			await accounts.InsertAsync(account, false);
		}
		else if (saved != null)
		{
			await ResetUnusedPreparationAsync(account);
		}
		var existingAssignment = source.StudentId.HasValue ? await LazyServiceProvider.LazyGetRequiredService<IRepository<StudentFeeStructure, Guid>>()
			.FirstOrDefaultAsync(a => a.StudentId == source.StudentId.Value && a.AcademicYearId == source.AcademicYearId) : null;
		if (existingAssignment != null)
		{
			var existing = existingAssignment.AssignmentSnapshotJson == null ? null : JsonSerializer.Deserialize<ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos.StudentFeeAssignmentPreviewDto>(existingAssignment.AssignmentSnapshotJson);
			if (existing == null || !SelectionMatches(source, existing) || existingAssignment.FinanceFeeAccountId.HasValue && existingAssignment.FinanceFeeAccountId != account.Id)
				throw new BusinessException(AdmissionFeeCollectionErrorCodes.AssignmentConflict);
			var existingRows = await dues.GetListAsync(d => d.StudentFeeStructureId == existingAssignment.Id);
			foreach (var row in existingRows) row.BindFinanceFeeAccount(account.Id);
			existingAssignment.BindFinanceFeeAccount(account.Id);
			account.CaptureAssignment(existing.StructureId, existing.RevisionNumber, existingAssignment.AssignmentSnapshotJson!, existingAssignment.AssignmentRequestFingerprint!);
			account.BindStudentAssignment(existingAssignment.Id);
			if (existingRows.Any(d => d.PaidAmount > 0m || d.RefundedAmount > 0m) ||
				await transactions.AnyAsync(t => t.StudentId == source.StudentId && t.AcademicYearId == source.AcademicYearId))
			{
				account.RecordPaymentEvidence();
				await contextReader.RecordPaymentEvidenceAsync(applicationId, account.Id);
			}
			await dues.UpdateManyAsync(existingRows, false);
		}
		else
		{
			var calculated = await assignmentCalculator.PrepareAdmissionAsync(source, account.Id, input.VariableFeeHeads);
			account.CaptureAssignment(calculated.Structure.Id, calculated.Structure.RevisionNumber, JsonSerializer.Serialize(calculated.Preview), calculated.Fingerprint);
			await dues.InsertManyAsync(calculated.Dues, false);
		}
		await accounts.UpdateAsync(account, false);
		await CurrentUnitOfWork!.SaveChangesAsync();
		if (source.EnrollmentId.HasValue) await BindEnrolledStudentAsync(source, account);
		await unit.CompleteAsync();
		return await GetSummaryAsync(applicationId);
	}

	private static bool VariableAmountsMatch(
		ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos.StudentFeeAssignmentPreviewDto saved,
		System.Collections.Generic.IReadOnlyList<ShikshaMitra.Finance.Fees.StudentFeeAssignment.Dtos.VariableFeeHeadAmountDto> requested)
	{
		var variables = saved.Heads.Where(h => h.Included && h.IsVariable).ToList();
		return requested.Count == variables.Count && requested.All(v => v != null) &&
			requested.Select(v => v.FeeHeadId).Distinct().Count() == requested.Count &&
			requested.All(v => variables.Any(h => h.FeeHeadId == v.FeeHeadId && h.EffectiveAmount == v.Amount &&
				string.Equals(h.ZeroAmountReason, v.ZeroAmountReason?.Trim(), StringComparison.Ordinal)));
	}

	[Authorize(ShikshaMitraSubmodulePermissions.Finance.Fees_FeeCollections.Add)]
	public async Task<CollectFeeOutput> CollectAsync(Guid applicationId, CollectAdmissionFeeInput input)
	{
		var source = await contextReader.GetAsync(applicationId);
		var account = await accounts.FirstOrDefaultAsync(a => a.ApplicationId == applicationId && a.AcademicYearId == source.AcademicYearId)
			?? throw new BusinessException(AdmissionFeeCollectionErrorCodes.NotPrepared);
		await scope.EnsureFeeAccountAccessibleAsync(account);
		if (account.AssignmentSnapshotJson == null || !SelectionMatches(source, ReadAssignment(account)))
			throw new BusinessException(AdmissionFeeCollectionErrorCodes.SelectionChanged);
		var command = AdmissionFeeCollectionInputMapper.Map(input, account);
		return await collector.CollectFeeAsync(command);
	}
}
