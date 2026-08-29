-- =====================================================================================================
-- PRE-FLIGHT FOR THE LEAVE-BALANCE UNIQUE INDEX (T-171).
-- =====================================================================================================
--
-- Run this against EVERY TENANT DATABASE before the unique index migration is applied.
--
-- ---- WHY THIS EXISTS.
--
-- `UX_AttendanceLeaveBalances_Employee_Type_Year` is unfiltered and unique. If any tenant already holds
-- two balance rows on the same key, CREATE INDEX fails and the migration fails mid-deployment.
--
-- ---- ⚠ A NON-ZERO RESULT IS NOT A TIDY-UP. DO NOT MERGE THE ROWS.
--
-- Two rows with entitlements and consumptions raise a question engineering may not answer: is the
-- entitlement the max, the sum, or the later one, and is the consumption the sum? A pair where BOTH rows
-- carry consumption is the case where somebody HAS ALREADY OVER-CONSUMED, and quietly summing it into one
-- row erases the evidence that it happened.
--
-- **Report the output. Do not resolve it here.**
--
-- ---- WHAT THE DUPLICATE MEANS, SO THE READER KNOWS WHY IT MATTERS.
--
-- `LeaveBalance.Consume` guards with `ConsumedQuantity + quantity > EntitlementQuantity` against THAT ROW's
-- own consumed figure, and the repository reads with `FirstOrDefaultAsync`. With two rows the guard passes
-- twice against two different counters, so an employee can take double their entitlement and nothing
-- reports it.

SET NOCOUNT ON;

SELECT
    b.TenantId,
    b.CompanyId,
    b.EmployeeId,
    b.LeaveTypeId,
    b.PeriodYear,
    COUNT(*)                       AS RowCountOnKey,
    SUM(b.EntitlementQuantity)     AS TotalEntitlement,
    MAX(b.EntitlementQuantity)     AS MaxEntitlement,
    SUM(b.ConsumedQuantity)        AS TotalConsumed,

    -- The row that decides severity: both rows consumed means leave has already been taken twice.
    SUM(CASE WHEN b.ConsumedQuantity > 0 THEN 1 ELSE 0 END) AS RowsWithConsumption,

    MIN(b.CreatedUtc)              AS FirstCreatedUtc,
    MAX(b.ModifiedUtc)             AS LastModifiedUtc
FROM dbo.AttendanceLeaveBalances AS b
GROUP BY b.TenantId, b.CompanyId, b.EmployeeId, b.LeaveTypeId, b.PeriodYear
HAVING COUNT(*) > 1
ORDER BY RowsWithConsumption DESC, COUNT(*) DESC;

-- A second statement rather than a comment, so an empty grid is distinguishable from a query that did not
-- run: zero here is a measured zero.
SELECT
    COUNT(*)                                   AS TotalBalanceRows,
    COUNT(DISTINCT CONCAT_WS('|', TenantId, CompanyId, EmployeeId, LeaveTypeId, PeriodYear))
                                               AS DistinctKeys
FROM dbo.AttendanceLeaveBalances;
