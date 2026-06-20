param(
	[Parameter(Mandatory=$true)] [string] $zipPath,
	[Parameter(Mandatory=$true)] [string] $targetPath
)

Write-Output "Remote deploy starting. Zip: $zipPath, Target: $targetPath"

$timestamp = Get-Date -Format yyyyMMddHHmmss
$backupPath = "$targetPath-backup-$timestamp"

try {
	if (Test-Path $targetPath) {
		Write-Output "Backing up existing content to $backupPath"
		Move-Item -Path $targetPath -Destination $backupPath -Force
	}

	Write-Output "Extracting $zipPath to $targetPath"
	Expand-Archive -LiteralPath $zipPath -DestinationPath $targetPath -Force

	# If IIS is present and a site named 'SDM' exists, try recycle it
	if (Get-Module -ListAvailable -Name WebAdministration) {
		Import-Module WebAdministration
		if (Test-Path "IIS:\Sites\SDM") {
			Write-Output "Recycling IIS site 'SDM'"
			try { Stop-Website -Name 'SDM' -ErrorAction SilentlyContinue } catch { }
			try { Start-Website -Name 'SDM' -ErrorAction SilentlyContinue } catch { }
		}
	}

	# Basic health check
	try {
		$healthUrl = 'http://localhost:5254/enroll'
		Write-Output "Performing health check against $healthUrl"
		$resp = Invoke-WebRequest -UseBasicParsing -Uri $healthUrl -TimeoutSec 10
		Write-Output "Health check HTTP status: $($resp.StatusCode)"
	} catch {
		Write-Error "Health check failed: $_"
		exit 1
	}

	Write-Output "Deployment completed successfully"
	exit 0
}
catch {
	Write-Error "Deployment failed: $_"
	exit 2
}
