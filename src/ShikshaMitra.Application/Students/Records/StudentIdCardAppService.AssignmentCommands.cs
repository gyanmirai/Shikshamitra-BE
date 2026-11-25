using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Records;

public partial class StudentIdCardAppService
{
	public async Task<StudentIdCardAssignmentDto> SaveAssignmentAsync(Guid academicYearId, Guid standardId, SaveStudentIdCardAssignmentDto input)
	{
		await AuthorizeAssignmentAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Add);
		if (input.TemplateId == Guid.Empty || input.ConcurrencyStamp?.Length > 64)
			throw new BusinessException(StudentErrorCodes.InvalidIdAssignment);
		try
		{
			using var work = BeginAssignmentTransaction();
			var result = await EnsureAssignmentClassAsync(academicYearId, standardId, manage: true);
			var rule = await FindAssignmentAsync(academicYearId, standardId);
			if (rule?.ConcurrencyStamp != input.ConcurrencyStamp)
				throw new BusinessException(StudentErrorCodes.IdAssignmentChanged);
			var template = await FindTemplateRowAsync(input.TemplateId)
				?? throw new EntityNotFoundException(typeof(StudentIdCardTemplate), input.TemplateId);
			if (rule == null)
			{
				rule = new StudentIdCardAssignment(GuidGenerator.Create(), CurrentTenant.Id!.Value, academicYearId, standardId, template.Id);
				await Assignments.InsertAsync(rule, autoSave: true);
			}
			else if (rule.TemplateId != template.Id)
			{
				rule.Assign(template.Id);
				await Assignments.UpdateAsync(rule, autoSave: true);
			}
			SetAssignment(result, rule, template);
			await work.CompleteAsync();
			return result;
		}
		catch (Exception ex) when (IsAssignmentTransactionConflict(ex))
		{
			throw new BusinessException(StudentErrorCodes.IdAssignmentChanged, innerException: ex);
		}
	}

	public async Task DeleteAssignmentAsync(Guid academicYearId, Guid standardId, string? concurrencyStamp)
	{
		await AuthorizeAssignmentAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Delete);
		try
		{
			using var work = BeginAssignmentTransaction();
			await EnsureAssignmentClassAsync(academicYearId, standardId, manage: true);
			var rule = await FindAssignmentAsync(academicYearId, standardId);
			if (rule == null || rule.ConcurrencyStamp != concurrencyStamp)
				throw new BusinessException(StudentErrorCodes.IdAssignmentChanged);
			await Assignments.DeleteAsync(rule, autoSave: true);
			await work.CompleteAsync();
		}
		catch (Exception ex) when (IsAssignmentTransactionConflict(ex))
		{
			throw new BusinessException(StudentErrorCodes.IdAssignmentChanged, innerException: ex);
		}
	}

	public async Task<List<StudentIdCardDto>> PreviewAssignmentAsync(Guid academicYearId, Guid standardId, StudentIdCardAssignmentPreviewDto input)
	{
		await AuthorizeAssignmentAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View);
		return await UseAssignmentAsync(academicYearId, standardId, input, template =>
			PreviewCoreAsync(new StudentIdCardPreviewRequestDto { StudentIds = input.StudentIds }, template));
	}

	public async Task<StudentCertificatePdfDto> ExportAssignmentAsync(Guid academicYearId, Guid standardId, StudentIdCardAssignmentExportDto input)
	{
		await AuthorizeAssignmentAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Add);
		return await UseAssignmentAsync(academicYearId, standardId, input, template =>
			ExportCoreAsync(new StudentIdCardsPdfRequestDto
			{
				StudentIds = input.StudentIds,
				PhotoEdits = input.PhotoEdits,
				CardsPerPage = input.CardsPerPage ?? template.CardsPerPage
			}, template));
	}

	public async Task<List<StudentIdCardDto>> IssueAssignmentAsync(Guid academicYearId, Guid standardId, StudentIdCardAssignmentIssueDto input)
	{
		await AuthorizeAssignmentAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Add);
		ValidateIdempotencyKey(input.IdempotencyKey);
		var keyData = Encoding.UTF8.GetBytes($"{academicYearId:N}:{standardId:N}:{input.IdempotencyKey.Trim()}");
		var batchKey = "IC-" + Convert.ToHexString(SHA256.HashData(keyData));
		return await UseAssignmentAsync(academicYearId, standardId, input,
			template => IssueAssignmentBatchAsync(academicYearId, input, batchKey, template), retry: true);
	}

	private async Task<List<StudentIdCardDto>> IssueAssignmentBatchAsync(Guid year, StudentIdCardAssignmentIssueDto input,
		string batchKey, StudentIdCardTemplateDto template)
	{
		var keyPrefix = batchKey + ":";
		var issued = await AsyncExecuter.ToListAsync((await cards.GetQueryableAsync()).Where(card =>
			card.TenantId == CurrentTenant.Id && card.IdempotencyKey.StartsWith(keyPrefix)));
		var fingerprint = ComputeAssignmentFingerprint(input);
		if (issued.Count > 0)
		{
			var issuedByStudent = issued.ToDictionary(card => card.StudentId);
			using var snapshot = JsonDocument.Parse(issued[0].StudentSnapshotJson);
			if (issued.Count != input.StudentIds.Count || input.StudentIds.Any(id => !issuedByStudent.ContainsKey(id)) ||
				!snapshot.RootElement.TryGetProperty(AssignmentFingerprintField, out var saved) || saved.GetString() != fingerprint)
				throw new BusinessException(StudentErrorCodes.IdAssignmentIssueKeyReused);
			return await RedactCardSignaturesAsync(input.StudentIds.Select(id => Map(issuedByStudent[id])).ToList());
		}
		var selectedIds = input.StudentIds.ToArray();
		if (await AsyncExecuter.AnyAsync((await GetValidAssignmentCardsAsync(year)).Where(card => Enumerable.Contains(selectedIds, card.StudentId))))
			throw new BusinessException(StudentErrorCodes.IdAssignmentAlreadyIssued);
		return await IssueCoreAsync(new IssueStudentIdCardsDto
		{
			StudentIds = input.StudentIds,
			IdempotencyKey = batchKey,
			PhotoEdits = input.PhotoEdits
		}, template, fingerprint);
	}

	private static string ComputeAssignmentFingerprint(StudentIdCardAssignmentIssueDto input)
	{
		using var hash = SHA256.Create();
		using var stream = new CryptoStream(Stream.Null, hash, CryptoStreamMode.Write);
		JsonSerializer.Serialize(stream, new
		{
			input.ConcurrencyStamp,
			input.TemplateConcurrencyStamp,
			StudentIds = input.StudentIds.Order().ToArray(),
			PhotoEdits = input.PhotoEdits?.OrderBy(edit => edit?.RecordId).ToArray() ?? []
		}, JsonOptions);
		stream.FlushFinalBlock();
		return Convert.ToHexString(hash.Hash!);
	}

	private async Task<T> UseAssignmentAsync<T>(Guid year, Guid standard, StudentIdCardAssignmentPreviewDto input,
		Func<StudentIdCardTemplateDto, Task<T>> perform, bool retry = false)
	{
		ValidateStudents(new StudentIdCardPreviewRequestDto { StudentIds = input.StudentIds });
		for (var attempt = 0; ; attempt++)
		{
			try
			{
				using var work = BeginAssignmentTransaction();
				await EnsureAssignmentClassAsync(year, standard);
				var rule = await FindAssignmentAsync(year, standard);
				if (rule == null || rule.ConcurrencyStamp != input.ConcurrencyStamp)
					throw new BusinessException(StudentErrorCodes.IdAssignmentChanged);
				var template = await FindTemplateRowAsync(rule.TemplateId);
				if (template == null || template.ConcurrencyStamp != input.TemplateConcurrencyStamp)
					throw new BusinessException(StudentErrorCodes.IdAssignmentChanged);
				var selectedIds = input.StudentIds.ToArray();
				var population = await GetAssignmentStudentPopulationAsync(year, standard);
				if (await AsyncExecuter.CountAsync(population.Where(student => Enumerable.Contains(selectedIds, student.Id))) != selectedIds.Length)
					throw new BusinessException(StudentErrorCodes.IdAssignmentRosterChanged);
				var result = await perform(MapTemplate(template));
				await work.CompleteAsync();
				return result;
			}
			catch (Exception ex) when (IsAssignmentTransactionConflict(ex))
			{
				if (!retry || attempt == 2) throw new BusinessException(StudentErrorCodes.IdAssignmentChanged, innerException: ex);
			}
		}
	}

	private async Task<StudentIdCardAssignment?> FindAssignmentAsync(Guid year, Guid standard) =>
		await AsyncExecuter.FirstOrDefaultAsync((await GetAssignmentRowsAsync(year)).Where(rule => rule.StandardId == standard));

	private async Task EnsureTemplateHasNoLiveAssignmentsAsync(Guid templateId)
	{
		// This predicate read and assignment creation's template read share the serializable integrity check.
		var assignments = (await Assignments.GetQueryableAsync())
			.Where(rule => rule.TenantId == CurrentTenant.Id && rule.TemplateId == templateId);
		var assigned = await AsyncExecuter.ToListAsync(assignments);
		if (assigned.Count == 0) return;
		var standards = await LazyServiceProvider.LazyGetRequiredService<IRepository<Standard, Guid>>().GetQueryableAsync();
		var liveStandards = standards.Where(standard => standard.TenantId == CurrentTenant.Id &&
			standard.EducationMediumStage.TenantId == CurrentTenant.Id &&
			standard.EducationMediumStage.EducationMedium.TenantId == CurrentTenant.Id &&
			standard.EducationMediumStage.EducationMedium.EducationBoard.TenantId == CurrentTenant.Id &&
			standard.EducationMediumStage.EducationMedium.EducationBoard.AcademicYear.TenantId == CurrentTenant.Id);
		if (await AsyncExecuter.AnyAsync(assignments.Where(rule => liveStandards.Any(standard =>
			standard.Id == rule.StandardId && standard.EducationMediumStage.EducationMedium.EducationBoard.AcademicYearId == rule.AcademicYearId))))
			throw new BusinessException(StudentErrorCodes.IdTemplateAssigned);
		// Academic setup can remove an unused class independently. Keep its old rule in the audit trail.
		await Assignments.DeleteManyAsync(assigned, autoSave: true);
	}

	private IUnitOfWork BeginAssignmentTransaction() => UnitOfWorkManager.Begin(
		new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);

	private async Task AuthorizeAssignmentAsync(string permission)
	{
		await AuthorizationService.CheckAsync(permission);
		EnsureTenant();
		// The class scope lookup validates the active database actor inside the command unit of work.
	}

	private static bool IsAssignmentTransactionConflict(Exception exception)
	{
		for (Exception? current = exception; current != null; current = current.InnerException)
			if (current is AbpDbConcurrencyException || current is DbException { SqlState: "40001" or "40P01" or "23505" })
				return true;
		return false;
	}
}
