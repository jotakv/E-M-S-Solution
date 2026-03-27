-- ============================================================
-- Run this script BEFORE applying migration
-- 20260327000000_AddUniqueIndexesAndFixAuditLogEmployeeId
-- on a database that already has data.
-- ============================================================

-- ── 1. Delete orphaned child rows ────────────────────────────────────────────
-- These rows were left behind when an Employee was deleted without cascade.
-- After the migration the FK constraints + cascade will prevent new orphans.

DELETE FROM Vacations
WHERE EmployeeId NOT IN (SELECT Id FROM Employees);

DELETE FROM Overtimes
WHERE EmployeeId NOT IN (SELECT Id FROM Employees);

DELETE FROM Sanctions
WHERE EmployeeId NOT IN (SELECT Id FROM Employees);

DELETE FROM Doctors
WHERE EmployeeId NOT IN (SELECT Id FROM Employees);

-- ── 2. Inspect duplicate CivilIds ────────────────────────────────────────────
-- Review before deleting; keep the most-recently updated row.

SELECT CivilId, COUNT(*) AS [Count]
FROM Employees
GROUP BY CivilId
HAVING COUNT(*) > 1;

-- Deduplicate: keep the employee with the lowest Id for each CivilId.
-- Adjust the filter (MIN vs MAX) to match your retention policy.
DELETE e
FROM Employees e
JOIN (
    SELECT CivilId, MIN(Id) AS KeepId
    FROM Employees
    GROUP BY CivilId
    HAVING COUNT(*) > 1
) dup ON e.CivilId = dup.CivilId AND e.Id <> dup.KeepId;

-- ── 3. Inspect duplicate FileNumbers ─────────────────────────────────────────

SELECT FileNumber, COUNT(*) AS [Count]
FROM Employees
GROUP BY FileNumber
HAVING COUNT(*) > 1;

DELETE e
FROM Employees e
JOIN (
    SELECT FileNumber, MIN(Id) AS KeepId
    FROM Employees
    GROUP BY FileNumber
    HAVING COUNT(*) > 1
) dup ON e.FileNumber = dup.FileNumber AND e.Id <> dup.KeepId;

-- ── 4. Verify no duplicates remain ───────────────────────────────────────────
-- Both queries should return 0 rows before running the migration.

SELECT 'CivilId duplicates remaining:' AS Check, COUNT(*) AS [Count]
FROM (
    SELECT CivilId
    FROM Employees
    GROUP BY CivilId
    HAVING COUNT(*) > 1
) x;

SELECT 'FileNumber duplicates remaining:' AS Check, COUNT(*) AS [Count]
FROM (
    SELECT FileNumber
    FROM Employees
    GROUP BY FileNumber
    HAVING COUNT(*) > 1
) x;

-- ── 5. Verify no orphans remain ───────────────────────────────────────────────

SELECT 'Orphaned Vacations:'  AS Check, COUNT(*) AS [Count] FROM Vacations WHERE EmployeeId NOT IN (SELECT Id FROM Employees);
SELECT 'Orphaned Overtimes:'  AS Check, COUNT(*) AS [Count] FROM Overtimes  WHERE EmployeeId NOT IN (SELECT Id FROM Employees);
SELECT 'Orphaned Sanctions:'  AS Check, COUNT(*) AS [Count] FROM Sanctions  WHERE EmployeeId NOT IN (SELECT Id FROM Employees);
SELECT 'Orphaned Doctors:'    AS Check, COUNT(*) AS [Count] FROM Doctors    WHERE EmployeeId NOT IN (SELECT Id FROM Employees);
-- All counts must be 0 before running the migration.
