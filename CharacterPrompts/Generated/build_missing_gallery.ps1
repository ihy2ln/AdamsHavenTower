$ErrorActionPreference = 'Stop'
$assetRoot = 'S:/AI/Game/Unity AHCG/My project/CharacterPrompts/Generated'
$assetQueue = @(Get-Content -LiteralPath "$assetRoot/missing_queue.jsonl" | ForEach-Object { $_ | ConvertFrom-Json })
$tiles = foreach ($assetEntry in $assetQueue) {
    $assetPath = "$($assetEntry.folder)/$($assetEntry.file)"
    $selectedPath = $assetPath -replace '\.png$', '-v2.png'
    if (-not (Test-Path -LiteralPath $selectedPath)) { $selectedPath = $assetPath }
    $thirdPath = $assetPath -replace '\.png$', '-v3.png'
    if (Test-Path -LiteralPath $thirdPath) { $selectedPath = $thirdPath }
    $fourthPath = $assetPath -replace '\.png$', '-v4.png'
    if (Test-Path -LiteralPath $fourthPath) { $selectedPath = $fourthPath }
    if (Test-Path -LiteralPath $selectedPath) {
        $assetRelative = $selectedPath.Substring($assetRoot.Length + 1)
        $assetLabel = [System.Net.WebUtility]::HtmlEncode($assetEntry.file)
        "<article data-search='$assetLabel'><a href='$assetRelative'><img loading='lazy' src='$assetRelative' alt='$assetLabel'></a><p>$assetLabel</p></article>"
    }
}
$madeCount = @($tiles).Count
$galleryHtml = @"
<!doctype html><html><head><meta charset='utf-8'><title>Adams Haven Missing Stills</title><style>body{font:16px system-ui;background:#171a27;color:#eef0ff;margin:24px}input{padding:12px;width:480px;max-width:80%;background:#292f45;color:white;border:1px solid #62709c}main{display:grid;grid-template-columns:repeat(auto-fill,minmax(220px,1fr));gap:18px;margin-top:24px}article{background:#242a3c;padding:12px;border-radius:8px}img{width:100%;height:300px;object-fit:contain;background:repeating-conic-gradient(#454b5e 0% 25%,#343b4f 0% 50%) 50%/20px 20px}p{font-size:12px;overflow-wrap:anywhere}a{color:#bccbff}</style></head><body><h1>Adams Haven missing stills</h1><p>$madeCount of 276 requested files saved. Full quality audit and corrections are pending while generation continues.</p><p><a href='gallery.html'>Original character art gallery</a></p><input id='filter' placeholder='Filter by character, rank or asset type'><main>$($tiles -join "`n")</main><script>document.getElementById('filter').addEventListener('input',e=>{let q=e.target.value.toLowerCase();document.querySelectorAll('article').forEach(a=>a.hidden=!a.dataset.search.toLowerCase().includes(q))})</script></body></html>
"@
Set-Content -LiteralPath "$assetRoot/missing-gallery.html" -Value $galleryHtml -Encoding UTF8
Write-Output "Gallery includes $madeCount requested images."


