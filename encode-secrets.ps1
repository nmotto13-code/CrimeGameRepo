# encode-secrets.ps1
# Run this to get the base64 values needed for GitHub Secrets.
# Copy each output value into the corresponding GitHub Secret.

$signingDir  = "C:\Users\blued\apple-signing"
$downloadDir = "C:\Users\blued\Downloads"

Write-Host ""
Write-Host "===== GITHUB SECRETS — copy each value into your repo secrets =====" -ForegroundColor Cyan
Write-Host ""

# P12_BASE64
$p12Path = "$signingDir\AppleDistribution.p12"
if (Test-Path $p12Path) {
    $p12Base64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($p12Path))
    Write-Host "--- P12_BASE64 ---" -ForegroundColor Yellow
    Write-Host $p12Base64
    Write-Host ""
} else {
    Write-Host "ERROR: AppleDistribution.p12 not found at $p12Path" -ForegroundColor Red
}

# P12_PASSWORD — enter the password you set when exporting the p12
Write-Host "--- P12_PASSWORD ---" -ForegroundColor Yellow
Write-Host "(enter the password you used when exporting AppleDistribution.p12)"
Write-Host ""

# PROVISION_BASE64
$provPath = "$downloadDir\PocketCasebook_Prov.mobileprovision"
if (Test-Path $provPath) {
    $provBase64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($provPath))
    Write-Host "--- PROVISION_BASE64 ---" -ForegroundColor Yellow
    Write-Host $provBase64
    Write-Host ""
} else {
    Write-Host "ERROR: PocketCasebook_Prov.mobileprovision not found at $provPath" -ForegroundColor Red
}

Write-Host "===== REMAINING SECRETS (get from App Store Connect) =====" -ForegroundColor Cyan
Write-Host ""
Write-Host "APP_STORE_CONNECT_KEY_ID" -ForegroundColor Yellow
Write-Host "  -> App Store Connect -> Users & Access -> Integrations -> Keys -> your key's ID"
Write-Host ""
Write-Host "APP_STORE_CONNECT_ISSUER_ID" -ForegroundColor Yellow
Write-Host "  -> Same page, shown above the keys table"
Write-Host ""
Write-Host "APP_STORE_CONNECT_KEY_BASE64" -ForegroundColor Yellow
Write-Host "  -> Download the .p8 key file from App Store Connect, then run:"
Write-Host '  [Convert]::ToBase64String([IO.File]::ReadAllBytes("C:\path\to\AuthKey_XXXXXXX.p8"))'
Write-Host ""
