using System;
using System.Threading.Tasks;
namespace ShikshaMitra.HR.WorkShifts;
public interface IEmployeeShiftWriteFence { Task LockAsync(Guid employeeId); }