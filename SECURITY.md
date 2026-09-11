# Security requirements

Database credentials and customer data must never be published to GitHub.
Keep the schema, migrations and placeholder configuration in source control;
keep real passwords, connection strings, access/refresh tokens, private keys,
database files, backups and customer records out of it.

## Configuration

- Use .NET user secrets for local connection strings. Use a deployment secret
  manager for production credentials. Never put them in tracked appsettings files.
- The local Compose database requires a password supplied through the environment
  and binds its port only to localhost. Production databases should be reachable
  only by the backend through a private network, with encrypted connections.
- Frontend code must access data through authenticated backend APIs. Never put
  database credentials in frontend environment variables or browser bundles.
- Do not log connection strings, passwords, authentication tokens or customer
  order contents. Keep EF sensitive-data logging disabled.
- Business writes must derive the account ID from authentication and check the
  stored restaurant owner. Never trust an owner ID or role sent by the client.

## Before committing

Review staged files for secrets and real customer data. The ignore rules protect
common local secret files and database exports, but cannot detect a password
pasted into source code or stop a forced add. Never commit real values in the
HTTP request examples. Enable GitHub secret scanning and push protection where
available; these repository settings are separate from the files in this project.

If a credential is published, revoke/rotate it immediately. Removing it in a
later commit does not remove it from repository history. Do not post credentials
or customer records in public issues when reporting a security problem.

## Current release boundary

This is an unfinished backend. Email verification delivery, production Data
Protection key storage and a supported runtime upgrade remain pending. Local
PostgreSQL authentication and persistence have been verified. See
[local development](docs/local-development.md).
