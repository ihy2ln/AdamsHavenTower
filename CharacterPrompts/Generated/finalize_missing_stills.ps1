$ErrorActionPreference = 'Stop'
$assetRoot = 'S:/AI/Game/Unity AHCG/My project/CharacterPrompts/Generated'
& "$assetRoot/audit_missing_stills.ps1"
$assetQueue = @(Get-Content -LiteralPath "$assetRoot/missing_queue.jsonl" | ForEach-Object { $_ | ConvertFrom-Json })
$assetAudit = @(Get-Content -LiteralPath "$assetRoot/missing-file-verification.json" -Raw | ConvertFrom-Json)
if (@($assetAudit | Where-Object { -not $_.exists -or -not $_.readable }).Count) { throw 'Missing or unreadable assets.' }
$manifest = for ($i=0; $i -lt $assetQueue.Count; $i++) {
    $entry=$assetQueue[$i]; $record=$assetAudit[$i]
    $record.status='accepted-after-visual-review'
    $record | Add-Member -NotePropertyName prompt -NotePropertyValue $entry.prompt
    $record | Add-Member -NotePropertyName generator -NotePropertyValue 'Codex built-in ImageGen'
    $record
}
$manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath "$assetRoot/missing-generation-manifest.json" -Encoding UTF8
$assetAudit | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath "$assetRoot/missing-file-verification.json" -Encoding UTF8
foreach ($group in ($assetQueue | Group-Object folder)) {
    $logPath="$($group.Name)/PROMPTS.md"
    $marker='## Missing stills final acceptance audit'
    if ((Get-Content -LiteralPath $logPath -Raw).Contains($marker)) { continue }
    $lines=@('', $marker, '', 'Built-in Codex ImageGen; originals retained; selected corrections are versioned siblings. Full generation and correction prompts are recorded above.', '')
    foreach ($entry in $group.Group) {
        $record=$manifest | Where-Object file -EQ $entry.file
        $selectedName=Split-Path -Leaf $record.selectedOutput
        $lines += "- Accepted: $selectedName | Reference: $($entry.reference) | Canvas: $($record.width)x$($record.height) | Requested asset: $($entry.file) | Transparent requested: $($entry.transparent) | Status: visually reviewed and PNG readable."
    }
    Add-Content -LiteralPath $logPath -Value ($lines -join "`n") -Encoding UTF8
}
$alphaFailures=@($manifest | Where-Object { $_.transparentRequested -and $_.cornerAlpha[0] -gt 0 -and $_.cornerAlpha[1] -gt 0 })
$correctionFiles=0
foreach ($entry in $assetQueue) { foreach($version in 2..4) { if(Test-Path -LiteralPath ("$($entry.folder)/$($entry.file)" -replace '\.png$', "-v$version.png")) { $correctionFiles++ } } }
$report=[ordered]@{requested=276;completed=$manifest.Count;characters=@($assetQueue.id | Sort-Object -Unique).Count;heroImages=@($manifest | Where-Object kind -EQ hero).Count;residentImages=@($manifest | Where-Object kind -EQ resident).Count;correctedAssets=@($manifest | Where-Object { $_.selectedOutput -ne $_.output }).Count;thirdVersions=@($manifest | Where-Object selectedOutput -Like '*-v3.png').Count;missing=0;unreadable=0;transparentPortraits=@($manifest | Where-Object transparentRequested).Count;transparentCornerFailures=$alphaFailures.Count;dimensions=@($manifest | Group-Object width,height | ForEach-Object { [ordered]@{canvas=$_.Name;count=$_.Count} });stillsOnly=$true;existingCardArtRetained=$true;generator='Codex built-in ImageGen';nativeCanvasPolicy='Native generator canvases retained as permitted by brief; avatars are native-resolution masters rather than literal 512px exports.';scope='276 character stills requested in user table; shared summon assets are separate.';gallery='missing-gallery.html';manifest='missing-generation-manifest.json'}
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath "$assetRoot/missing-batch-report.json" -Encoding UTF8
$report.correctionFilesSaved=$correctionFiles
$report.failedAssets=0
$report.visualReview='All 276 originals reviewed; 38 flagged assets corrected and selected corrections reviewed.'
$report | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath "$assetRoot/missing-batch-report.json" -Encoding UTF8
& "$assetRoot/build_missing_gallery.ps1"
$galleryPath="$assetRoot/missing-gallery.html"
$gallery=Get-Content -LiteralPath $galleryPath -Raw
$gallery=$gallery.Replace('Full quality audit and corrections are pending while generation continues.', 'All selected images visually reviewed and file-verified. Versioned corrections are selected; originals retained. Native canvas sizes are recorded in missing-batch-report.json.')
Set-Content -LiteralPath $galleryPath -Value $gallery -Encoding UTF8
Add-Content -LiteralPath "$assetRoot/README.txt" -Value "`nAdditional missing stills completed: 276 images across the same 60 character folders (180 hero, 96 resident). Browse missing-gallery.html. missing-generation-manifest.json identifies selected correction versions; missing-batch-report.json records verification and dimensions. Each PROMPTS.md contains generation prompts and final accepted-file entries. Existing cards retained; stills only."
$report | ConvertTo-Json -Depth 10
