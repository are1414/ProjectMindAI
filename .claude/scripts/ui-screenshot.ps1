<#
.SYNOPSIS
    Çalışan ProjectMind AI (Blazor Server) uygulamasının sayfalarından ekran görüntüsü alır.

.DESCRIPTION
    Headless Microsoft Edge'i DevTools protokolüyle (CDP) sürer. Blazor Server ön-render kapalı olduğu için
    düz "msedge --screenshot" boş sayfa çeker; bu betik sayfayı açar, SignalR bağlantısı ve render için bekler,
    isteğe bağlı bir JavaScript (tıklama vb.) çalıştırır, sonra PNG kaydeder. Her çekimden sonra sayfanın durumunu
    (başlık, Blazor hata çubuğu görünüyor mu, yeniden bağlanma penceresi, yatay taşma, görünür metin uzunluğu)
    tek satır JSON olarak yazar. Yeni paket gerektirmez (yalnız Edge + Windows PowerShell 5.1).

    Uygulama ayrıca çalışıyor olmalı (Visual Studio veya: dotnet run --project src/ProjectMind.Web --launch-profile http).

.PARAMETER Shots
    "ad|yol" ya da "ad|yol|javascript" dizisi. Örnek: @('home|/', 'ayarlar|/settings',
    'durum|/projects/1|document.querySelectorAll(".tab")[1]?.click()')
    JavaScript bir metin döndürürse (ör. JSON.stringify(...)) sonucu "<ad> js: ..." satırında yazılır; böylece
    sayfadaki bağlantılar, metinler veya hesaplanmış stiller okunabilir.

.EXAMPLE
    & .claude/scripts/ui-screenshot.ps1 -Shots @('home|/','evaluation|/evaluation') -Width 390 -Height 844 -Suffix mobile
#>
param(
    [Parameter(Mandatory = $true)][string[]]$Shots,
    [string]$BaseUrl = "http://localhost:5115",
    [string]$OutDir = (Join-Path $env:TEMP "projectmind-ui-shots"),
    [int]$Width = 1440,
    [int]$Height = 900,
    [int]$WaitMs = 3500,
    [int]$AfterScriptWaitMs = 1500,
    [string]$Suffix = "",
    [int]$Port = 9333
)

$ErrorActionPreference = "Stop"

$edgeCandidates = @(
    "${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe",
    "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe"
)
$edge = $edgeCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $edge) { throw "Microsoft Edge bulunamadı." }

try { Invoke-WebRequest -Uri $BaseUrl -UseBasicParsing -TimeoutSec 5 | Out-Null }
catch { throw "Uygulama $BaseUrl adresinde yanıt vermiyor. Önce uygulamayı başlatın." }

New-Item -ItemType Directory -Force $OutDir | Out-Null
$profileDir = Join-Path $OutDir "edge-profile"

$edgeArgs = @(
    "--headless=new", "--disable-gpu", "--hide-scrollbars", "--no-first-run", "--no-default-browser-check",
    "--remote-debugging-port=$Port", "--user-data-dir=`"$profileDir`"", "--window-size=$Width,$Height", "about:blank"
)
$proc = Start-Process -FilePath $edge -ArgumentList $edgeArgs -PassThru -WindowStyle Hidden

$script:ws = $null
$script:nextId = 0

function Send-Cdp([string]$method, [hashtable]$params = @{}) {
    $script:nextId++
    $id = $script:nextId
    $payload = @{ id = $id; method = $method; params = $params } | ConvertTo-Json -Depth 10 -Compress
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($payload)
    $segment = New-Object System.ArraySegment[byte] -ArgumentList @(, $bytes)
    $script:ws.SendAsync($segment, [System.Net.WebSockets.WebSocketMessageType]::Text, $true,
        [System.Threading.CancellationToken]::None).Wait()

    $buffer = New-Object byte[] 65536
    while ($true) {
        $stream = New-Object System.IO.MemoryStream
        do {
            $seg = New-Object System.ArraySegment[byte] -ArgumentList @(, $buffer)
            $task = $script:ws.ReceiveAsync($seg, [System.Threading.CancellationToken]::None)
            if (-not $task.Wait(30000)) { throw "CDP yanıtı zaman aşımına uğradı: $method" }
            $stream.Write($buffer, 0, $task.Result.Count)
        } while (-not $task.Result.EndOfMessage)
        $text = [System.Text.Encoding]::UTF8.GetString($stream.ToArray())
        # Yalnız bu isteğin cevabı; olay mesajları (method alanlı) atlanır.
        if ($text -match "^\{`"id`":$id,") { return $text }
    }
}

function Get-EvalValue([string]$response) {
    if ($response -match '"value":"((?:[^"\\]|\\.)*)"') { return [regex]::Unescape($Matches[1]) }
    return $null
}

$probeJs = @'
JSON.stringify({
  title: document.title,
  blazorErrorVisible: (() => { const e = document.getElementById('blazor-error-ui'); return !!e && getComputedStyle(e).display !== 'none'; })(),
  reconnectVisible: (() => { const e = document.getElementById('components-reconnect-modal'); return !!e && (e.open === true || getComputedStyle(e).display !== 'none' && e.offsetHeight > 0); })(),
  horizontalOverflow: document.documentElement.scrollWidth > window.innerWidth,
  clippedContainers: [...document.querySelectorAll('body *')].filter(e => {
      const s = getComputedStyle(e);
      return (s.overflowX === 'hidden' || s.overflowX === 'clip') && s.textOverflow !== 'ellipsis'
          && e.clientWidth > 0 && e.scrollWidth > e.clientWidth + 1;
    }).slice(0, 8).map(e => e.tagName.toLowerCase() + (typeof e.className === 'string' && e.className ? '.' + e.className.trim().split(/\s+/).join('.') : '')),
  visibleTextLength: (document.body.innerText || '').trim().length
})
'@

try {
    $target = $null
    for ($i = 0; $i -lt 40 -and -not $target; $i++) {
        Start-Sleep -Milliseconds 250
        try {
            $list = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/json/list" -TimeoutSec 2
            $target = $list | Where-Object { $_.type -eq "page" } | Select-Object -First 1
        } catch { }
    }
    if (-not $target) { throw "Edge DevTools bağlantısı kurulamadı (port $Port)." }

    $script:ws = New-Object System.Net.WebSockets.ClientWebSocket
    $script:ws.Options.KeepAliveInterval = [TimeSpan]::FromSeconds(10)
    $script:ws.ConnectAsync([Uri]$target.webSocketDebuggerUrl, [System.Threading.CancellationToken]::None).Wait()

    Send-Cdp "Emulation.setDeviceMetricsOverride" @{ width = $Width; height = $Height; deviceScaleFactor = 1; mobile = ($Width -lt 768) } | Out-Null

    foreach ($shot in $Shots) {
        $parts = $shot.Split("|", 3)
        $name = $parts[0]
        $path = if ($parts.Length -gt 1) { $parts[1] } else { "/" }
        $js = if ($parts.Length -gt 2) { $parts[2] } else { $null }

        Send-Cdp "Page.navigate" @{ url = ($BaseUrl.TrimEnd("/") + $path) } | Out-Null
        Start-Sleep -Milliseconds $WaitMs
        $jsResult = $null
        if ($js) {
            $jsResult = Get-EvalValue (Send-Cdp "Runtime.evaluate" @{ expression = $js; awaitPromise = $true; returnByValue = $true })
            Start-Sleep -Milliseconds $AfterScriptWaitMs
        }

        $probe = Get-EvalValue (Send-Cdp "Runtime.evaluate" @{ expression = $probeJs; returnByValue = $true })
        $shotResponse = Send-Cdp "Page.captureScreenshot" @{ format = "png" }
        if ($shotResponse -notmatch '"data":"([^"]+)"') { throw "Ekran görüntüsü alınamadı: $name" }

        $fileName = if ($Suffix) { "$name-$Suffix.png" } else { "$name.png" }
        $file = Join-Path $OutDir $fileName
        [System.IO.File]::WriteAllBytes($file, [Convert]::FromBase64String($Matches[1]))
        Write-Output ("{0} -> {1} {2}" -f $name, $file, $probe)
        if ($jsResult) { Write-Output ("{0} js: {1}" -f $name, $jsResult) }
    }
}
finally {
    if ($script:ws) { try { $script:ws.Dispose() } catch { } }
    Get-CimInstance Win32_Process -Filter "Name = 'msedge.exe'" |
        Where-Object { $_.CommandLine -and $_.CommandLine.Contains($profileDir) } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }
    if (-not $proc.HasExited) { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue }
}
