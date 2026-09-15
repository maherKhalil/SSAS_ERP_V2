$sql = Get-Content -Raw "scripts/HIS_Migration.sql"
$outDir = "src/Modules/HIS/SSAS.HIS.Domain/Entities"

$schemas = @("BloodBank", "Emergency", "Pharmacy")

foreach ($schema in $schemas) {
    $schemaDir = Join-Path $outDir $schema
    if (-not (Test-Path $schemaDir)) {
        New-Item -ItemType Directory -Path $schemaDir | Out-Null
    }

    $regex = "INSERT INTO \[${schema}\]\.\[(.*?)\] \((.*?)\)"
    $matches = [regex]::Matches($sql, $regex)

    foreach ($match in $matches) {
        $table = $match.Groups[1].Value
        $colsStr = $match.Groups[2].Value
        
        # Clean up table name for class name
        $className = $table -replace ' ', '' -replace '\.', '_'
        $cols = $colsStr -split ',' | ForEach-Object { $_.Trim().Trim('[', ']') }

        $props = ""
        foreach ($col in $cols) {
            $colName = $col -replace ' ', '' -replace '\.', '_'
            if ($colName -eq "Id") {
                continue
            }
            if ($colName -eq "TenantId") {
                $props += "    public Guid TenantId { get; set; }`n"
            }
            else {
                $props += "    public string? $colName { get; set; }`n"
            }
        }
        
        $hasId = $cols -contains "Id"
        $baseClass = if ($hasId) { " : Entity<string>, ITenantOwnedEntity" } else { " : ITenantOwnedEntity" }
        $ctor = if ($hasId) { "`n    public $className(string id) : base(id) { }`n    public $className() : base(Guid.NewGuid().ToString()) { }`n" } else { "" }

        $classContent = @"
using System;
using SSAS.BuildingBlocks.Domain;

namespace SSAS.HIS.Domain.Entities.$schema;

public class $className$baseClass
{
$ctor
$props
}
"@

        $filePath = Join-Path $schemaDir "$className.cs"
        Set-Content -Path $filePath -Value $classContent
    }
}
