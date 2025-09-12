# 4-setup-policies.sh
#!/bin/bash
set -e

echo "=== Setting up Vault policies ==="

# Create policy files if they don't exist (fallback)
mkdir -p /tmp/policies

# MVC App Policy
cat > /tmp/policies/mvc-app-policy.hcl << 'EOF'
# Allow reading dynamic database credentials
path "database/creds/app-role" {
  capabilities = ["read"]
}

# Allow reading static database credentials
path "database/static-creds/app-static-role" {
  capabilities = ["read"]  
}

# Allow reading application configuration
path "kv/data/mvc-app/*" {
  capabilities = ["read"]
}

# Allow token operations
path "auth/token/renew-self" {
  capabilities = ["update"]
}

path "auth/token/lookup-self" {
  capabilities = ["read"]
}
EOF

# API Service Policy
cat > /tmp/policies/api-service-policy.hcl << 'EOF'
# Allow reading dynamic database credentials
path "database/creds/app-role" {
  capabilities = ["read"]
}

# Allow reading static database credentials
path "database/static-creds/app-static-role" {
  capabilities = ["read"]
}

# Allow reading API configuration
path "kv/data/api-service/*" {
  capabilities = ["read"]
}

# Allow token operations
path "auth/token/renew-self" {
  capabilities = ["update"]
}

path "auth/token/lookup-self" {
  capabilities = ["read"]
}
EOF

# Use mounted policies if available, otherwise use fallback
POLICY_DIR="/policies"
if [ ! -d "$POLICY_DIR" ]; then
    echo "Using fallback policies from /tmp/policies"
    POLICY_DIR="/tmp/policies"
fi

echo "Creating policies..."
vault policy write mvc-app-policy "$POLICY_DIR/mvc-app-policy.hcl"
vault policy write api-service-policy "$POLICY_DIR/api-service-policy.hcl"

echo "Current policies:"
vault policy list

echo "✓ Policies setup complete"
