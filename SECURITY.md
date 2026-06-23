# Security Policy

## Supported versions

The latest released version receives security fixes. Older versions are not
maintained — please update before reporting.

## Reporting a vulnerability

**Please do not open a public issue for security problems.**

Report privately via GitHub's **"Report a vulnerability"** button on the
repository's **Security** tab (Security advisories), or email
**ellesgabry5@gmail.com**.

Please include:
- a description of the issue and its impact,
- steps to reproduce (a proof of concept if possible),
- affected version and OS.

You can expect an initial response within a few days. Once a fix is available
and released, we will credit you in the advisory unless you prefer otherwise.

## Scope notes

OpenAdoration parses external files (Bibles, songs, media, backups) and loads
**in-process plugins at full trust**. Only install plugins from sources you
trust. Reports about file-parsing safety (XXE, zip-slip, zip-bomb), update
integrity, and plugin isolation are especially welcome.
