$path = "tests/Modules/HIS/SSAS.HIS.Domain.Tests/SSAS.HIS.Domain.Tests.csproj"
$content = Get-Content $path
$content = $content -replace 'Version="[^"]*"', ''
Set-Content -Path $path -Value $content
