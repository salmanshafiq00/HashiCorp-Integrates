# 5-setup-approles.sh
#!/bin/bash
set -e

echo "=== Setting up AppRoles ==="

# Create AppRole for MVC Application
echo "Creating AppRole for MVC Application..."
vault write auth/approle/role/mvc-app \
    token_policies="mvc-app-policy" \
    token_ttl=1h \
    token_max_ttl=4h \
    bind_secret_id=true \
    secret_id_ttl=10m \
    token_num_uses=0

# Create AppRole for API Service  
echo "Creating AppRole for API Service..."
vault write auth/approle/role/api-service \
    token_policies="api-service-policy" \
    token_ttl=1h \
    token_max_ttl=4h \
    bind_secret_id=true \
    secret_id_ttl=10m \
    token_num_uses=0

# Generate credentials
echo "Generating credentials for MVC Application..."
MVC_ROLE_ID=$(vault read -field=role_id auth/approle/role/mvc-app/role-id)
MVC_SECRET_ID=$(vault write -field=secret_id auth/approle/role/mvc-app/secret-id)

echo "Generating credentials for API Service..."  
API_ROLE_ID=$(vault read -field=role_id auth/approle/role/api-service/role-id)
API_SECRET_ID=$(vault write -field=secret_id auth/approle/role/api-service/secret-id)

# Save credentials
CREDS_FILE="/vault-init/approle-credentials.json"
cat > "$CREDS_FILE" << EOF
{
  "mvc_app": {
    "role_id": "$MVC_ROLE_ID",
    "secret_id": "$MVC_SECRET_ID"
  },
  "api_service": {
    "role_id": "$API_ROLE_ID", 
    "secret_id": "$API_SECRET_ID"
  }
}
EOF

echo "✓ Credentials saved to $CREDS_FILE"

# Test authentication
echo "Testing AppRole authentication..."

MVC_TOKEN=$(vault write -field=token auth/approle/login \
    role_id="$MVC_ROLE_ID" \
    secret_id="$MVC_SECRET_ID")

API_TOKEN=$(vault write -field=token auth/approle/login \
    role_id="$API_ROLE_ID" \
    secret_id="$API_SECRET_ID")

if [ -n "$MVC_TOKEN" ] && [ -n "$API_TOKEN" ]; then
    echo "✓ AppRole authentication tests successful"
else
    echo "✗ AppRole authentication tests failed"
fi

echo ""
echo "AppRole Credentials:"
echo "==================="
echo "MVC App - Role ID: $MVC_ROLE_ID"
echo "MVC App - Secret ID: $MVC_SECRET_ID"
echo ""  
echo "API Service - Role ID: $API_ROLE_ID"
echo "API Service - Secret ID: $API_SECRET_ID"
echo ""
echo "✓ AppRole setup complete"