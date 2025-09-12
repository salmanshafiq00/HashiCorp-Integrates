# master-init.sh
#!/bin/bash
set -e

echo "=== Master Vault Initialization Script ==="

export VAULT_ADDR=http://vault:8200
export VAULT_SKIP_VERIFY=true
KEYS_FILE="/vault-init/vault-keys.json"

echo "Waiting for Vault to be ready..."

# Enhanced waiting with better error handling
for i in {1..60}; do
  if vault status > /dev/null 2>&1; then
    echo "Vault is responding (attempt $i)"
    break
  fi
  echo "Attempt $i: Waiting for Vault..."
  sleep 5
  
  if [ $i -eq 60 ]; then
    echo "ERROR: Vault did not become ready in time"
    exit 1
  fi
done

# Check initialization status
VAULT_STATUS=$(vault status -format=json 2>/dev/null || echo '{"initialized": false}')
IS_INITIALIZED=$(echo "$VAULT_STATUS" | jq -r '.initialized // false')

if [ "$IS_INITIALIZED" = "true" ]; then
  echo "Vault is already initialized"
  
  if [ -f "$KEYS_FILE" ]; then
    echo "Loading existing keys..."
    ROOT_TOKEN=$(jq -r '.root_token' "$KEYS_FILE")
    
    # Check if sealed
    IS_SEALED=$(echo "$VAULT_STATUS" | jq -r '.sealed // true')
    if [ "$IS_SEALED" = "true" ]; then
      echo "Unsealing Vault..."
      vault operator unseal "$(jq -r '.unseal_keys_b64[0]' "$KEYS_FILE")"
      vault operator unseal "$(jq -r '.unseal_keys_b64[1]' "$KEYS_FILE")"
      vault operator unseal "$(jq -r '.unseal_keys_b64[2]' "$KEYS_FILE")"
    fi
    
    export VAULT_TOKEN="$ROOT_TOKEN"
    echo "Vault ready with existing configuration"
  else
    echo "ERROR: Vault is initialized but no keys file found!"
    echo "Manual intervention required"
    exit 1
  fi
else
  echo "Initializing Vault for the first time..."
  
  # Ensure directory exists
  mkdir -p /vault-init
  
  # Initialize Vault
  vault operator init \
    -key-shares=5 \
    -key-threshold=3 \
    -format=json > "$KEYS_FILE"
  
  echo "Vault initialized! Keys saved to $KEYS_FILE"
  
  # Extract and use keys
  ROOT_TOKEN=$(jq -r '.root_token' "$KEYS_FILE")
  
  # Unseal Vault
  echo "Unsealing Vault..."
  vault operator unseal "$(jq -r '.unseal_keys_b64[0]' "$KEYS_FILE")"
  vault operator unseal "$(jq -r '.unseal_keys_b64[1]' "$KEYS_FILE")"
  vault operator unseal "$(jq -r '.unseal_keys_b64[2]' "$KEYS_FILE")"
  
  export VAULT_TOKEN="$ROOT_TOKEN"
  echo "Vault unsealed and ready"
fi

echo "Current Vault status:"
vault status

# Run setup scripts
echo "=== Running setup scripts ==="
/scripts/2-setup-engines.sh
/scripts/3-setup-database.sh  
/scripts/4-setup-policies.sh
/scripts/5-setup-approles.sh

echo "=== Vault setup completed successfully ==="
echo ""
echo "IMPORTANT INFORMATION:"
echo "======================"
echo "Root Token: $ROOT_TOKEN"
echo "Unseal keys and credentials are stored in: $KEYS_FILE"
echo "BACKUP THESE CREDENTIALS SECURELY!"
echo ""
echo "Access Vault UI at: http://localhost:8200"