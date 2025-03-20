# OutThink Email Injector App

## Version: 1.0

A secure console application that injects emails into customer inboxes without requiring provider access to Azure subscriptions.

---

## 📌 Overview

This document provides:
- Steps to publish the console application on **Azure Marketplace**.
- Instructions for customers to **configure** the application using their own **Azure Key Vault**.
- Security details ensuring **no provider access** to the customer’s Azure subscription.

---

## 📍 PART 1: Publishing the App on Azure Marketplace

### Step 1: Register as a Publisher in Partner Center
1. Go to [Partner Center](https://partner.microsoft.com).
2. Sign in with a **Microsoft Business Account**.
3. Click **Become a Partner** → Select **Commercial Marketplace**.
4. Accept the terms and create your **Publisher Profile**.

### Step 2: Create a New Offer
1. In **Partner Center**, go to **Marketplace Offers**.
2. Click **New Offer**.
3. Select **Azure Application** as the offer type.
4. Enter:
   - **Offer Name:** OutThink Email Injection App
   - **Unique ID:** outthink-emailinjectorapp
   - **Publisher:** OutThink

### Step 3: Configure Offer Details
#### **General Information**
- **Category:** Security & Compliance
- **Description:** "A secure console application that injects emails into customer inboxes without requiring provider access to Azure subscriptions."
- **Keywords:** email, security, azure, key vault

#### **Pricing Model**
- Choose **Free**.

#### **Plan Setup**
1. Create a new **Plan** → Name it `Standard`.
2. **Deployment Model:** Free.
3. Provide a link to installation instructions (this document).

### Step 4: Upload & Configure the App
#### **Deployment Package**
- Provide a **download link** to a ZIP file (with the executable and a README).

#### **Technical Setup**
- **Hosting Model:** Customer Managed.
- **Authentication:** No direct authentication in Azure. Uses customer-provided Azure **Key Vault**.

### Step 5: Submit for Certification
1. Run the **self-validation test** in Partner Center.
2. Ensure all **metadata is correct**.
3. Click **Submit for Review**.
4. Microsoft will validate **security, compliance, and deployment**.
5. Once approved, the application will be **listed in Azure Marketplace**.

---

## 📍 PART 2: Customer Installation Guide

Customers must follow these steps **within their own Azure subscription**.

### Step 1: Create an Azure Key Vault
1. Open **[Azure Portal](https://portal.azure.com)**.
2. Navigate to **Key Vaults** → Click **Create**.
3. Fill in:
   - **Key Vault Name:** `kvemailinjectorapp`
   - **Subscription:** Select your Azure Subscription.
   - **Resource Group:** Choose or create a new one.
   - **Region:** Select a region close to your services.
   - **Pricing Tier:** Standard.
4. Click **Review + Create** → **Create**.

### Step 2: Add Secrets to Key Vault
1. Go to **Key Vault** → **Secrets** → Click **Generate/Import**.
2. Add the following **secrets**:
   - `ClientId`
   - `ClientSecret`
   - `OtApiKey`
   - `OTCustomerId`
   - `TenantId`
3. Click **Create** after entering each secret.

### Step 3: Grant App Access to Key Vault
1. Go to **Key Vault** → **Access Policies** → **Create**.
2. Assign **Secret Management** permissions:
   - **Select Principal:** Enter the app’s **ClientId**.
   - **Permissions:** Get, List (Secrets).
3. Click **Review + Assign**.

Alternatively, run the following **Azure CLI** command:

```sh
az keyvault set-policy --name kvemailinjectorapp --spn <APP_CLIENT_ID> --secret-permissions get list