# ?? Local Development Setup - Secrets Management

## ?? IMPORTANT: Never Commit Real Secrets

This project uses **environment variables** and **template files** to manage secrets safely.

---

## ?? Required Environment Variables

Set these **before** building the project:

### **Windows (PowerShell):**
```powershell
# OpenAI API Keys
$env:QUICKPROMPT_GPTApiKeys__Key1="your_openai_key_1_here"
$env:QUICKPROMPT_GPTApiKeys__Key2="your_openai_key_2_here"

# Firebase Settings
$env:QUICKPROMPT_FirebaseSettings__ApiKey="your_firebase_api_key_here"

# Android Keystore (Release builds only)
$env:AndroidKeystorePath="C:\path\to\your\keystore.keystore"
$env:AndroidStorePassword="your_keystore_password"
$env:AndroidKeyPassword="your_key_password"
```

### **macOS/Linux (Bash/Zsh):**
```bash
# OpenAI API Keys
export QUICKPROMPT_GPTApiKeys__Key1="your_openai_key_1_here"
export QUICKPROMPT_GPTApiKeys__Key2="your_openai_key_2_here"

# Firebase Settings
export QUICKPROMPT_FirebaseSettings__ApiKey="your_firebase_api_key_here"

# Android Keystore (Release builds only)
export AndroidKeystorePath="/path/to/your/keystore.keystore"
export AndroidStorePassword="your_keystore_password"
export AndroidKeyPassword="your_key_password"
```

---

## ??? Setup Instructions

### **1. Copy Template File**

```bash
cp appsettings.template.json appsettings.json
```

### **2. Fill in Real Values**

Edit `appsettings.json` (?? **NEVER commit this file**):

```json
{
  "GPTApiKeys": {
    "Key1": "sk-proj-REAL_KEY_HERE",
    "Key2": "sk-proj-REAL_KEY_HERE"
  },
  "FirebaseSettings": {
    "ApiKey": "REAL_FIREBASE_KEY_HERE"
  }
}
```

### **3. Verify .gitignore**

Ensure `appsettings.json` is in `.gitignore`:

```bash
git check-ignore appsettings.json
# Should output: appsettings.json
```

---

## ?? Getting API Keys

### **OpenAI API Keys:**
1. Go to: https://platform.openai.com/api-keys
2. Click "Create new secret key"
3. Copy the key (you won't see it again!)
4. Store in password manager (1Password, Bitwarden, etc.)

### **Firebase API Key:**
1. Go to: Firebase Console ? Project Settings ? General
2. Copy "Web API Key"

### **Android Keystore:**
Generate new keystore:

```bash
keytool -genkey -v -keystore quickprompt-release.keystore \
  -alias QuickPromptAlias \
  -keyalg RSA -keysize 2048 -validity 10000
```

---

## ?? CI/CD (GitHub Actions)

Secrets are stored in **GitHub Secrets** (not in code):

1. Go to: Repository ? Settings ? Secrets and variables ? Actions
2. Add secrets:
   - `OPENAI_API_KEY_1`
   - `OPENAI_API_KEY_2`
   - `FIREBASE_API_KEY`
   - `ANDROID_KEYSTORE_PASSWORD`
   - `ANDROID_KEY_PASSWORD`
   - `ANDROID_KEYSTORE_BASE64` (keystore file encoded as base64)

---

## ? Verification

Before committing, verify no secrets are exposed:

```bash
# Check for leaked secrets
git diff --cached | grep -i "sk-proj"
git diff --cached | grep -i "AIzaSy"

# Should return nothing
```

---

## ?? If Secrets Are Accidentally Committed

1. **IMMEDIATELY** revoke the exposed keys:
   - OpenAI: https://platform.openai.com/api-keys
   - Firebase: Regenerate in console

2. Remove from Git history:
```bash
# Using BFG Repo Cleaner
java -jar bfg.jar --delete-files appsettings.json
git reflog expire --expire=now --all
git gc --prune=now --aggressive
git push --force
```

3. **NOTIFY TEAM** to re-clone repository

---

## ?? Support

If you need access to secrets, contact:
- **Project Lead:** [Your Name]
- **DevOps:** [DevOps Contact]

**Never share secrets via:**
- ? Email
- ? Slack/Teams messages
- ? Code comments
- ? Git commits

**Always use:**
- ? Password manager
- ? Encrypted communication
- ? Environment variables
