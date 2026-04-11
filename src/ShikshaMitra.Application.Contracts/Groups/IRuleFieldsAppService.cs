using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace ShikshaMitra.Application.Contracts.Groups;

/// <summary>
/// Provides metadata about rule fields (which properties, operators, and options are available)
/// </summary>
public interface IRuleFieldsAppService : IApplicationService
{
    /// <summary>
    /// Returns the available rule fields for student filtering with their applicable operators and options
    /// </summary>
    Task<List<RuleFieldDto>> GetStudentRuleFieldsAsync();

    /// <summary>
    /// Returns the available rule fields for employee filtering with their applicable operators and options
    /// </summary>
    Task<List<RuleFieldDto>> GetEmployeeRuleFieldsAsync();
}
