namespace BaseLibrary.DTOs;

/// <summary>
/// Published to the RabbitMQ topic exchange on routing key "ems.employee.created"
/// whenever a new Employee is persisted.
///
/// EmployeeId is typed as int (not string) — consumers must deserialise it as a
/// JSON number.  Changing this to string is a breaking schema change.
/// </summary>
public sealed record EmployeeCreatedEvent(
    int      EmployeeId,
    string?  Name,
    string?  JobName,
    int      BranchId,
    int      TownId,
    DateTime Timestamp);

/// <summary>
/// Published to "ems.employee.updated" when any field on an Employee changes.
/// Changes is a list of { Field, OldValue, NewValue } objects.
/// EmployeeId is int — not string.
/// </summary>
public sealed record EmployeeUpdatedEvent(
    int      EmployeeId,
    DateTime Timestamp,
    object   Changes);
