# 2-setup-engines.sh
#!/bin/bash
set -e

echo "=== Setting up secrets engines ==="

# Enable database secrets engine
if ! vault secrets list -format=json | jq -r 'keys[]' | grep -q "database/"; then
  echo "Enabling database secrets engine..."
  vault secrets enable database
else
  echo "Database secrets engine already enabled"
fi

# Enable AppRole auth method  
if ! vault auth list -format=json | jq -r 'keys[]' | grep -q "approle/"; then
  echo "Enabling AppRole auth method..."
  vault auth enable approle
else
  echo "AppRole auth method already enabled"
fi

# Enable KV v2 for application configs
if ! vault secrets list -format=json | jq -r 'keys[]' | grep -q "kv/"; then
  echo "Enabling KV v2 secrets engine..."
  vault secrets enable -version=2 -path=kv kv
else
  echo "KV v2 secrets engine already enabled"
fi

echo "✓ Secrets engines setup complete"