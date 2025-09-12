# vault-admin-policy.hcl
# Administrative policy for Vault management

# Full access to system health and status
path "sys/health" {
  capabilities = ["read"]
}

path "sys/seal-status" {
  capabilities = ["read"]
}

# Manage auth methods
path "sys/auth/*" {
  capabilities = ["create", "read", "update", "delete", "list"]
}

# Manage secrets engines
path "sys/mounts/*" {
  capabilities = ["create", "read", "update", "delete", "list"]
}

# Manage policies
path "sys/policies/acl/*" {
  capabilities = ["create", "read", "update", "delete", "list"]
}

# Database secrets management
path "database/*" {
  capabilities = ["create", "read", "update", "delete", "list"]
}

# KV secrets management  
path "kv/*" {
  capabilities = ["create", "read", "update", "delete", "list"]
}

# AppRole management
path "auth/approle/*" {
  capabilities = ["create", "read", "update", "delete", "list"]
}