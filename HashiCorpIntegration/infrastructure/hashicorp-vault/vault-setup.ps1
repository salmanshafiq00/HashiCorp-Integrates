# vault-setup.ps1
Write-Host "Setting up HashiCorp Vault for HashiCorpIntegration database..." -ForegroundColor Green

# Set Vault environment variables
$env:VAULT_ADDR = "http://127.0.0.1:8200"
$env:VAULT_TOKEN = "ISHw8d2gDei24Q0IOex4fMIciBLP3gFRs55pwCPi0RuJf6fXy7qxqhPTfqdMXwXV"

# Wait for Vault to be ready
Write-Host "Waiting for Vault to be ready..." -ForegroundColor Yellow
do {
    Start-Sleep -Seconds 2
    $vaultStatus = docker exec hashicorp_vault vault status 2>$null
} while ($LASTEXITCODE -ne 0)

Write-Host "Vault is ready! Configuring..." -ForegroundColor Green

# Login to Vault
Write-Host "Authenticating with Vault..." -ForegroundColor Yellow
docker exec hashicorp_vault vault login $env:VAULT_TOKEN

# Wait for SQL Server to be ready
Write-Host "Waiting for SQL Server to be ready..." -ForegroundColor Yellow
do {
    Start-Sleep -Seconds 5
    $sqlReady = docker exec vault_sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "VaultTest123!" -Q "SELECT 1" 2>$null
} while ($LASTEXITCODE -ne 0)

Write-Host "SQL Server is ready!" -ForegroundColor Green

# Create HashiCorpIntegration database
Write-Host "Creating HashiCorpIntegration database..." -ForegroundColor Cyan
docker exec vault_sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "VaultTest123!" -Q "
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'HashiCorpIntegration')
BEGIN
    CREATE DATABASE [HashiCorpIntegration];
    PRINT 'HashiCorpIntegration database created successfully';
END
ELSE
BEGIN
    PRINT 'HashiCorpIntegration database already exists';
END
"

# 1. Enable KV v2 secrets engine at default path
Write-Host "`n1. Enabling KV v2 secrets engine..." -ForegroundColor Cyan
docker exec hashicorp_vault vault secrets enable kv-v2 2>$null

# 2. Enable Database secrets engine
Write-Host "`n2. Enabling database secrets engine..." -ForegroundColor Cyan
docker exec hashicorp_vault vault secrets enable database 2>$null

# 3. Configure SQL Server database connection - FIXED CONNECTION STRING
Write-Host "`n3. Configuring HashiCorpIntegration database connection..." -ForegroundColor Cyan
docker exec hashicorp_vault vault write database/config/hashicorp-integration `
    plugin_name=mssql-database-plugin `
    connection_url="sqlserver://sa:VaultTest123!@vault_sqlserver:1433?database=HashiCorpIntegration" `
    allowed_roles="app-role,app-static-role"

# 4. Create dynamic database role
Write-Host "`n4. Creating dynamic database role..." -ForegroundColor Cyan
$creationSQL = "USE [HashiCorpIntegration]; CREATE LOGIN [{{name}}] WITH PASSWORD = '{{password}}'; CREATE USER [{{name}}] FOR LOGIN [{{name}}]; GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO [{{name}}]; GRANT EXECUTE ON SCHEMA::dbo TO [{{name}}];"

$revocationSQL = "USE [HashiCorpIntegration]; DROP USER [{{name}}]; USE [master]; DROP LOGIN [{{name}}];"

docker exec hashicorp_vault vault write database/roles/app-role `
    db_name=hashicorp-integration `
    creation_statements="$creationSQL" `
    revocation_statements="$revocationSQL" `
    default_ttl="1h" `
    max_ttl="24h"

# 5. Create static database user
Write-Host "`n5. Creating static database user..." -ForegroundColor Cyan
docker exec vault_sqlserver /opt/mssql-tools/bin/sqlcmd -S localhost -U sa -P "VaultTest123!" -Q "
USE [HashiCorpIntegration];
IF NOT EXISTS (SELECT * FROM sys.sql_logins WHERE name = 'vault_static_user')
BEGIN
    CREATE LOGIN [vault_static_user] WITH PASSWORD = 'StaticPass123!';
    PRINT 'Created login vault_static_user';
END

IF NOT EXISTS (SELECT * FROM sys.database_principals WHERE name = 'vault_static_user')
BEGIN
    CREATE USER [vault_static_user] FOR LOGIN [vault_static_user];
    GRANT SELECT, INSERT, UPDATE, DELETE ON SCHEMA::dbo TO [vault_static_user];
    GRANT EXECUTE ON SCHEMA::dbo TO [vault_static_user];
    PRINT 'Created user vault_static_user and granted permissions';
END
"

# 6. Create static database role in Vault
Write-Host "`n6. Creating static database role..." -ForegroundColor Cyan
docker exec hashicorp_vault vault write database/static-roles/app-static-role `
    db_name=hashicorp-integration `
    username="vault_static_user" `
    rotation_period="24h"

# 7. Create sample KV secrets using default kv-v2 mount point
Write-Host "`n7. Creating sample KV secrets..." -ForegroundColor Cyan
docker exec hashicorp_vault vault kv put kv-v2/hashicorp-integration/config `
    database_connection="Server=localhost,1434;Database=HashiCorpIntegration;Integrated Security=false;" `
    api_key="sample-api-key-12345" `
    jwt_secret="sample-jwt-secret-67890" `
    encryption_key="sample-encryption-key-abcdef"

docker exec hashicorp_vault vault kv put kv-v2/hashicorp-integration/environments/dev `
    environment="Development" `
    debug="true" `
    log_level="Debug" `
    external_api_url="https://api-dev.example.com"

docker exec hashicorp_vault vault kv put kv-v2/hashicorp-integration/environments/prod `
    environment="Production" `
    debug="false" `
    log_level="Warning" `
    external_api_url="https://api.example.com"

# 8. Enable AppRole auth method
Write-Host "`n8. Enabling AppRole authentication..." -ForegroundColor Cyan
docker exec hashicorp_vault vault auth enable approle 2>$null

# 9. Create policy for your application
Write-Host "`n9. Creating application policy..." -ForegroundColor Cyan
$policyContent = @"
# Policy for HashiCorp Integration Application
path "kv-v2/data/hashicorp-integration/*" {
  capabilities = ["read", "list"]
}

path "database/creds/app-role" {
  capabilities = ["read"]
}

path "database/static-creds/app-static-role" {
  capabilities = ["read"]
}

path "auth/token/lookup-self" {
  capabilities = ["read"]
}
"@

$policyContent | Out-File -FilePath "integration-app-policy.hcl" -Encoding UTF8
docker cp integration-app-policy.hcl hashicorp_vault:/tmp/integration-app-policy.hcl
docker exec hashicorp_vault vault policy write hashicorp-integration-app /tmp/integration-app-policy.hcl
Remove-Item "integration-app-policy.hcl"

# 10. Create AppRole
Write-Host "`n10. Creating AppRole..." -ForegroundColor Cyan
docker exec hashicorp_vault vault write auth/approle/role/hashicorp-integration-app `
    token_policies="hashicorp-integration-app" `
    token_ttl=1h `
    token_max_ttl=4h

# 11. Get AppRole credentials
Write-Host "`n11. Getting AppRole credentials..." -ForegroundColor Cyan
$roleId = docker exec hashicorp_vault vault read -field=role_id auth/approle/role/hashicorp-integration-app/role-id
$secretId = docker exec hashicorp_vault vault write -field=secret_id -f auth/approle/role/hashicorp-integration-app/secret-id

Write-Host "`n=== SETUP COMPLETE ===" -ForegroundColor Green
Write-Host "`nVault UI: http://localhost:8200" -ForegroundColor White
Write-Host "Root Token: $env:VAULT_TOKEN" -ForegroundColor White
Write-Host "`nDatabase: HashiCorpIntegration" -ForegroundColor Yellow
Write-Host "SA Password: VaultTest123!" -ForegroundColor White
Write-Host "`nAppRole Credentials:" -ForegroundColor Yellow
Write-Host "Role ID: $roleId" -ForegroundColor White
Write-Host "Secret ID: $secretId" -ForegroundColor White

Write-Host "`n=== TESTING COMMANDS ===" -ForegroundColor Magenta
Write-Host "# Test dynamic credentials:" -ForegroundColor Gray
Write-Host "docker exec hashicorp_vault vault read database/creds/app-role" -ForegroundColor White

Write-Host "`n# Test static credentials:" -ForegroundColor Gray
Write-Host "docker exec hashicorp_vault vault read database/static-creds/app-static-role" -ForegroundColor White

Write-Host "`n# Test KV secrets:" -ForegroundColor Gray
Write-Host "docker exec hashicorp_vault vault kv get kv-v2/hashicorp-integration/config" -ForegroundColor White

# Test the setup
Write-Host "`n=== RUNNING TESTS ===" -ForegroundColor Magenta
Write-Host "Testing dynamic credentials..." -ForegroundColor Yellow
docker exec hashicorp_vault vault read database/creds/app-role

Write-Host "`nTesting static credentials..." -ForegroundColor Yellow
docker exec hashicorp_vault vault read database/static-creds/app-static-role

Write-Host "`nTesting KV secrets..." -ForegroundColor Yellow
docker exec hashicorp_vault vault kv get kv-v2/hashicorp-integration/config