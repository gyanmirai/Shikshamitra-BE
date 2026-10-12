using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Students.StudentRecords;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Volo.Abp.BlobStoring;

namespace ShikshaMitra.Students.Records;

[RemoteService(false)]
[Authorize]
public partial class StudentIdCardAppService(IRepository<StudentIdCard, Guid> cards, IRepository<StudentIdCardTemplate, Guid> templates,
	IRepository<StudentRecordDocumentEvent, Guid> events, StudentDocumentSnapshotService snapshots, StudentRecordsAccessService access,
	StudentRecordsPolicyResolver policies, IBlobContainer blobs)
	: ApplicationService, IStudentIdCardAppService
{
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
	private static readonly string[] AllowedFields = ["StudentName", "GRNumber", "Class", "Division", "AcademicYear", "DateOfBirth"];
	public async Task<StudentIdCardTemplateDto> GetTemplateAsync()
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View); EnsureTenant();
		await access.EnsureActiveActorAsync();
		return await LoadTemplateAsync();
	}
	public async Task<List<StudentIdCardTemplateDto>> GetTemplatesAsync()
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View); EnsureTenant();
		await access.EnsureActiveActorAsync();
		var query = await templates.GetQueryableAsync();
		var items = await AsyncExecuter.ToListAsync(query.Where(t => t.TenantId == CurrentTenant.Id).OrderBy(t => t.CreationTime).ThenBy(t => t.Id));
		return items.Select(MapTemplate).ToList();
	}
	public async Task<StudentIdCardTemplateDto> GetTemplateByIdAsync(Guid id)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View); EnsureTenant();
		await access.EnsureActiveActorAsync();
		return await LoadTemplateAsync(id);
	}
	private static StudentIdCardTemplateDto MapTemplate(StudentIdCardTemplate template)
	{
		var dto = JsonSerializer.Deserialize<StudentIdCardTemplateDto>(template.ConfigurationJson, JsonOptions)!;
		dto.Selection ??= new();
		dto.Id = template.Id; dto.ConcurrencyStamp = template.ConcurrencyStamp;
		return dto;
	}
	private async Task<StudentIdCardTemplateDto> LoadTemplateAsync(Guid? templateId = null)
	{
		var query = await templates.GetQueryableAsync();
		var template = await AsyncExecuter.FirstOrDefaultAsync(query.Where(t => t.TenantId == CurrentTenant.Id &&
			(!templateId.HasValue || t.Id == templateId.Value)).OrderBy(t => t.CreationTime).ThenBy(t => t.Id));
		if (template == null && templateId.HasValue) throw new EntityNotFoundException(typeof(StudentIdCardTemplate), templateId.Value);
		return template == null ? new() { SchoolName = CurrentTenant.Name ?? "School" } : MapTemplate(template);
	}
	[UnitOfWork(true)]
	public async Task<StudentIdCardTemplateDto> CreateTemplateAsync(StudentIdCardTemplateDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Add); EnsureTenant();
		await access.EnsureActiveActorAsync(); ValidateTemplate(input);
		using var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		await EnsureStudentsAvailableAsync(input.Selection, null);
		input.Id = null; input.ConcurrencyStamp = null;
		var template = new StudentIdCardTemplate(GuidGenerator.Create(), CurrentTenant.Id!.Value, JsonSerializer.Serialize(input, JsonOptions));
		await templates.InsertAsync(template, autoSave: true);
		await work.CompleteAsync();
		return MapTemplate(template);
	}
	[UnitOfWork(true)]
	public async Task<StudentIdCardTemplateDto> UpdateTemplateAsync(Guid id, StudentIdCardTemplateDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Add); EnsureTenant();
		await access.EnsureActiveActorAsync(); ValidateTemplate(input);
		using var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
		var template = await templates.FindAsync(t => t.Id == id && t.TenantId == CurrentTenant.Id)
			?? throw new EntityNotFoundException(typeof(StudentIdCardTemplate), id);
		if (template.ConcurrencyStamp != input.ConcurrencyStamp) throw new BusinessException("Students:Records:IdTemplateConflict");
		await EnsureStudentsAvailableAsync(input.Selection, id);
		input.Id = null; input.ConcurrencyStamp = null;
		template.Update(JsonSerializer.Serialize(input, JsonOptions));
		await templates.UpdateAsync(template, autoSave: true);
		await work.CompleteAsync();
		return MapTemplate(template);
	}
	private async Task EnsureStudentsAvailableAsync(StudentIdCardSelectionDto selection, Guid? currentTemplateId)
	{
		if (selection.StudentIds.Count == 0) return;
		var selected = selection.StudentIds.ToHashSet();
		var query = await templates.GetQueryableAsync();
		var otherTemplates = await AsyncExecuter.ToListAsync(query.Where(t => t.TenantId == CurrentTenant.Id && t.Id != currentTemplateId));
		if (otherTemplates.Any(t =>
		{
			var existing = MapTemplate(t).Selection;
			return existing.AcademicYearId == selection.AcademicYearId && existing.StudentIds.Any(selected.Contains);
		})) throw new BusinessException("Students:Records:IdTemplateStudentConflict");
	}
	[UnitOfWork(true)]
	public async Task DeleteTemplateAsync(Guid id)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Delete); EnsureTenant();
		await access.EnsureActiveActorAsync();
		var template = await templates.FindAsync(t => t.Id == id && t.TenantId == CurrentTenant.Id)
			?? throw new EntityNotFoundException(typeof(StudentIdCardTemplate), id);
		await templates.DeleteAsync(template, autoSave: true);
	}
	[UnitOfWork(true)]
	public async Task<StudentIdCardTemplateDto> SaveTemplateAsync(StudentIdCardTemplateDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Add); EnsureTenant();
		await access.EnsureActiveActorAsync(); ValidateTemplate(input);
		var query = await templates.GetQueryableAsync();
		var template = await AsyncExecuter.FirstOrDefaultAsync(query.Where(t => t.TenantId == CurrentTenant.Id).OrderBy(t => t.CreationTime).ThenBy(t => t.Id));
		if (template == null) return await CreateTemplateAsync(input);
		return await UpdateTemplateAsync(template.Id, input);
	}
	private static void ValidateTemplate(StudentIdCardTemplateDto input)
	{
		if (input.Name == null || input.Name.Length is < 1 or > 150 || input.SchoolName == null || input.HeaderText == null || input.FooterText == null || input.PrimaryColor == null || input.BackgroundColor == null || input.SchoolAddress == null ||
			input.SchoolName.Length > 150 || input.SchoolAddress.Length > 300 || input.HeaderText.Length > 300 || input.FooterText.Length > 500 ||
			!new[] { "formal-student", "modern-student", "vertical-badge", "visitor-style", "minimal", "photo-first" }.Contains(input.PresetKey) ||
			!new[] { "Horizontal", "Vertical" }.Contains(input.Orientation) || input.CardsPerPage is < 1 or > 10 ||
			!Regex.IsMatch(input.PrimaryColor, "^#[0-9a-fA-F]{6}$") || !Regex.IsMatch(input.BackgroundColor, "^#[0-9a-fA-F]{6}$") || input.Fields == null || input.Fields.Count is < 1 or > 6 ||
			input.Fields.Distinct().Count() != input.Fields.Count || input.Fields.Any(f => !AllowedFields.Contains(f))) throw new BusinessException("Students:Records:InvalidIdTemplate");
		input.Selection ??= new();
		var selection = input.Selection;
		if (selection.BoardIds == null || selection.MediumIds == null || selection.StandardIds == null || selection.DivisionIds == null || selection.StudentIds == null ||
			new[] { selection.BoardIds, selection.MediumIds, selection.StandardIds, selection.DivisionIds, selection.StudentIds }.Any(ids =>
				ids.Count > 100 || ids.Contains(Guid.Empty) || ids.Distinct().Count() != ids.Count) ||
			(selection.StudentIds.Count > 0 && !selection.AcademicYearId.HasValue))
			throw new BusinessException("Students:Records:InvalidIdTemplate");
	}
	private static StudentIdCardTemplateDto ForCardSnapshot(StudentIdCardTemplateDto template)
	{
		var snapshot = JsonSerializer.Deserialize<StudentIdCardTemplateDto>(JsonSerializer.Serialize(template, JsonOptions), JsonOptions)!;
		snapshot.Selection = new();
		return snapshot;
	}
	public async Task<List<StudentIdCardDto>> PreviewAsync(StudentIdCardPreviewRequestDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View); EnsureTenant(); ValidateStudents(input);
		var template = ForCardSnapshot(await LoadTemplateAsync(input.TemplateId)); var result = new List<StudentIdCardDto>();
		foreach (var studentId in input.StudentIds)
		{
			var snapshot = await snapshots.CaptureAsync(studentId);
			result.Add(new() { StudentId = studentId, HasPhoto = !string.IsNullOrWhiteSpace(snapshot.Fields.GetValueOrDefault("_PhotoBlobName")), StudentSnapshot = PublicFields(snapshot.Fields), TemplateSnapshot = template });
		}
		return result;
	}
	public async Task<List<StudentIdCardDto>> IssueAsync(IssueStudentIdCardsDto input)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Add); EnsureTenant(); ValidateStudents(input);
		if (string.IsNullOrWhiteSpace(input.IdempotencyKey) || input.IdempotencyKey.Length > 60) throw new BusinessException("Students:Records:DocumentIdempotencyRequired");
		for (var attempt = 0; attempt < 3; attempt++)
		{
			try
			{
				using var uow = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true }, requiresNew: true);
				var template = ForCardSnapshot(await LoadTemplateAsync(input.TemplateId)); var result = new List<StudentIdCardDto>(); var sequences = new Dictionary<string, int>();
				foreach (var studentId in input.StudentIds)
				{
					var snapshot = await snapshots.CaptureAsync(studentId);
					var key = $"{input.IdempotencyKey.Trim()}:{studentId:N}";
					var existing = await cards.FindAsync(c => c.TenantId == CurrentTenant.Id && c.IdempotencyKey == key);
					if (existing != null) { await EnsureIssuedYearVisibleAsync(existing.AcademicYearId); result.Add(Map(existing)); continue; }
					var prefix = $"{snapshot.Policy.Documents.IdNumberPrefix}-{Clock.Now.Year}-";
					if (!sequences.TryGetValue(prefix, out var sequence))
					{
						var numbers = await AsyncExecuter.ToListAsync((await cards.GetQueryableAsync()).Where(c => c.TenantId == CurrentTenant.Id && c.CardNumber.StartsWith(prefix)).Select(c => c.CardNumber));
						sequence = numbers.Select(n => int.TryParse(n[prefix.Length..], out var parsed) ? parsed : 0).DefaultIfEmpty().Max();
					}
					sequences[prefix] = ++sequence;
					var card = new StudentIdCard(GuidGenerator.Create(), CurrentTenant.Id!.Value, studentId, $"{prefix}{sequence:000000}", key,
						JsonSerializer.Serialize(snapshot.Fields, JsonOptions), JsonSerializer.Serialize(template, JsonOptions), Clock.Now, CurrentUser.Id!.Value, snapshot.AcademicYearId);
					await cards.InsertAsync(card); await AddEventAsync(card, "Issued", null); result.Add(Map(card));
				}
				await uow.CompleteAsync(); return result;
			}
			catch (Exception ex) when (IsRetryable(ex))
			{
				if (attempt == 2) throw new BusinessException("Students:Records:IdNumberConflict");
			}
		}
		throw new BusinessException("Students:Records:IdNumberConflict");
	}
	public async Task<List<StudentIdCardDto>> GetHistoryAsync(Guid studentId)
	{
		await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View); EnsureTenant();
		await snapshots.CaptureAsync(studentId);
		var history = await AsyncExecuter.ToListAsync((await cards.GetQueryableAsync()).Where(c => c.TenantId == CurrentTenant.Id && c.StudentId == studentId).OrderByDescending(c => c.CreationTime));
		foreach (var year in history.Select(c => c.AcademicYearId).Distinct()) await EnsureIssuedYearVisibleAsync(year);
		return history.Select(Map).ToList();
	}
	private async Task<StudentIdCard> LoadCardAsync(Guid id)
	{
		EnsureTenant(); var card = await cards.FindAsync(c => c.Id == id && c.TenantId == CurrentTenant.Id) ?? throw new EntityNotFoundException(typeof(StudentIdCard), id);
		await snapshots.CaptureAsync(card.StudentId); await EnsureIssuedYearVisibleAsync(card.AcademicYearId); return card;
	}
	private async Task EnsureIssuedYearVisibleAsync(Guid? academicYearId)
	{
		if ((await policies.ResolveAsync(academicYearId)).Policy.IsMasked("Identity", await access.GetDatabaseRoleNamesAsync()))
			throw new BusinessException("Students:Records:DocumentFieldsRestricted");
	}
	private Task AddEventAsync(StudentIdCard card, string action, string? reason) => events.InsertAsync(new StudentRecordDocumentEvent(GuidGenerator.Create(), CurrentTenant.Id!.Value, card.StudentId, card.Id, CurrentUser.Id!.Value, "ID", action, reason, card.AcademicYearId));
	private void EnsureTenant() { if (!CurrentTenant.Id.HasValue || !CurrentUser.Id.HasValue) throw new BusinessException("Students:Records:TenantRequired"); }
	private static void ValidateStudents(StudentIdCardPreviewRequestDto input) { if (input.StudentIds == null || input.StudentIds.Count is < 1 or > 100 || input.StudentIds.Any(id => id == Guid.Empty) || input.StudentIds.Distinct().Count() != input.StudentIds.Count) throw new BusinessException("Students:Records:InvalidIdStudentSelection"); }
	private static Dictionary<string, string?> PublicFields(Dictionary<string, string?> snapshot) => snapshot.Where(x => !x.Key.StartsWith("_", StringComparison.Ordinal)).ToDictionary(x => x.Key, x => x.Value);
	private static StudentIdCardDto Map(StudentIdCard card)
	{
		var snapshot = JsonSerializer.Deserialize<Dictionary<string, string?>>(card.StudentSnapshotJson, JsonOptions)!;
		return new() { Id = card.Id, StudentId = card.StudentId, CardNumber = card.CardNumber, Status = card.Status,
			IssuedAt = card.IssuedAt, IssuedByUserId = card.IssuedByUserId, HasPhoto = !string.IsNullOrWhiteSpace(snapshot.GetValueOrDefault("_PhotoBlobName")),
			StudentSnapshot = PublicFields(snapshot), TemplateSnapshot = JsonSerializer.Deserialize<StudentIdCardTemplateDto>(card.TemplateSnapshotJson, JsonOptions)! };
	}
	private static bool IsRetryable(Exception ex) => ex.ToString().Contains("23505", StringComparison.Ordinal) || ex.ToString().Contains("unique constraint", StringComparison.OrdinalIgnoreCase) || ex.ToString().Contains("database is locked", StringComparison.OrdinalIgnoreCase);
}
