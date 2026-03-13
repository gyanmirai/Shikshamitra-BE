using System;
using Volo.Abp.Domain.Repositories;

namespace ShikshaMitra.HR.Employees.Settings;

public interface IDocumentSettingRepository : IRepository<DocumentSetting, Guid>;
