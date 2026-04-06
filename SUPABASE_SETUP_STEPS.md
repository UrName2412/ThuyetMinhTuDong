# 🎯 SUPABASE SETUP - Step by Step

## 📋 Complete Setup Instructions

Follow these steps **exactly** to setup soft delete in Supabase.

---

## ⏱️ Time Required: 5 Minutes

---

## 🚀 Step 1: Open Supabase SQL Editor

1. Go to [Supabase Dashboard](https://supabase.com)
2. Select your project
3. Click **SQL Editor** in left sidebar
4. Click **New Query** button
5. You'll see an empty SQL editor

```
┌─────────────────────────────────────┐
│ SQL Editor                          │
│                                     │
│ ┌──────────────────────────────────┐│
│ │ SELECT * FROM poi;  ← Example    ││
│ └──────────────────────────────────┘│
│                                     │
│ [Run] [Save] [Format]              │
└─────────────────────────────────────┘
```

---

## 🔄 Step 2: Copy SQL Code

1. Open file: `SUPABASE_MIGRATIONS.sql` (in your project)
2. **Select All** (Ctrl+A)
3. **Copy** (Ctrl+C)

```
SUPABASE_MIGRATIONS.sql contains:
├─ ALTER TABLE poi...           (new columns)
├─ CREATE INDEX...              (performance)
├─ CREATE OR REPLACE VIEW...    (active_pois)
├─ CREATE TABLE poi_audit_log   (audit trail)
├─ CREATE FUNCTION...           (utilities)
└─ CREATE TRIGGER...            (auto-logging)
```

---

## 📝 Step 3: Paste into Supabase

1. Click in the SQL Editor text area
2. **Paste All** (Ctrl+V)
3. You should see all the SQL code

```
The editor should now show:
-- ===================================================================
-- SUPABASE MIGRATIONS - POI Soft Delete Implementation
-- ===================================================================

ALTER TABLE poi ADD COLUMN is_deleted BOOLEAN...
[... many lines of SQL ...]
```

---

## ▶️ Step 4: Execute SQL

### **Option A: Execute All at Once (Recommended)**
1. Press `Ctrl+Enter` 
   OR
2. Click the **Run** button (▶️ icon)

### **Option B: Execute Individual Statements**
1. Highlight one statement
2. Press `Ctrl+Enter`
3. Repeat for each statement

```
⚠️ Important: Execute the ENTIRE file, not just parts!
All statements are interdependent.
```

---

## ✅ Step 5: Verify Success

### **Check 1: New Columns Added**
```sql
-- In SQL Editor, run this query:
SELECT column_name, data_type 
FROM information_schema.columns 
WHERE table_name='poi'
ORDER BY ordinal_position;

-- You should see:
-- is_deleted (boolean)
-- deleted_at (timestamp)
-- deleted_by (text)
```

### **Check 2: New View Created**
```sql
-- Query:
SELECT * FROM active_pois LIMIT 1;

-- If it works, view is created ✅
-- If error, view creation failed ❌
```

### **Check 3: Audit Table Exists**
```sql
-- Query:
SELECT * FROM poi_audit_log LIMIT 1;

-- If it works (even if empty), table exists ✅
```

### **Check 4: Functions Work**
```sql
-- Query:
SELECT soft_delete_poi(1, 'test');

-- If successful message, function works ✅
-- It will soft-delete POI with id=1
```

---

## 🔍 Verification Results

### **Success Indicators** ✅
```
✅ All SQL executed without errors
✅ No red error messages
✅ New columns visible in poi table
✅ active_pois view queryable
✅ poi_audit_log table exists
✅ Functions callable
```

### **If Something Fails** ❌
```
1. Check the error message
2. Scroll up in SQL editor to see full error
3. Common issues:
   - Syntax error → Check SQL file is complete
   - Table doesn't exist → Wrong table name?
   - Permission denied → Check role permissions
   
4. Contact support if stuck
```

---

## 🎯 What Each Statement Does

### **Part 1: Schema Changes** (Lines 1-15)
```sql
ALTER TABLE poi ADD COLUMN is_deleted BOOLEAN DEFAULT false;
ALTER TABLE poi ADD COLUMN deleted_at TIMESTAMP;
ALTER TABLE poi ADD COLUMN deleted_by TEXT;

CREATE INDEX idx_poi_is_deleted ON poi(is_deleted);
CREATE INDEX idx_poi_deleted_at ON poi(deleted_at);
```
**Purpose**: Add soft delete tracking to POI table

### **Part 2: Active POIs View** (Lines 18-24)
```sql
CREATE OR REPLACE VIEW active_pois AS
SELECT * FROM poi
WHERE is_deleted = false
ORDER BY id;
```
**Purpose**: Easy query for non-deleted POIs

### **Part 3: Audit Log Table** (Lines 27-40)
```sql
CREATE TABLE IF NOT EXISTS poi_audit_log (
    id BIGSERIAL PRIMARY KEY,
    poi_id INTEGER NOT NULL,
    action TEXT NOT NULL,
    ...
);
```
**Purpose**: Track all deletion/restoration actions

### **Part 4: RLS Policies** (Lines 43-60)
```sql
CREATE POLICY "anon_select_active_pois" ON poi
FOR SELECT
USING (is_deleted = false);
```
**Purpose**: Ensure anonymous users only see active POIs

### **Part 5: Triggers & Functions** (Lines 63-120)
```sql
CREATE FUNCTION log_poi_deletion() ...
CREATE TRIGGER poi_deletion_trigger ...
CREATE FUNCTION soft_delete_poi() ...
CREATE FUNCTION restore_poi() ...
CREATE FUNCTION cleanup_deleted_pois() ...
```
**Purpose**: Automation for deletions, restorations, cleanup

---

## 🧪 Test the Setup

### **Test 1: Soft Delete a POI**
```sql
-- Soft delete POI with id = 1
UPDATE poi SET is_deleted = true, deleted_at = NOW() WHERE id = 1;

-- Verify it's deleted
SELECT * FROM poi WHERE id = 1;
-- Should show: is_deleted = true

-- Query active POIs
SELECT * FROM active_pois;
-- Should NOT show POI #1
```

### **Test 2: Restore a POI**
```sql
-- Restore POI #1
UPDATE poi SET is_deleted = false, deleted_at = NULL WHERE id = 1;

-- Query active POIs
SELECT * FROM active_pois;
-- Should show POI #1 again ✅
```

### **Test 3: Check Audit Log**
```sql
-- View all audit entries
SELECT * FROM poi_audit_log ORDER BY created_at DESC;

-- Should show deletion/restoration entries
```

### **Test 4: Use Utility Function**
```sql
-- Soft delete using function
SELECT soft_delete_poi(2, 'admin');

-- Restore using function
SELECT restore_poi(2);

-- Cleanup old deleted (example)
SELECT cleanup_deleted_pois(90);
```

---

## 📊 Verification Checklist

After completing setup:

### **Database Structure**
- [ ] POI table has `is_deleted` column
- [ ] POI table has `deleted_at` column
- [ ] POI table has `deleted_by` column
- [ ] Indexes created on `is_deleted` and `deleted_at`

### **Views & Tables**
- [ ] `active_pois` view exists and queryable
- [ ] `poi_audit_log` table exists
- [ ] Can query audit log

### **Functions**
- [ ] `soft_delete_poi()` function works
- [ ] `restore_poi()` function works
- [ ] `cleanup_deleted_pois()` function works

### **Policies**
- [ ] RLS policies applied (if using RLS)
- [ ] Anonymous users only see active POIs

### **Data**
- [ ] Existing POIs have `is_deleted = false`
- [ ] Can soft delete and restore POIs
- [ ] Audit log records changes

---

## 🎉 Success!

If all checks pass, you're done with Supabase setup! ✅

### **Next Steps:**
1. App code is already updated (no changes needed)
2. Deploy your app
3. App will auto-sync and use soft delete

---

## ❓ Troubleshooting

### **Error: "Relation does not exist"**
```
Cause: Table poi doesn't exist
Solution: Create POI table first, then run migrations
```

### **Error: "Column already exists"**
```
Cause: Columns were already added
Solution: This is fine, the migration is idempotent
Alternative: DROP columns first, then re-run
```

### **Error: "Function already exists"**
```
Cause: Function created multiple times
Solution: Ignore, OR use CREATE OR REPLACE pattern
Already handled in SUPABASE_MIGRATIONS.sql ✅
```

### **View won't query**
```
Cause: View definition error
Solution: Check syntax in SQL file
Try: SELECT * FROM poi WHERE is_deleted = false;
If works, view issue isn't critical
```

### **No error, but nothing happened**
```
Possible Causes:
1. SQL didn't execute (click Run button)
2. SQL is incomplete (copy entire file)
3. Wrong database selected (check top of editor)

Solution:
1. Click Run button again
2. Copy entire SUPABASE_MIGRATIONS.sql
3. Verify you're in correct project
```

---

## 📞 Getting Help

### **Supabase Documentation**
- SQL Editor: https://supabase.com/docs/guides/database/sql-editor
- Migrations: https://supabase.com/docs/guides/migrations

### **Check Logs**
1. Go to **Logs** in Supabase Dashboard
2. Look for SQL Editor logs
3. Check for errors

### **Rollback (If Needed)**
```sql
-- Drop soft delete columns
ALTER TABLE poi DROP COLUMN IF EXISTS is_deleted;
ALTER TABLE poi DROP COLUMN IF EXISTS deleted_at;
ALTER TABLE poi DROP COLUMN IF EXISTS deleted_by;

-- Drop view
DROP VIEW IF EXISTS active_pois;

-- Drop tables
DROP TABLE IF EXISTS poi_audit_log;

-- Drop functions (handle manually if needed)
```

---

## ✨ Summary

| Step | Action | Time |
|------|--------|------|
| 1 | Open Supabase SQL Editor | 1 min |
| 2 | Copy `SUPABASE_MIGRATIONS.sql` | 30 sec |
| 3 | Paste into SQL Editor | 30 sec |
| 4 | Click Run/Execute | 10 sec |
| 5 | Verify with test queries | 2 min |
| **Total** | | **~5 min** |

---

## 🚀 You're All Set!

After completing these steps:
- ✅ Supabase is configured for soft delete
- ✅ New columns and views in place
- ✅ Audit trail ready
- ✅ Auto-cleanup functions available
- ✅ App code already updated
- ✅ Ready to deploy!

**Next: Deploy your app** 🚀

---

For any issues, check the Troubleshooting section above or review debug logs in your app.

Good luck! 🎉
