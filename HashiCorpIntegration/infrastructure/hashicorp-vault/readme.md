# README.md: HashiCorp Vault + SQL Server Setup on Windows 11

## Overview

This guide explains how to set up HashiCorp Vault with a SQL Server database for learning and integration purposes on Windows 11 using Docker. It also includes steps for configuring Vault secrets engines, database roles, KV secrets, and AppRole authentication.

## Prerequisites

* Windows 11 with Docker Desktop installed.
* PowerShell 7+.
* Basic familiarity with Docker and Vault concepts.

## Step 1: Start the containers

1. Open PowerShell in the project folder.
2. Run the command to start Vault and SQL Server containers in detached mode:
3. First ensure are in infrastructure\hashicorp-vault folder

```powershell
docker-compose up -d
```

## Step 2: Install `sqlcmd` inside SQL Server container

1. Open an interactive shell inside the SQL Server container:

```powershell
docker exec -it --user root vault_sqlserver bash
```

2. Inside the container, install SQL Server tools:

```bash
apt-get update
apt-get install -y mssql-tools unixodbc-dev
```

3. Add `sqlcmd` to PATH:

```bash
echo 'export PATH=$PATH:/opt/mssql-tools/bin' >> ~/.bashrc
source ~/.bashrc
```

4. Test the connection:

```bash
sqlcmd -S localhost -U sa -P "VaultTest123!" -Q "SELECT @@VERSION"
```

5. Exit the container:

```bash
exit
```

## Step 3: Run Vault setup script

```powershell
.\vault-setup.ps1
```

## Step 4: For the first time use DefaultConnection for databse connection and then run Update-Database command
## After migration again back to vault connection.

The script will:

* Wait for Vault and SQL Server to be ready.
* Create the `HashiCorpIntegration` database.
* Enable KV v2 and database secrets engines.
* Configure dynamic and static database roles.
* Create sample KV secrets.
* Enable AppRole authentication and create policies.
* Output AppRole credentials for testing.

## Step 4: Verify Vault Setup

### Vault UI

* Open [http://localhost:8200](http://localhost:8200)
* Use the root token from the setup script.

### Check Dynamic Database Credentials

```powershell
docker exec hashicorp_vault vault read database/creds/app-role
```

### Check Static Database Credentials

```powershell
docker exec hashicorp_vault vault read database/static-creds/app-static-role
```

### Check KV Secrets

```powershell
docker exec hashicorp_vault vault kv get kv/hashicorp-integration/config
```

## Step 5: Retrieve Important Vault Information

1. Vault status:

```powershell
docker exec hashicorp_vault vault status
```

2. List all enabled secrets engines:

```powershell
docker exec hashicorp_vault vault secrets list
```

3. List all policies:

```powershell
docker exec hashicorp_vault vault policy list
```

4. Retrieve AppRole credentials:

```powershell
docker exec hashicorp_vault vault read -field=role_id auth/approle/role/hashicorp-integration-app/role-id
```

```powershell
docker exec hashicorp_vault vault write -field=secret_id -f auth/approle/role/hashicorp-integration-app/secret-id
```

## Step 6: Quick Test Commands

* Dynamic credentials:

```powershell
docker exec hashicorp_vault vault read database/creds/app-role
```

* Static credentials:

```powershell
docker exec hashicorp_vault vault read database/static-creds/app-static-role
```

* KV secrets:

```powershell
docker exec hashicorp_vault vault kv get kv/hashicorp-integration/config
```

## Reading KV Secrets from Vault using PowerShell

Use the following PowerShell script to retrieve secret data from HashiCorp Vault's KV v2 engine:

```powershell
$vaultToken = "ISHw8d2gDei24Q0IOex4fMIciBLP3gFRs55pwCPi0RuJf6fXy7qxqhPTfqdMXwXV"
$uri = "http://localhost:8200/v1/kv-v2/data/hashicorp-integration/config"
$headers = @{ "X-Vault-Token" = $vaultToken }

$response = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get
$response.data.data
``` 

## Step 7: Optional - Make SQLCMD Permanent

* Build a custom SQL Server Docker image with `sqlcmd` installed.
* Update docker-compose.yml to use the custom image.
* Rebuild containers:

```powershell
docker-compose up -d --build
```

## Tips

* Use Vault root token or AppRole token for initial testing.
* Check container logs if something doesn’t work:

```powershell
docker logs hashicorp_vault
docker logs vault_sqlserver
```

* For learning Vault database secrets, try creating dynamic credentials and revoking them to see automatic user lifecycle.
