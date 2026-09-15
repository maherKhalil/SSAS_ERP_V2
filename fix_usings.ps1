$files = Get-ChildItem "c:\Users\User\Documents\SSAS_ERP_V2\SSAS_ERP_V2\src\Platform\SSAS.Platform.Application\Subscriptions\Plans\*Handler.cs"
foreach ($f in $files) {
    $content = Get-Content $f.FullName
    $content = $content -replace 'using SSAS.Platform.Domain.Subscriptions;', "using SSAS.Platform.Domain;`nusing SSAS.Platform.Domain.Subscriptions;"
    Set-Content -Path $f.FullName -Value $content
}
