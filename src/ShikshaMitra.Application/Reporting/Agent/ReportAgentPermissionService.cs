using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Academic.Reports;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.DependencyInjection;

namespace ShikshaMitra.Reporting.Agent;

public class ReportAgentPermissionService(IAuthorizationService authorizationService) : ITransientDependency
{
	private static readonly IReadOnlyDictionary<string, string> ModuleIds =
		new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
		{
			[AcademicReportModules.Curriculum] = "academic.curriculum",
			[AcademicReportModules.HomeworkAssignment] = "academic.homework-assignment",
			[AcademicReportModules.Homework] = "academic.homework-assignment",
			[AcademicReportModules.Assignment] = "academic.homework-assignment",
			[AcademicReportModules.Examination] = "academic.examination",
			[AcademicReportModules.Timetable] = "academic.timetable",
			[AcademicReportModules.LearningManagement] = "academic.lms",
			[AcademicReportModules.StudentRecords] = "students.records",
			[AcademicReportModules.StudentAttendance] = "students.attendance",
			[AcademicReportModules.HumanResources] = "hr.reports",
			[AcademicReportModules.Finance] = "finance.reports",
			[AcademicReportModules.Library] = "library.reports",
			[AcademicReportModules.Transport] = "transport.reports"
		};

	public Task EnsureCanBuildAsync(string module) => EnsureAsync(module, definition => definition.BuildPermission);
	public Task EnsureCanExportAsync(string module) => EnsureAsync(module, definition => definition.ExportPermission);

	public ReportAgentModuleDefinition GetAvailableModule(string module)
	{
		var normalized = AcademicDynamicReportGuard.NormalizeModule(module);
		if (!ModuleIds.TryGetValue(normalized, out var id))
		{
			throw new BusinessException("Academic:Reports:AiModuleNotAvailable").WithData("Module", normalized);
		}
		var definition = ReportAgentModuleCatalog.All.Single(item => item.Id == id);
		if (definition.Availability != ReportAgentModuleAvailability.Available)
		{
			throw new BusinessException("Academic:Reports:AiModuleNotAvailable").WithData("Module", normalized);
		}
		return definition;
	}

	private async Task EnsureAsync(string module, Func<ReportAgentModuleDefinition, string> permissionSelector)
	{
		var definition = GetAvailableModule(module);
		var permission = permissionSelector(definition);
		if (!await authorizationService.IsGrantedAsync(permission))
		{
			throw new AbpAuthorizationException($"Required report permission: {permission}");
		}
	}
}
