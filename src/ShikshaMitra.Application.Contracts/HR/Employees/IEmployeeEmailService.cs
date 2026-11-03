using System.Threading.Tasks;

namespace ShikshaMitra.HR.Employees;

/// <summary>
/// Interface for employee-related email communications.
/// Defined in Application.Contracts to allow decoupling and easier testing.
/// </summary>
public interface IEmployeeEmailService
{
    /// <summary>
    /// Sends welcome email to newly created employee with login credentials.
    /// </summary>
    /// <param name="employeeEmail">Employee's email address</param>
    /// <param name="employeeName">Employee's full name</param>
    /// <param name="username">Generated username</param>
    /// <param name="password">Generated password</param>
    /// <returns>Task representing the async operation</returns>
    Task SendWelcomeEmailAsync(
        string? employeeEmail,
        string employeeName,
        string username,
        string password);
}
