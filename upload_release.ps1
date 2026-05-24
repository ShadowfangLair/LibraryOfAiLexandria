param (
    [string]$Tag = "v1.0.15",
    [string]$Token = ""
)

$repo = "ShadowfangLair/LibraryOfAiLexandria"

$headers = @{
    Authorization = "token $token"
    Accept = "application/vnd.github.v3+json"
}

Write-Host "Creating release $Tag..."
$body = @{
    tag_name = $Tag
    name = "Update $Tag"
    body = "Automated update via AI."
    draft = $false
    prerelease = $false
} | ConvertTo-Json

try {
    $releaseResponse = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases" -Method Post -Headers $headers -Body $body
    Write-Host "Release created with ID: $($releaseResponse.id)"
} catch {
    Write-Host "Error creating release. It might already exist."
    # Try to get the existing release
    $releaseResponse = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/tags/$Tag" -Headers $headers
}

$uploadUrl = $releaseResponse.upload_url -replace '\{\?name,label\}', "?name=LibraryOfAiLexandria-Setup.exe"

$filePath = "E:\Antigravity\Library of Ai-Lexandria\Output\LibraryOfAiLexandria-Setup.exe"

Write-Host "Uploading installer to $uploadUrl ..."
Invoke-RestMethod -Uri $uploadUrl -Method Post -Headers @{
    Authorization = "token $token"
    Accept = "application/vnd.github.v3+json"
    "Content-Type" = "application/octet-stream"
} -InFile $filePath

Write-Host "Upload complete!"
