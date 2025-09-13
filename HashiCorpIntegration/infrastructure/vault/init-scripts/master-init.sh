#!/bin/sh

echo "=== Master Vault Initialization Script ==="
export VAULT_ADDR=http://vault:8200
export VAULT_SKIP_VERIFY=true
KEYS_FILE="/vault-init/vault-keys.json"

echo "Waiting for Vault to be ready..."

# Enhanced waiting with better error handling (sh compatible)
i=1
while [ $i -le 60 ]; do
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
  i=$((i + 1))
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
      vault operator unseal "$(jq -r '.unseal_keys_b64[0]' "$KEYS_FILE")" || exit 1
      vault operator unseal "$(jq -r '.unseal_keys_b64[1]' "$KEYS_FILE")" || exit 1
      vault operator unseal "$(jq -r '.unseal_keys_b64[2]' "$KEYS_FILE")" || exit 1
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
  
  if [ $? -ne 0 ]; then
    echo "ERROR: Failed to initialize Vault"
    exit 1
  fi
  
  echo "Vault initialized! Keys saved to $KEYS_FILE"
  
  # Extract and use keys
  ROOT_TOKEN=$(jq -r '.root_token' "$KEYS_FILE")
  
  # Unseal Vault
  echo "Unsealing Vault..."
  vault operator unseal "$(jq -r '.unseal_keys_b64[0]' "$KEYS_FILE")" || exit 1
  vault operator unseal "$(jq -r '.unseal_keys_b64[1]' "$KEYS_FILE")" || exit 1
  vault operator unseal "$(jq -r '.unseal_keys_b64[2]' "$KEYS_FILE")" || exit 1
  
  export VAULT_TOKEN="$ROOT_TOKEN"
  echo "Vault unsealed and ready"
fi

echo "Current Vault status:"
vault status

# Run setup scripts
echo "=== Running setup scripts ==="
/scripts/2-setup-engines.sh || { echo "Failed to setup engines"; exit 1; }
/scripts/3-setup-database.sh || { echo "Failed to setup database"; exit 1; }
/scripts/4-setup-policies.sh || { echo "Failed to setup policies"; exit 1; }
/scripts/5-setup-approles.sh || { echo "Failed to setup approles"; exit 1; }

echo "=== Vault setup completed successfully ==="
echo ""
echo "IMPORTANT INFORMATION:"
echo "======================"
echo "Root Token: $ROOT_TOKEN"
echo "Unseal keys and credentials are stored in: $KEYS_FILE"
echo "BACKUP THESE CREDENTIALS SECURELY!"
echo ""
echo "Access Vault UI at: http://localhost:8200"