# Usage: .\build-docker.ps1 [-DockerfilePath] <string> [-ImageName] <string>
# Example: .\build-docker.ps1 -DockerfilePath SURIMI-Ecopath\Dockerfile -ImageName rikkert242/ecopath:latest
# Example: .\build-docker.ps1 SURIMI-Ecopath\Dockerfile rikkert242/ecopath:latest
#
# Set environment variables (one-time setup):
# [System.Environment]::SetEnvironmentVariable('SURIMI_DOCKER_BUILD_GITHUB_TOKEN', 'your-github-token', 'User')
# [System.Environment]::SetEnvironmentVariable('SURIMI_DOCKER_BUILD_BSR_TOKEN', 'your-bsr-token', 'User')
#
# After setting environment variables, restart PowerShell or your terminal

param(
    [Parameter(Mandatory = $true, Position = 0, HelpMessage = "Path to the Dockerfile (e.g., SURIMI-Ecopath\Dockerfile)")]
    [string]$DockerfilePath,
    
    [Parameter(Mandatory = $true, Position = 1, HelpMessage = "Docker image name and tag (e.g., rikkert242/ecopath:latest)")]
    [string]$ImageName
)

# Validate Dockerfile exists
if (-not (Test-Path $DockerfilePath)) {
    Write-Error "Dockerfile not found at: $DockerfilePath"
    exit 1
}

Write-Host "Using Dockerfile: $DockerfilePath" -ForegroundColor Cyan
Write-Host "Image name: $ImageName" -ForegroundColor Cyan

# Get tokens from environment variables
$githubToken = $env:SURIMI_DOCKER_BUILD_GITHUB_TOKEN
$bsrToken = $env:SURIMI_DOCKER_BUILD_BSR_TOKEN

# Validate tokens are found
if ([string]::IsNullOrEmpty($githubToken) -or [string]::IsNullOrEmpty($bsrToken)) {
    Write-Error @"
Tokens not found in environment variables. Please set them using:

[System.Environment]::SetEnvironmentVariable('SURIMI_DOCKER_BUILD_GITHUB_TOKEN', 'your-github-token', 'User')
[System.Environment]::SetEnvironmentVariable('SURIMI_DOCKER_BUILD_BSR_TOKEN', 'your-bsr-token', 'User')

After setting, restart PowerShell or your terminal.
"@
    exit 1
}

Write-Host "Tokens loaded from environment variables" -ForegroundColor Green

# Build Docker image
Write-Host "Building Docker image..." -ForegroundColor Yellow
docker build -f $DockerfilePath `
    --build-arg GITHUB_TOKEN=$githubToken `
    --build-arg BSR_TOKEN=$bsrToken `
    -t $ImageName .

if ($LASTEXITCODE -ne 0) {
    Write-Error "Docker build failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}

Write-Host "Docker image '$ImageName' built successfully!" -ForegroundColor Green

# Push Docker image to registry
Write-Host "Pushing Docker image to registry..." -ForegroundColor Yellow
docker push $ImageName

if ($LASTEXITCODE -eq 0) {
    Write-Host "Docker image '$ImageName' pushed successfully!" -ForegroundColor Green
} else {
    Write-Error "Docker push failed with exit code $LASTEXITCODE"
    exit $LASTEXITCODE
}