using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using ShikshaMitra.Constants.Permissions;
using ShikshaMitra.Security.UserScope;
using ShikshaMitra.TenantSettings.AcademicSetups.EducationBoards;
using ShikshaMitra.TenantSettings.EducationBoards;
using ShikshaMitra.TenantSettings.EducationMediums;
using ShikshaMitra.TenantSettings.Standards;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.Students.StudentEnrollments.AdmissionsCrm;

public class AdmissionCrmLeadInquirySourceReader(
	IRepository<AdmissionCrmInquirySource, Guid> repository) : ApplicationService, ITransientDependency
{
	internal async Task<string?> GetInquirySourceSnapshotAsync(Guid? inquirySourceId)
	{
		if (!inquirySourceId.HasValue)
		{
			return null;
		}

		var inquirySource = await repository.GetAsync(inquirySourceId.Value);
		return inquirySource.DisplayName;
	}
}