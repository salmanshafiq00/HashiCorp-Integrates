# HashiCorp Vault Persistent Setup

This guide provides a complete setup for HashiCorp Vault with persistent storage for development and production environments.

## Directory Structure
```
infrastructure/
└── vault/
    ├── docker-compose.yml
    ├── vault-config.hcl
    ├── README.md
    ├── init-scripts/
    │   ├── master-init.sh
    │   ├── 2-setup-engines.sh
    │   ├── 3-setup-database.sh
    │   ├── 4-setup-policies.sh
    │   └── 5-setup-approles.sh
    └── policies/
        ├── mvc-app-policy.hcl
        ├── api-service-policy.hcl
        └── vault-admin-policy.hcl
```

## Prerequisites
- Docker and Docker Compose installed
- Access to your MSSQL database server
- Basic understanding of HashiCorp Vault concepts

## Quick Start

### 1. Clone/Setup Directory Structure
Ensure your directory structure matches the one shown above.

### 2. Start Vault
```bash
cd infrastructure/vault
docker-compose up -d vault
```

Option 2: Set a custom project name
Run Compose with -p to set your own project name:

```bash
docker-compose -p hashcorp-vault up -d vault
```

### 3. Initialize Vault (First Time Only)
```bash
docker-compose -p hashcorp-vault up vault-init
```

**IMPORTANT**: Save the output containing:
- Root token
- Unseal keys
- AppRole credentials

### 4. Access Vault UI
Open browser: http://localhost:8200
Login with the root token from step 3.

## Configuration Details

### Vault Storage
- Uses file storage backend for persistence
- Data stored in Docker volume `vault-data`
- Logs stored in Docker volume `vault-logs`

### Database Integration
- Configured for MSSQL Server
- Supports both dynamic and static database credentials
- Dynamic credentials: 1-hour TTL, 24-hour max TTL
- Static credentials: 24-hour rotation period

### Authentication
- AppRole authentication method enabled
- Separate roles for MVC app and API service
- Tokens valid for 1 hour, max 4 hours

## Application Integration

### For .NET Applications

Add to appsettings.json:
```json
{
  "Vault": {
    "VaultUrl": "http://localhost:8200",
    "DatabaseRole": "app-role",
    "StaticDatabaseRole": "app-static-role",
    "UseStaticCredentials": false,
    "DatabaseServer": "your-db-server",
    "DatabaseName": "your-database",
    "CacheExpirationMinutes": 30
  }
}
```

### Getting Database Credentials

#### Dynamic Credentials (Recommended)
```bash
# Get temporary database credentials
vault read database/creds/app-role
```

#### Static Credentials
```bash
# Get static database credentials
vault read database/static-creds/app-static-role
```

## Management Commands

### Check Vault Status
```bash
docker-compose exec vault vault status
```

### Unseal Vault (if sealed)
```bash
docker-compose exec vault vault operator unseal <unseal-key-1>
docker-compose exec vault vault operator unseal <unseal-key-2>
docker-compose exec vault vault operator unseal <unseal-key-3>
```

### View Logs
```bash
docker-compose logs -f vault
```

### Restart Services
```bash
docker-compose restart vault
```

### Stop Services
```bash
docker-compose down
```

## Security Best Practices

1. **Backup Unseal Keys**: Store unseal keys securely and separately
2. **Rotate Root Token**: Generate new root tokens periodically
3. **Use Policies**: Never use root token in applications
4. **Monitor Access**: Review audit logs regularly
5. **Network Security**: Use HTTPS in production
6. **Rotate Credentials**: Implement credential rotation

## Troubleshooting

### Vault Won't Start
1. Check logs: `docker-compose logs vault`
2. Verify file permissions on mounted volumes
3. Ensure no other services using port 8200

### Cannot Connect to Database
1. Verify database server accessibility
2. Check connection string in database configuration
3. Validate database credentials

### Authentication Issues
1. Verify AppRole credentials are correct
2. Check policy permissions
3. Ensure tokens haven't expired

### Vault is Sealed
1. Use unseal keys from initialization
2. Run unseal commands three times with different keys
3. Check if auto-unseal is configured

## Production Considerations

1. **TLS**: Enable TLS for all communications
2. **Auto-Unseal**: Configure auto-unseal with cloud KMS
3. **High Availability**: Use Consul or integrated storage for HA
4. **Monitoring**: Implement proper monitoring and alerting
5. **Backup Strategy**: Regular backups of Vault data
6. **Network Isolation**: Restrict network access to Vault

## Support

For issues or questions:
1. Check HashiCorp Vault documentation
2. Review Docker logs for error messages
3. Verify network connectivity and firewall rules