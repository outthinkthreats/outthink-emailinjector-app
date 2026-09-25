# OutThink Direct Mail Injection (DMI) Proxy Application for Microsoft 365 / Azure

## Overview
This application is a **proxy service** that enables the OutThink platform to securely send notifications and simulation emails to end-users using Microsoft **Graph API**.
It is registered in the **Azure Marketplace** and can be installed within a customer's **Microsoft Azure environment** for seamless integration with the **OutThink Platform**.

## Features
- Secure email relay using Microsoft Graph API
- Seamless Azure integration
- Configurable authentication & access control
- Scalable architecture for enterprise use.

## Installation & Usage

Refer to the following public documentation for all installation instructions:
https://docs.outthink.io/direct-mail-injection-dmi-for-microsoft-365/

### App Service Plan selection

The Azure Marketplace deployment checks Linux App Service tier availability for the selected subscription and region, then offers supported choices from `B1`, `S1`, `P0v3`, and `P1v3`. `P0v3` is marked as the recommended minimum, while the lowest-cost available choice is selected initially.

Direct ARM template deployments default to `P0v3`. Override the `sku` parameter when that SKU is unavailable or a different capacity is required. Availability checks indicate regional tier support only; subscription quota and current Azure capacity can still cause deployment validation or provisioning to fail.

### Deployment modes

`publicDefault` creates a public Web App and a new public Key Vault. `existingKeyVaultPrivate` creates the private Web App networking, NAT egress IP, and a private endpoint to a customer-supplied Key Vault. `fullPrivate` also creates a new Key Vault with public access disabled.

For `existingKeyVaultPrivate`, the customer must grant the Web App's system-assigned identity permission to read secrets from the existing vault after deployment. This is intentionally outside the template so the mode works with either Key Vault RBAC or access-policy authorization.

Run `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Validate-AzureDeployment.ps1` before packaging the Managed Application artifacts.

## Monitoring & Troubleshooting
Check logs via Azure Monitor
Review API responses for error handling
Ensure credentials and permissions are properly configured, as instructed in the documentation.

## Support
For issues and enquiries, please raise an issue in this GitHub repository or contact OutThink Support.

## Copyright
Copyright 2025 OutThink Ltd.
