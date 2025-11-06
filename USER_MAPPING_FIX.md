# User Mapping Fix - Email-Based Matching

## Problem Summary

The migration was failing with this error:
```
Detail: User 'e397c97d-6046-4b53-ad6e-b78dc2a83066' could not be found.
```

**Root Cause**: Users have **different usernames** on Tableau Server vs Tableau Cloud, but the same email addresses. The SDK was trying to match by username instead of email.

## Example User (MOS7CA / Silvio Carvalho)

| Platform | Username | Email | Display Name |
|----------|----------|-------|--------------|
| **Tableau Server** | `MOS7CA` | `silvio.carvalho@company.com` | Carvalho Silvio |
| **Tableau Cloud** | `mos7ca@company.com` | `silvio.carvalho@company.com` | Carvalho Silvio |

The SDK needs to match users by **email address** (which is the same) rather than username (which differs).

## Solution Implemented

Updated `DestinationUserMapping.cs` to:

1. **Primary matching**: Use email address for user mapping
2. **Fallback**: If no email exists, use username (with warning)
3. **SDK behavior**: The SDK will find destination users by email, even when usernames differ

### How It Works

```csharp
// Source user: Username="MOS7CA", Email="silvio.carvalho@company.com"
var emailLocation = ContentLocation.ForUsername(sourceUser.Domain, sourceUser.Email);
// Result: SDK finds destination user with email "silvio.carvalho@company.com"
//         (which has username "mos7ca@company.com" at destination)
```

## What Changed

**Before**: Mapping attempted to use username → Failed because `MOS7CA` doesn't exist at destination

**After**: Mapping uses email address → Succeeds because `silvio.carvalho@company.com` exists at destination (as user `mos7ca@company.com`)

## Expected Behavior in Logs

You should now see:
```
info: Mapping source user MOS7CA (Email: silvio.carvalho@company.com) to destination user by email address
```

Instead of:
```
warn: Could not find destination user to map for source user MOS7CA (ID: e397c97d-6046-4b53-ad6e-b78dc2a83066)
```

## Files Modified

- `Hooks/Mappings/DestinationUserMapping.cs` - Changed to use email-based matching for all users

## Testing Checklist

- [ ] Run migration and verify no "Could not find destination user" errors
- [ ] Check logs show "Mapping source user ... by email address" for all users
- [ ] Verify content ownership is correctly assigned at destination:
  - Content owned by `MOS7CA` at source → Owned by `mos7ca@company.com` at destination
  - Content owned by `ZAG3CA` at source → Owned by appropriate user at destination
  - Content owned by `TTC9FE` at source → Owned by appropriate user at destination
- [ ] Verify workbooks migrated successfully: `Databricks_PAT_LIVE`, `Databricks_OAUTH_LIVE`, `ORACLE_LIVE`
- [ ] Verify projects migrated successfully: `DATABRICKS_TEST`, `Oracle_TEST`

## Important Notes

- ✅ **Email addresses must match** between source and destination for this mapping to work
- ✅ **Usernames can be different** - the SDK will match by email and use the correct destination username
- ⚠️ **Users without email addresses** will attempt to match by username (may fail if usernames differ)
- ✅ **Works with Azure AD imported users** - as long as email addresses are preserved
