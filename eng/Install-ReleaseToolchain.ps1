param(
    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
$manifestPath = Join-Path $PSScriptRoot 'toolchain.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
$toolsPath = Join-Path $outputPath 'tools'
$licensesPath = Join-Path $toolsPath 'licenses'
$temporaryPath = Join-Path ([IO.Path]::GetTempPath()) ("VideoHarvester-toolchain-" + [Guid]::NewGuid().ToString('N'))

function Get-VerifiedFile {
    param(
        [Parameter(Mandatory = $true)] [string]$Url,
        [Parameter(Mandatory = $true)] [string]$Sha256,
        [Parameter(Mandatory = $true)] [string]$Destination
    )

    Invoke-WebRequest -Uri $Url -OutFile $Destination
    $stream = [IO.File]::OpenRead($Destination)
    try {
        $sha256Algorithm = [Security.Cryptography.SHA256]::Create()
        try {
            $actualHash = [BitConverter]::ToString($sha256Algorithm.ComputeHash($stream)).Replace('-', '')
        }
        finally {
            $sha256Algorithm.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }

    if ($actualHash -ne $Sha256.ToUpperInvariant()) {
        throw "Checksum verification failed for $Url. Expected $Sha256, received $actualHash."
    }
}

try {
    New-Item -ItemType Directory -Path $temporaryPath, $toolsPath, $licensesPath -Force | Out-Null

    $ytDlpPath = Join-Path $toolsPath 'yt-dlp.exe'
    Get-VerifiedFile -Url $manifest.ytDlp.url -Sha256 $manifest.ytDlp.sha256 -Destination $ytDlpPath
    Invoke-WebRequest -Uri "https://raw.githubusercontent.com/yt-dlp/yt-dlp/$($manifest.ytDlp.version)/LICENSE" -OutFile (Join-Path $licensesPath 'yt-dlp-LICENSE.txt')
    Invoke-WebRequest -Uri "https://raw.githubusercontent.com/yt-dlp/yt-dlp/$($manifest.ytDlp.version)/THIRD_PARTY_LICENSES.txt" -OutFile (Join-Path $licensesPath 'yt-dlp-THIRD_PARTY_LICENSES.txt')

    $nodeArchive = Join-Path $temporaryPath 'node.zip'
    $nodeExtracted = Join-Path $temporaryPath 'node'
    Get-VerifiedFile -Url $manifest.node.url -Sha256 $manifest.node.sha256 -Destination $nodeArchive
    Expand-Archive -LiteralPath $nodeArchive -DestinationPath $nodeExtracted
    $nodeExecutable = Get-ChildItem -Path $nodeExtracted -Filter 'node.exe' -Recurse | Select-Object -First 1
    $nodeLicense = Get-ChildItem -Path $nodeExtracted -Filter 'LICENSE' -Recurse | Select-Object -First 1
    if ($null -eq $nodeExecutable -or $null -eq $nodeLicense) {
        throw 'The Node.js archive does not contain the expected executable and license.'
    }
    Copy-Item -LiteralPath $nodeExecutable.FullName -Destination (Join-Path $toolsPath 'node.exe')
    Copy-Item -LiteralPath $nodeLicense.FullName -Destination (Join-Path $licensesPath 'node-LICENSE.txt')

    $ffmpegArchive = Join-Path $temporaryPath 'ffmpeg.zip'
    $ffmpegExtracted = Join-Path $temporaryPath 'ffmpeg'
    Get-VerifiedFile -Url $manifest.ffmpeg.url -Sha256 $manifest.ffmpeg.sha256 -Destination $ffmpegArchive
    Expand-Archive -LiteralPath $ffmpegArchive -DestinationPath $ffmpegExtracted
    foreach ($fileName in 'ffmpeg.exe', 'ffprobe.exe') {
        $executable = Get-ChildItem -Path $ffmpegExtracted -Filter $fileName -Recurse | Select-Object -First 1
        if ($null -eq $executable) {
            throw "The FFmpeg archive does not contain $fileName."
        }
        Copy-Item -LiteralPath $executable.FullName -Destination (Join-Path $toolsPath $fileName)
    }

    $ffmpegLicenseFiles = Get-ChildItem -Path $ffmpegExtracted -Filter 'LICENSE*' -File -Recurse
    foreach ($licenseFile in $ffmpegLicenseFiles) {
        Copy-Item -LiteralPath $licenseFile.FullName -Destination (Join-Path $licensesPath ("ffmpeg-" + $licenseFile.Name))
    }

    Copy-Item -LiteralPath $manifestPath -Destination (Join-Path $toolsPath 'toolchain.json')
}
finally {
    if (Test-Path -LiteralPath $temporaryPath) {
        Remove-Item -LiteralPath $temporaryPath -Recurse -Force
    }
}
