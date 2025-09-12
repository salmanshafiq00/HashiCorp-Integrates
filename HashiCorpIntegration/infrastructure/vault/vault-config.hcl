# HashiCorp Vault Configuration File for Persistent Storage

# Storage backend - File storage for persistence
storage "file" {
  path = "/vault/data"
}

# Listener configuration - HTTP only for development
listener "tcp" {
  address     = "0.0.0.0:8200"
  tls_disable = 1
}

# API and Cluster addresses
api_addr = "http://0.0.0.0:8200"
cluster_addr = "https://0.0.0.0:8201"

# Disable mlock for containerized environments
disable_mlock = true

# Enable UI
ui = true

# Log configuration
log_level = "INFO"
log_format = "standard"

# Lease configuration
default_lease_ttl = "768h"    # 32 days
max_lease_ttl = "8760h"       # 365 days

# Performance and limits
raw_storage_endpoint = true
introspection_endpoint = true

# Disable clustering for single-node setup
cluster_name = "company-vault-dev"

# Plugin directory
plugin_directory = "/vault/plugins"