# Integration tests — operational notes

These tests run against a real SQL Server. Every fixture creates real databases and drops them in teardown.

## `SSAS_` is a reserved prefix

**Any database named `SSAS_...` on the test instance belongs to the test suite and may be dropped.**

Do not name a scratch or personal database with this prefix. Production already reserves `SSAS_Verify_` the
same way (`TenantDatabaseVerificationNaming.ReservedPrefix`); this extends the convention to the whole `SSAS_`
namespace.

The convention exists because pattern-matching the catalog names does not work. The suite has at least three
unrelated shapes — `SSAS_<name>_<12 hex>`, `SSAS_ERP_BACKUPP_<32 hex>`, `SSAS_Verify_<digits>_<digits>` — and
a fourth arrives with the next fixture. `CatalogLeakGuardTests` therefore matches `SSAS_%` loosely and relies
on this convention for correctness. Tightening that predicate is a regression, not a cleanup.

## When `CatalogLeakGuardTests` fails

It reports test catalogs that were on the instance **before the test process started**, so they survived an
earlier run. Teardown failed and the catalogs leaked.

First check standard error in the failing run for `[CATALOG LEAK]` lines — those name the catalog and the SQL
error that stopped the drop, which is usually the real diagnosis.

Then reap the orphans. **Enumerate, confirm, then drop — never a blind loop.**

### Step 1 — enumerate (read-only)

```sql
SELECT d.name, d.create_date,
       SUM(mf.size) * 8 / 1024 AS mb,
       COUNT(s.session_id)     AS open_sessions
FROM sys.databases d
LEFT JOIN sys.master_files mf     ON mf.database_id = d.database_id
LEFT JOIN sys.dm_exec_sessions s  ON s.database_id  = d.database_id
WHERE d.database_id > 4
GROUP BY d.name, d.create_date
ORDER BY d.create_date;
```

### Step 2 — confirm, before dropping anything

- **No suite is running.** Dropping while a run is in flight destroys live catalogs mid-test. Check
  `open_sessions` above and confirm no `testhost` process is active.
- **Only `SSAS_`-prefixed databases are in scope.** Everything else stays, whatever it looks like. A wrongly
  dropped database is not recoverable from this repo.
- **Read the list.** Confirm every name you are about to drop is a test catalog.

### Step 3 — drop

```sql
DECLARE @n sysname, @sql nvarchar(max);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR
  SELECT name FROM sys.databases WHERE database_id > 4 AND name LIKE 'SSAS[_]%';
OPEN c; FETCH NEXT FROM c INTO @n;
WHILE @@FETCH_STATUS = 0
BEGIN
  BEGIN TRY
    -- Direct DROP first: a database left in RESTORING cannot be put into SINGLE_USER.
    SET @sql = N'DROP DATABASE ' + QUOTENAME(@n) + N';';
    EXEC sp_executesql @sql;
  END TRY
  BEGIN CATCH
    BEGIN TRY
      SET @sql = N'ALTER DATABASE ' + QUOTENAME(@n) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; ' +
                 N'DROP DATABASE ' + QUOTENAME(@n) + N';';
      EXEC sp_executesql @sql;
    END TRY
    BEGIN CATCH
      PRINT 'FAILED: ' + @n + ' -> ' + ERROR_MESSAGE();
    END CATCH
  END CATCH
  FETCH NEXT FROM c INTO @n;
END
CLOSE c; DEALLOCATE c;
```

Re-run step 1 afterwards and confirm the count is zero.

## Do not run suites concurrently against one instance

`CatalogLeakGuardTests` cannot distinguish a previous run's orphan from a concurrent sibling suite's live
catalog. Running Integration and API against the same instance at the same time will fail the guard, possibly
while nothing is wrong. Serialise the runs — do not weaken the guard.
