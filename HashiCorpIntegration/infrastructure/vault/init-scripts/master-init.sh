#!/bin/sh

echo "=== Master Vault Initialization Script ==="
export VAULT_ADDR=http://vault:8200
export VAULT_SKIP_VERIFY=true
KEYS_FILE="/vault-init/vault-keys.json"

echo "Waiting for Vault to be ready..."

# Enhanced waiting with better error handling and proper status checking
i=1
while [ $i -le 60 ]; do
  echo "Attempt $i: Checking Vault status..."
  
  # Try to get vault status - check if it's responding at all
  if wget --quiet --timeout=5 --tries=1 --spider "http://vault:8200/v1/sys/health" 2>/dev/null; then
    echo "Vault HTTP endpoint is responding"
    
    # Now try vault CLI command
    if vault status > /dev/null 2>&1; then
      echo "Vault CLI is working (attempt $i)"
      break
    else
      echo "Vault HTTP responding but CLI not ready yet..."
    fi
  else
    echo "Vault HTTP endpoint not ready yet..."
  fi
  
  sleep 5
  
  if [ $i -eq 60 ]; then
    echo "ERROR: Vault did not become ready in time"
    echo "Last status check:"
    vault status || echo "Vault status command failed"
    wget -O- --timeout=5 "http://vault:8200/v1/sys/health" 2>/dev/null || echo "HTTP health check failed"
    exit 1
  fi
  i=$((i + 1))
done

echo "Vault is ready! Getting status..."

# Check initialization status with better error handling
VAULT_STATUS_OUTPUT=$(vault status -format=json 2>&1)
VAULT_STATUS_EXIT=$?

if [ $VAULT_STATUS_EXIT -ne 0 ]; then
  echo "vault status command failed with exit code $VAULT_STATUS_EXIT"
  echo "Output: $VAULT_STATUS_OUTPUT"
  
  # Check if it's because vault is not initialized (this is expected on first run)
  if echo "$VAULT_STATUS_OUTPUT" | grep -q "not been initialized" || echo "$VAULT_STATUS_OUTPUT" | grep -q "Vault is sealed"; then
    echo "Vault is not initialized - this is expected on first run"
    IS_INITIALIZED="false"
  else
    echo "ERROR: Unexpected vault status error"
    exit 1
  fi
else
  IS_INITIALIZED=$(echo "$VAULT_STATUS_OUTPUT" | jq -r '.initialized // false' 2>/dev/null || echo "false")
fi

echo "Vault initialization status: $IS_INITIALIZED"

if [ "$IS_INITIALIZED" = "true" ]; then
  echo "Vault is already initialized"
  if [ -f "$KEYS_FILE" ]; then
    echo "Loading existing keys..."
    ROOT_TOKEN=$(jq -r '.root_token' "$KEYS_FILE")
    
    # Check if sealed
    IS_SEALED=$(echo "$VAULT_STATUS_OUTPUT" | jq -r '.sealed // true' 2>/dev/null || echo "true")
    echo "Vault sealed status: $IS_SEALED"
    
    if [ "$IS_SEALED" = "true" ]; then
      echo "Unsealing Vault..."
      vault operator unseal "$(jq -r '.unseal_keys_b64[0]' "$KEYS_FILE")" || { echo "Failed to unseal with key 1"; exit 1; }
      vault operator unseal "$(jq -r '.unseal_keys_b64[1]' "$KEYS_FILE")" || { echo "Failed to unseal with key 2"; exit 1; }
      vault operator unseal "$(jq -r '.unseal_keys_b64[2]' "$KEYS_FILE")" || { echo "Failed to unseal with key 3"; exit 1; }
      echo "Vault unsealed successfully"
    else
      echo "Vault is already unsealed"
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
  echo "Running vault operator init..."
  vault operator init \
    -key-shares=5 \
    -key-threshold=3 \
    -format=json > "$KEYS_FILE"
  
  if [ $? -ne 0 ]; then
    echo "ERROR: Failed to initialize Vault"
    cat "$KEYS_FILE" 2>/dev/null || echo "No output file created"
    exit 1
  fi
  
  echo "Vault initialized! Keys saved to $KEYS_FILE"
  
  # Verify keys file was created and has content
  if [ ! -s "$KEYS_FILE" ]; then
    echo "ERROR: Keys file is empty or not created"
    exit 1
  fi
  
  # Extract and use keys
  ROOT_TOKEN=$(jq -r '.root_token' "$KEYS_FILE")
  if [ -z "$ROOT_TOKEN" ] || [ "$ROOT_TOKEN" = "null" ]; then
    echo "ERROR: Could not extract root token"
    cat "$KEYS_FILE"
    exit 1
  fi
  
  # Unseal Vault
  echo "Unsealing Vault..."
  vault operator unseal "$(jq -r '.unseal_keys_b64[0]' "$KEYS_FILE")" || { echo "Failed to unseal with key 1"; exit 1; }
  vault operator unseal "$(jq -r '.unseal_keys_b64[1]' "$KEYS_FILE")" || { echo "Failed to unseal with key 2"; exit 1; }
  vault operator unseal "$(jq -r '.unseal_keys_b64[2]' "$KEYS_FILE")" || { echo "Failed to unseal with key 3"; exit 1; }
  
  export VAULT_TOKEN="$ROOT_TOKEN"
  echo "Vault unsealed and ready"
fi

echo "Current Vault status:"
vault status

# Verify we can authenticate
echo "Testing authentication..."
if vault token lookup > /dev/null 2>&1; then
  echo "Authentication successful"
else
  echo "ERROR: Authentication failed"
  exit 1
fi

# Run setup scripts
echo "=== Running setup scripts ==="

# Make sure scripts are executable
chmod +x /scripts/*.sh

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