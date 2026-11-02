using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.TenantSettings.SchoolIdentity;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Uow;

namespace ShikshaMitra.Students.Records;

public partial class StudentIdCardAppService
{
	public async Task<List<StudentIdCardTemplateDto>> GetTemplatesAsync()
	{
		await EnsureTemplateActorAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View);
		return await VisibleTemplatesAsync((await ReadTemplateRowsAsync()).Select(MapTemplate).ToList());
	}
	public async Task<StudentIdCardTemplateDto> GetTemplateByIdAsync(Guid id)
	{
		await EnsureTemplateActorAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View);
		return (await VisibleTemplatesAsync([await LoadTemplateAsync(id)])).Single();
	}
	public async Task<StudentIdCardTemplateDto> GetTemplateAsync()
	{
		await EnsureTemplateActorAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.View);
		return (await VisibleTemplatesAsync([await LoadTemplateAsync()])).Single();
	}
	public async Task<StudentIdCardTemplateDto> CreateTemplateAsync(StudentIdCardTemplateDto input)
	{
		await EnsureTemplateActorAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Add);
		return await SaveTemplateCoreAsync(null, input, legacy: false);
	}
	public async Task<StudentIdCardTemplateDto> UpdateTemplateAsync(Guid id, StudentIdCardTemplateDto input)
	{
		await EnsureTemplateActorAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Add);
		return await SaveTemplateCoreAsync(id, input, legacy: false);
	}
	public async Task<StudentIdCardTemplateDto> SaveTemplateAsync(StudentIdCardTemplateDto input)
	{
		await EnsureTemplateActorAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Add);
		return await SaveTemplateCoreAsync(null, input, legacy: true);
	}
	public async Task DeleteTemplateAsync(Guid id)
	{
		await EnsureTemplateActorAsync(ShikshaMitraSubmodulePermissions.Students.Records_StudentICard.Delete);
		try
		{
			using var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
			var row = await FindTemplateRowAsync(id) ?? throw new EntityNotFoundException(typeof(StudentIdCardTemplate), id);
			await ValidateAffectedMembersAsync(MapTemplate(row).Selection, null);
			await templates.DeleteAsync(row, autoSave: true);
			await work.CompleteAsync();
		}
		catch (Exception ex) when (IsTemplateSerializationConflict(ex)) { throw new AbpDbConcurrencyException("The template catalogue changed. Reload before saving.", ex); }
	}
	private async Task<StudentIdCardTemplateDto> SaveTemplateCoreAsync(Guid? id, StudentIdCardTemplateDto input, bool legacy)
	{
		ValidateTemplate(input);
		var draft = JsonSerializer.Deserialize<StudentIdCardTemplateDto>(JsonSerializer.Serialize(input, JsonOptions), JsonOptions)!;
		try
		{
			using var work = UnitOfWorkManager.Begin(new AbpUnitOfWorkOptions { IsTransactional = true, IsolationLevel = IsolationLevel.Serializable }, requiresNew: true);
			var transferring = draft.Selection?.StudentIds.Count > 0;
			List<StudentIdCardTemplate> rows = transferring ? await ReadTemplateRowsAsync() : [];
			StudentIdCardTemplate? row = null;
			if (id.HasValue || legacy)
				row = transferring ? rows.FirstOrDefault(t => !id.HasValue || t.Id == id) : await FindTemplateRowAsync(id);
			if (id.HasValue && row == null) throw new EntityNotFoundException(typeof(StudentIdCardTemplate), id);
			if (row != null && row.ConcurrencyStamp != input.ConcurrencyStamp) throw new BusinessException("Students:Records:IdTemplateConflict");
			var previous = row == null ? null : MapTemplate(row).Selection;
			if (draft.Selection == null) draft.Selection = previous ?? new();
			else
			{
				await ValidateAffectedMembersAsync(previous, draft.Selection);
			}
			draft.Id = null; draft.ConcurrencyStamp = null; draft.SchoolIdentity = null; draft.SelectionRestricted = false;
			await identityAssets.ValidateAndRetainAsync(draft.Logo, SchoolIdentityAssetKind.Logo);
			if (input.Selection != null && draft.Selection.StudentIds.Count > 0)
			{
				var selected = draft.Selection.StudentIds.ToHashSet();
				foreach (var otherRow in rows.Where(t => t.Id != row?.Id))
				{
					var other = MapTemplate(otherRow);
					if (other.Selection!.AcademicYearId != draft.Selection.AcademicYearId || !other.Selection.StudentIds.Any(selected.Contains)) continue;
					other.Selection.StudentIds.RemoveAll(selected.Contains); other.Id = null; other.ConcurrencyStamp = null; other.SelectionRestricted = false;
					otherRow.Update(JsonSerializer.Serialize(other, JsonOptions)); await templates.UpdateAsync(otherRow, autoSave: true);
				}
			}
			if (row == null)
			{
				row = new StudentIdCardTemplate(GuidGenerator.Create(), CurrentTenant.Id!.Value, JsonSerializer.Serialize(draft, JsonOptions));
				await templates.InsertAsync(row, autoSave: true);
			}
			else { row.Update(JsonSerializer.Serialize(draft, JsonOptions)); await templates.UpdateAsync(row, autoSave: true); }
			var result = (await VisibleTemplatesAsync([MapTemplate(row)])).Single();
			await work.CompleteAsync(); return result;
		}
		catch (Exception ex) when (IsTemplateSerializationConflict(ex)) { throw new AbpDbConcurrencyException("The template catalogue changed. Reload before saving.", ex); }
	}
	private async Task EnsureTemplateActorAsync(string permission)
	{
		await AuthorizationService.CheckAsync(permission); EnsureTenant(); await access.EnsureActiveActorAsync();
	}
	private async Task<List<StudentIdCardTemplate>> ReadTemplateRowsAsync() => await AsyncExecuter.ToListAsync(
		(await templates.GetQueryableAsync()).Where(t => t.TenantId == CurrentTenant.Id).OrderBy(t => t.CreationTime).ThenBy(t => t.Id));
	private async Task<StudentIdCardTemplate?> FindTemplateRowAsync(Guid? id) => await AsyncExecuter.FirstOrDefaultAsync(
		(await templates.GetQueryableAsync()).Where(t => t.TenantId == CurrentTenant.Id && (!id.HasValue || t.Id == id)).OrderBy(t => t.CreationTime).ThenBy(t => t.Id));
	private async Task<StudentIdCardTemplateDto> LoadTemplateAsync(Guid? id = null)
	{
		if (id == Guid.Empty) throw new BusinessException("Students:Records:InvalidIdTemplate");
		var row = await FindTemplateRowAsync(id);
		if (row != null) return MapTemplate(row);
		if (id.HasValue) throw new EntityNotFoundException(typeof(StudentIdCardTemplate), id);
		return new() { SchoolName = CurrentTenant.Name ?? "School", Selection = new() };
	}
	private static StudentIdCardTemplateDto MapTemplate(StudentIdCardTemplate row)
	{
		var dto = JsonSerializer.Deserialize<StudentIdCardTemplateDto>(row.ConfigurationJson, JsonOptions)!;
		dto.Selection ??= new(); dto.Id = row.Id; dto.ConcurrencyStamp = row.ConcurrencyStamp; dto.SelectionRestricted = false; return dto;
	}
	private static void ValidateTemplate(StudentIdCardTemplateDto input)
	{
		if (input == null || input.Name == null || input.Name.Length is < 1 or > 150 || input.SchoolName == null || input.HeaderText == null || input.FooterText == null || input.PrimaryColor == null || input.BackgroundColor == null || input.SchoolAddress == null ||
			input.SchoolName.Length > 150 || input.SchoolAddress.Length > 300 || input.HeaderText.Length > 300 || input.FooterText.Length > 500 ||
			!new[] { "formal-student", "modern-student", "vertical-badge", "visitor-style", "minimal", "photo-first" }.Contains(input.PresetKey) ||
			!new[] { "Horizontal", "Vertical" }.Contains(input.Orientation) || input.CardsPerPage is < 1 or > 10 ||
			!Regex.IsMatch(input.PrimaryColor, "^#[0-9a-fA-F]{6}$") || !Regex.IsMatch(input.BackgroundColor, "^#[0-9a-fA-F]{6}$") || input.Fields == null || input.Fields.Count is < 1 or > 6 ||
			input.Fields.Distinct().Count() != input.Fields.Count || input.Fields.Any(f => !AllowedFields.Contains(f))) throw new BusinessException("Students:Records:InvalidIdTemplate");
		if (input.Selection is { } selection && (!ValidSelectionMembers(selection) || new[] { selection.BoardIds, selection.MediumIds, selection.StandardIds, selection.DivisionIds, selection.StudentIds }.Any(ids => ids.Count > 100)))
			throw new BusinessException("Students:Records:InvalidIdTemplate");
	}
	private static bool IsTemplateSerializationConflict(Exception exception)
	{
		for (Exception? current = exception; current != null; current = current.InnerException)
			if (current.GetType().GetProperty("SqlState")?.GetValue(current) is string code && code is "40001" or "40P01") return true;
		return false;
	}
}
