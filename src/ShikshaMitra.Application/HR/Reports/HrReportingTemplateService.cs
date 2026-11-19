using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.HR.Audit;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Reports;

[RemoteService(false)]
[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.View)]
public class HrReportingTemplateService(IRepository<HrReportTemplate, Guid> repository, HrReportingAccess access,
	IPermissionChecker permissions, HrAuditWriter audit) : ApplicationService, IHrReportingTemplateService
{
	public async Task<List<HrReportLibraryTemplateDto>> GetLibraryAsync(bool edit = false)
	{
		var user = CurrentUser.Id ?? throw new AbpAuthorizationException();
		var canManage = await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Reports.ManageTemplates);
		var canBuild = await permissions.IsGrantedAsync(ShikshaMitraSubmodulePermissions.HR.Reports.Build);
		var allowed = new HashSet<string>();
		foreach (var dataset in HrReportingCatalogue.Datasets()) if (await access.CanReadAsync(dataset.Id, user)) allowed.Add(dataset.Id);
		var result = HrReportingCatalogue.BuiltIns().Where(x => allowed.Contains(x.Definition.DataSource)).ToList();
		var rows = await AsyncExecuter.ToListAsync((await repository.GetQueryableAsync()).Where(x => x.DefinitionVersion == 2 && x.Status != "Archived" &&
			(x.OwnerUserId == user || (x.Visibility == "organization" && (x.PublishedRevision > 0 || canManage)))));
		result.AddRange(rows.Where(x => allowed.Contains(x.DataSource)).Select(x => Map(x, x.Visibility == "organization" ? canManage : x.OwnerUserId == user && canBuild, edit)));
		if (!await access.CanReadConfidentialAsync(user)) result.RemoveAll(x => x.Definition.DataSource == "performance" && HrReportingAccess.UsesConfidentialOutcome(x.Definition));
		return result.OrderBy(x => x.Name).ToList();
	}
	public async Task<HrReportLibraryTemplateDto> SaveAsync(Guid? id, HrReportSaveDesignDto input)
	{
		input.Description ??= "";
		var user = CurrentUser.Id ?? throw new AbpAuthorizationException();
		var dataset = HrReportingEngine.Validate(input.Definition);
		await access.EnsureDesignAsync(input.Definition, user);
		if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 128 || input.Description.Length > 512 || input.Visibility is not ("private" or "organization")) throw HrReportingEngine.Invalid("Enter a name up to 128 characters, a description up to 512 characters, and valid visibility.");
		if (input.Visibility == "organization") await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.Reports.ManageTemplates);
		else await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.Reports.Build);
		HrReportTemplate template;
		if (id.HasValue)
		{
			template = await repository.GetAsync(id.Value);
			await EnsureEditableAsync(template);
			if (string.IsNullOrEmpty(input.ConcurrencyStamp) || input.ConcurrencyStamp != template.ConcurrencyStamp) throw new BusinessException(HRErrorCodes.ReportTemplateConflict);
			template.ConcurrencyStamp = input.ConcurrencyStamp;
		}
		else template = new HrReportTemplate(GuidGenerator.Create(), CurrentTenant.Id, user, input.Name, input.Description, dataset.Category, dataset.Id, input.Visibility, "Draft", "{}");
		template.SaveDesign(input.Name, input.Description, dataset.Category, dataset.Id, input.Visibility, JsonSerializer.Serialize(input.Definition, HrReportingEngine.Json), input.Publish);
		if (id.HasValue) await repository.UpdateAsync(template, true); else await repository.InsertAsync(template, true);
		await audit.WriteAsync(null, "hr_report_template", template.Id, "HR report template", input.Publish ? "template_published" : "template_saved", template.Name);
		return Map(template, true, true);
	}
	[Authorize(ShikshaMitraSubmodulePermissions.HR.Reports.Delete)]
	public async Task ArchiveAsync(Guid id, string concurrencyStamp)
	{
		var template = await repository.GetAsync(id); await EnsureEditableAsync(template);
		if (template.ConcurrencyStamp != concurrencyStamp) throw new BusinessException(HRErrorCodes.ReportTemplateConflict);
		template.ArchiveDesign(); await repository.UpdateAsync(template, true);
		await audit.WriteAsync(null, "hr_report_template", id, "HR report template", "template_archived", template.Name);
	}
	public async Task<HrReportLibraryTemplateDto> ResolveAsync(string id, int revision)
	{
		var template = (await GetLibraryAsync()).SingleOrDefault(x => x.Id == id) ?? throw new AbpAuthorizationException("Template is unavailable.");
		if (revision != template.Revision || template.PublishedRevision == 0) throw new BusinessException(HRErrorCodes.ReportTemplateConflict);
		return template;
	}
	private async Task EnsureEditableAsync(HrReportTemplate template)
	{
		if (template.DefinitionVersion != 2 || template.TenantId != CurrentTenant.Id || template.Status == "Archived") throw new AbpAuthorizationException();
		if (template.Visibility == "organization") await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.Reports.ManageTemplates);
		else if (template.OwnerUserId != CurrentUser.Id) throw new AbpAuthorizationException();
		else await AuthorizationService.CheckAsync(ShikshaMitraSubmodulePermissions.HR.Reports.Build);
		await access.EnsureAsync(template.DataSource, CurrentUser.Id!.Value);
	}
	private static HrReportLibraryTemplateDto Map(HrReportTemplate x, bool editable, bool edit)
	{
		var showDraft = edit && editable;
		return new()
		{
			Id = x.Id.ToString(),
			Name = showDraft ? x.Name : x.PublishedName ?? x.Name,
			Description = showDraft ? x.Description : x.PublishedDescription ?? x.Description,
			Category = x.Category,
			Visibility = x.Visibility,
			Status = x.Status,
			Revision = showDraft ? x.Revision : x.PublishedRevision,
			PublishedRevision = x.PublishedRevision,
			ConcurrencyStamp = x.ConcurrencyStamp,
			OwnerUserId = x.OwnerUserId,
			CanEdit = editable,
			Definition = JsonSerializer.Deserialize<HrReportDesignDto>(showDraft ? x.DefinitionJson : x.PublishedDefinitionJson ?? x.DefinitionJson, HrReportingEngine.Json)!
		};
	}
}