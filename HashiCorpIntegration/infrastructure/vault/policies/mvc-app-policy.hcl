# mvc-app-policy.hcl
# Policy for MVC Application

# Allow reading dynamic database credentials
path "database/creds/app-role" {
  capabilities = ["read"]
}

# Allow reading static database credentials
path "database/static-creds/app-static-role" {
  capabilities = ["read"]
}

# Allow reading application configuration from KV store
path "kv/data/mvc-app/*" {
  capabilities = ["read"]
}

# Allow token renewal and lookup
path "auth/token/renew-self" {
  capabilities = ["update"]
}

path "auth/token/lookup-self" {
  capabilities = ["read"]
}
