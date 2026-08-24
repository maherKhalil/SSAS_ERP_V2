param(
  [Parameter(Mandatory = $true)][string]$OutFile,
  [string]$Tag   = 'gate',
  [string]$Match = 'SSAS'
)

# Coarse working-set poll for the Integration leg of scripts/gate.sh. REPORTED, NEVER ASSERTED -- see
# the sampling note in gate.sh for why the allocation BUDGET that preceded it was removed and why the
# OBSERVATION was worth keeping.
#
# -Match scopes the count to OUR test hosts by command-line path, so a suite from an unrelated
# repository on the same box is never counted as ours. This process is powershell.exe and matches only
# testhost.exe, so it can neither count nor act on itself -- a self-match once made a process-hunting
# command kill itself, and that trap is deliberately closed here.

"iso,tag,testhost_ws_mb,testhost_count,free_mb" | Out-File -FilePath $OutFile -Encoding utf8

while ($true) {
  $procs = @(Get-CimInstance Win32_Process -Filter "Name='testhost.exe'" -ErrorAction SilentlyContinue |
             Where-Object { $_.CommandLine -like "*$Match*" })
  $ws = 0
  $n  = $procs.Count
  if ($n -gt 0) {
    $ws = [math]::Round((($procs | Measure-Object -Property WorkingSetSize -Sum).Sum) / 1MB, 0)
  }
  $free = [math]::Round((Get-CimInstance Win32_OperatingSystem).FreePhysicalMemory / 1KB, 0)
  "{0},{1},{2},{3},{4}" -f (Get-Date -Format 'HH:mm:ss'), $Tag, $ws, $n, $free |
    Out-File -FilePath $OutFile -Append -Encoding utf8
  Start-Sleep -Seconds 3
}
