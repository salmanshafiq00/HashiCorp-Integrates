# 3-setup-database.sh  
#!/bin/bash
set -e

echo "=== Configuring database connections ==="

# Wait a moment for engines to be fully ready
sleep 2

# Configure MSSQL database connection
echo "Setting up MSSQL connection..."

# Check if database config already exists
if vault list database/config/ 2>/dev/null | grep -q "mssql-database"; then
  echo "MSSQL database connection already configured"
else
  vault write database/config/mssql-database \
      plugin_name=mssql-database-plugin \
      connection_url="sqlserver://{{username}}:{{password}}@bongobani.com:1433?database=HashiCorpIntegration" \
      allowed_roles="app-role,app-static-role" \
      username="asdinc" \
      password="ASD007" \
      verify_connection=false
  echo "[OK] MSSQL database connection configured"
fi

# Create dynamic role for temporary credentials
echo "Creating dynamic database role..."
vault write database/roles/app-role \
    db_name=mssql-database \
    creation_statements="CREATE LOGIN [{{name}}] WITH PASSWORD = '{{password}}'; \
USE [HashiCorpIntegration]; \
CREATE USER [{{name}}] FOR LOGIN [{{name}}]; \
ALTER ROLE [db_datareader] ADD MEMBER [{{name}}]; \
ALTER ROLE [db_datawriter] ADD MEMBER [{{name}}]; \
ALTER ROLE [db_ddladmin] ADD MEMBER [{{name}}];" \
    revocation_statements="USE [HashiCorpIntegration]; DROP USER IF EXISTS [{{name}}]; DROP LOGIN IF EXISTS [{{name}}];" \
    default_ttl="1h" \
    max_ttl="24h"

echo "[OK] Dynamic database role created"

# Create static role for credential rotation
echo "Creating static database role..."
vault write database/static-roles/app-static-role \
    db_name=mssql-database \
    username="asdinc_static" \
    rotation_period="24h"

echo "[OK] Static database role created"

# Test database connection (but don't fail if it doesn't work)
echo "Testing database connection..."
if vault read database/creds/app-role > /dev/null 2>&1; then
    echo "[OK] Database dynamic credentials working"
else
    echo "Warning: Database dynamic credentials test failed (this may be normal if the database server is not accessible)"
fi

echo "[OK] Database configuration complete"