# 🚀 Soft Delete - Quick Reference

## ⚡ TL;DR (2 Minutes)

### **Setup (One-Time)**
1. Open Supabase SQL Editor
2. Copy-paste all code from `SUPABASE_MIGRATIONS.sql`
3. Execute
4. Done ✅

### **What Changed in App**
- POIs can now be soft-deleted (marked as deleted, not removed)
- App auto-syncs every 12 hours
- Deleted POIs automatically hide from users
- Old soft-deleted records (>90 days) are hard deleted

### **C# Code Usage**
```csharp
// Soft delete a POI locally
await _database.SoftDeletePOIAsync(poiId);

// Restore a POI
await _database.RestorePOIAsync(poiId);

// Get only active (non-deleted) POIs
var activePois = await _placeService.GetAllActivePOIsAsync();

// Manual sync
await _placeService.SyncPOIsFromApiAsync(apiUrl, apiKey);
```

---

## 📂 Files Overview

| File | Purpose |
|------|---------|
| `SUPABASE_MIGRATIONS.sql` | **→ PASTE THIS INTO SUPABASE** SQL Editor |
| `PointOfInterest.cs` | Model with soft delete fields |
| `LocalDatabase.cs` | Soft delete methods & sync tracking |
| `PlaceService.cs` | Sync logic with deletion detection |
| `MainPage.xaml.cs` | Auto-sync & display active POIs |

---

## 🔄 Sync Behavior

| When | What Happens |
|------|--------------|
| **App Starts** | Checks if >12h since last sync |
| **>12h Since Sync** | Triggers auto-sync |
| **During Sync** | Detects deleted POIs, soft deletes locally |
| **After Sync** | Cleanup removes POIs soft-deleted >90 days |
| **Display** | Only active (IsDeleted=false) POIs shown |

---

## 🧪 Test Scenarios

### **Delete on Server**
```
Supabase:
UPDATE poi SET is_deleted = true WHERE id = 5;

App Result:
POI#5 removed from map ✅
```

### **Restore on Server**
```
Supabase:
UPDATE poi SET is_deleted = false WHERE id = 5;

App Result:
POI#5 reappears on map ✅
```

### **Offline**
```
POI deleted on server, but user offline
→ Local copy still shows (safe)
→ After sync, removed ✅
```

---

## 📊 Database Changes

### Added to `poi` table:
- `is_deleted` (boolean) - default false
- `deleted_at` (timestamp) - when deleted
- `deleted_by` (text) - who deleted it

### New Objects:
- `active_pois` view - easy query for active POIs
- `poi_audit_log` table - tracks all deletions
- Functions: `soft_delete_poi()`, `restore_poi()`, `cleanup_deleted_pois()`

---

## 🐛 Debug Output

Look for these in Debug logs:

```
[Sync] Starting POI sync...
[Sync] Soft deleted: POI Name
[Sync] Restored: POI Name  
[Sync] Complete. Total active: 15
[Cleanup] Hard deleted: Old POI Name
```

---

## ⚙️ Configuration

### Change auto-sync interval:
```csharp
// MainPage.OnAppearing()
if (hoursSinceSync > 12)  // Change 12 to different hours
```

### Change cleanup threshold:
```csharp
// MainPage.OnAppearing()
await _placeService.CleanupSoftDeletedPOIsAsync(daysOld: 90);
// Change 90 to keep longer/shorter
```

---

## ✅ Verification Checklist

- [ ] SQL executed in Supabase
- [ ] `poi` table has `is_deleted` column
- [ ] App builds without errors
- [ ] Debug logs show sync happening
- [ ] Deleted POI removed from map
- [ ] Restored POI reappears

---

## 📞 Common Questions

**Q: What if I delete a POI by mistake?**
A: It's soft-deleted for 90 days. Go to Supabase and restore it:
```sql
UPDATE poi SET is_deleted = false WHERE id = 5;
```

**Q: Can user see soft-deleted POIs?**
A: No. App shows only `IsDeleted = false` POIs.

**Q: How often does sync happen?**
A: Auto-sync every 12 hours, or on app restart.

**Q: What if app crashes during sync?**
A: No problem - data is consistent. Sync will retry on next run.

**Q: Do I need to manually manage deletions?**
A: No. Everything is automatic. Sync detects changes automatically.

**Q: How much storage for old deleted POIs?**
A: After 90 days, auto-cleaned. No storage bloat.

---

## 🎯 You're All Set! ✅

SQL: Execute once in Supabase ✅
Code: Already updated ✅
App: Automatic sync & cleanup ✅

Done! 🚀
