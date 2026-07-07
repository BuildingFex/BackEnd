param(
    [string]$MySqlPassword = ""
)

$ErrorActionPreference = "Stop"
$apiDir = Join-Path $PSScriptRoot "BuildingFex.Api"
$localConfig = Join-Path $apiDir "appsettings.Local.json"

if (-not $MySqlPassword) {
    Write-Host "Configuracion local de BuildingFex (MySQL + Mercado Pago)" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "MySQL 8 esta instalado en tu PC. Ingresa la contraseña del usuario root."
    Write-Host "(La que definiste al instalar MySQL; dejala vacia y Enter si no tiene contraseña)"
    $secure = Read-Host "MySQL root password" -AsSecureString
    $MySqlPassword = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
}

$escaped = $MySqlPassword.Replace("'", "''")
$connection = "server=localhost;port=3306;user=root;password=$escaped;database=buildingfex"

$config = @{
    ConnectionStrings = @{
        DefaultConnection = $connection
    }
    MercadoPago = @{
        AccessToken      = "APP_USR-2528775726058438-070623-c0feca6584a8dea7e0f721e0be0948f7-3267434318"
        PublicKey        = "APP_USR-03744d5a-f2fa-4373-b22b-9b20fae5cc6a"
        WebhookSecret    = "0e30ffa5598db11adb333a2ac67416f555d273f301872741de4941f1a7c4176d"
        FrontendBaseUrl  = "http://localhost:5173"
        NotificationUrl  = "http://localhost:5001/api/v1/payments/webhook"
    }
} | ConvertTo-Json -Depth 4

Set-Content -Path $localConfig -Value $config -Encoding UTF8
Write-Host ""
Write-Host "Listo: $localConfig" -ForegroundColor Green
Write-Host ""
Write-Host "Siguiente paso:"
Write-Host "  cd BuildingFex.Api"
Write-Host "  dotnet run"
Write-Host ""
Write-Host "Frontend (otra terminal):"
Write-Host "  cd ..\Fronted"
Write-Host "  npm run dev"
Write-Host ""
Write-Host "Credenciales de prueba:"
Write-Host "  Admin:     admin@buildingfex.test / admin123"
Write-Host "  Residente: residente@buildingfex.test / residente123"
