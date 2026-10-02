$ErrorActionPreference = 'Stop'
$assetRoot = 'S:/AI/Game/Unity AHCG/My project/CharacterPrompts/Generated'
Add-Type -AssemblyName System.Drawing
$assetQueue = @(Get-Content -LiteralPath "$assetRoot/missing_queue.jsonl" | ForEach-Object { $_ | ConvertFrom-Json })
$assetAudit = foreach ($assetEntry in $assetQueue) {
    $assetPath = "$($assetEntry.folder)/$($assetEntry.file)"
    $selectedPath = $assetPath -replace '\.png$', '-v2.png'
    if (-not (Test-Path -LiteralPath $selectedPath)) { $selectedPath = $assetPath }
    $thirdPath = $assetPath -replace '\.png$', '-v3.png'
    if (Test-Path -LiteralPath $thirdPath) { $selectedPath = $thirdPath }
    $fourthPath = $assetPath -replace '\.png$', '-v4.png'
    if (Test-Path -LiteralPath $fourthPath) { $selectedPath = $fourthPath }
    $assetRow = [ordered]@{id=$assetEntry.id;name=$assetEntry.name;kind=$assetEntry.kind;file=$assetEntry.file;output=$assetPath;selectedOutput=$selectedPath;reference="$($assetEntry.folder)/$($assetEntry.reference)";priority=$assetEntry.priority;requestedSize=$assetEntry.size;transparentRequested=$assetEntry.transparent;exists=(Test-Path -LiteralPath $assetPath);readable=$false;width=$null;height=$null;cornerAlpha=$null;status='missing'}
    if ($assetRow.exists) {
        try {
            $assetBitmap = [System.Drawing.Bitmap]::new($selectedPath)
            $assetRow.width=$assetBitmap.Width; $assetRow.height=$assetBitmap.Height
            $assetRow.cornerAlpha=@($assetBitmap.GetPixel(0,0).A,$assetBitmap.GetPixel($assetBitmap.Width-1,0).A,$assetBitmap.GetPixel(0,$assetBitmap.Height-1).A,$assetBitmap.GetPixel($assetBitmap.Width-1,$assetBitmap.Height-1).A)
            $assetRow.readable=$true; $assetRow.status='file-verified-awaiting-final-review'
            $assetBitmap.Dispose()
        } catch { $assetRow.status='unreadable'; $assetRow.error=$_.Exception.Message }
    }
    [pscustomobject]$assetRow
}
$assetAudit | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath "$assetRoot/missing-file-verification.json" -Encoding UTF8
$assetCounts = [ordered]@{requested=276;saved=@($assetAudit | Where-Object exists).Count;missing=@($assetAudit | Where-Object { -not $_.exists }).Count;unreadable=@($assetAudit | Where-Object { $_.exists -and -not $_.readable }).Count;characters=@($assetQueue.id | Sort-Object -Unique).Count;corrections=@($assetAudit | Where-Object { $_.selectedOutput -ne $_.output }).Count}
$assetCounts | ConvertTo-Json


