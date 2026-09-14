DECLARE @n sysname, @sql nvarchar(max);
DECLARE c CURSOR LOCAL FAST_FORWARD FOR
  SELECT name FROM sys.databases WHERE database_id > 4 AND name LIKE 'SSAS[_]%';
OPEN c; FETCH NEXT FROM c INTO @n;
WHILE @@FETCH_STATUS = 0
BEGIN
  BEGIN TRY
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
