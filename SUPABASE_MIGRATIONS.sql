-- ===================================================================
-- SUPABASE MIGRATIONS - POI Soft Delete Implementation
-- ===================================================================
-- Purpose: Add soft delete support and sync tracking to POI table
-- Instructions: Copy and paste these SQL commands into Supabase SQL Editor
-- ===================================================================

-- ===================================================================
-- STEP 1: Add Soft Delete Columns to POI Table
-- ===================================================================
ALTER TABLE poi ADD COLUMN is_deleted BOOLEAN DEFAULT false;
ALTER TABLE poi ADD COLUMN deleted_at TIMESTAMP;
ALTER TABLE poi ADD COLUMN deleted_by TEXT;

-- Create index for efficient filtering of active POIs
CREATE INDEX idx_poi_is_deleted ON poi(is_deleted);
CREATE INDEX idx_poi_deleted_at ON poi(deleted_at);

-- ===================================================================
-- STEP 2: Create View for Active POIs (optional but recommended)
-- ===================================================================
-- This makes it easier to query only active POIs
CREATE OR REPLACE VIEW active_pois AS
SELECT * FROM poi
WHERE is_deleted = false
ORDER BY id;

-- ===================================================================
-- STEP 3: Create Audit Log Table (optional but recommended)
-- ===================================================================
-- For tracking who deleted what and when
CREATE TABLE IF NOT EXISTS poi_audit_log (
    id BIGSERIAL PRIMARY KEY,
    poi_id INTEGER NOT NULL REFERENCES poi(id) ON DELETE CASCADE,
    action TEXT NOT NULL, -- 'DELETE', 'RESTORE', 'CREATE', 'UPDATE'
    deleted_by TEXT,
    deleted_at TIMESTAMP DEFAULT NOW(),
    restored_at TIMESTAMP,
    reason TEXT,
    created_at TIMESTAMP DEFAULT NOW()
);

-- Create index for audit log
CREATE INDEX idx_poi_audit_poi_id ON poi_audit_log(poi_id);
CREATE INDEX idx_poi_audit_created_at ON poi_audit_log(created_at);

-- ===================================================================
-- STEP 4: Update RLS Policies (if using RLS)
-- ===================================================================
-- Add policy to filter out deleted POIs for SELECT
-- (Adjust table/role names according to your RLS setup)

-- Example for anon role (unauthenticated users)
CREATE POLICY "anon_select_active_pois" ON poi
FOR SELECT
USING (is_deleted = false);

-- Example for authenticated users
CREATE POLICY "authenticated_select_all_pois" ON poi
FOR SELECT
USING (true); -- or customize based on your needs

-- ===================================================================
-- STEP 5: Soft Delete Trigger (optional but useful)
-- ===================================================================
-- Automatically log deletions to audit table
CREATE OR REPLACE FUNCTION log_poi_deletion()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.is_deleted = true AND OLD.is_deleted = false THEN
        INSERT INTO poi_audit_log (poi_id, action, deleted_by, deleted_at)
        VALUES (NEW.id, 'DELETE', NEW.deleted_by, NEW.deleted_at);
    ELSIF NEW.is_deleted = false AND OLD.is_deleted = true THEN
        INSERT INTO poi_audit_log (poi_id, action, restored_at)
        VALUES (NEW.id, 'RESTORE', NOW());
    END IF;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Attach the trigger to the POI table
DROP TRIGGER IF EXISTS poi_deletion_trigger ON poi;
CREATE TRIGGER poi_deletion_trigger
AFTER UPDATE ON poi
FOR EACH ROW
EXECUTE FUNCTION log_poi_deletion();

-- ===================================================================
-- STEP 6: Utility Functions
-- ===================================================================

-- Function to soft delete a POI
CREATE OR REPLACE FUNCTION soft_delete_poi(p_id INTEGER, p_deleted_by TEXT DEFAULT NULL)
RETURNS void AS $$
BEGIN
    UPDATE poi
    SET is_deleted = true,
        deleted_at = NOW(),
        deleted_by = p_deleted_by
    WHERE id = p_id;
END;
$$ LANGUAGE plpgsql;

-- Function to restore a POI
CREATE OR REPLACE FUNCTION restore_poi(p_id INTEGER)
RETURNS void AS $$
BEGIN
    UPDATE poi
    SET is_deleted = false,
        deleted_at = NULL,
        deleted_by = NULL
    WHERE id = p_id;
END;
$$ LANGUAGE plpgsql;

-- Function to hard delete old soft-deleted POIs (cleanup)
CREATE OR REPLACE FUNCTION cleanup_deleted_pois(p_days_old INTEGER DEFAULT 90)
RETURNS INTEGER AS $$
DECLARE
    deleted_count INTEGER;
BEGIN
    DELETE FROM poi
    WHERE is_deleted = true
    AND deleted_at < NOW() - INTERVAL '1 day' * p_days_old
    AND deleted_at IS NOT NULL;
    
    GET DIAGNOSTICS deleted_count = ROW_COUNT;
    RETURN deleted_count;
END;
$$ LANGUAGE plpgsql;

-- ===================================================================
-- STEP 7: Manual SQL Queries for Testing
-- ===================================================================

-- View all active POIs
-- SELECT * FROM active_pois;

-- View all POIs including soft-deleted
-- SELECT * FROM poi ORDER BY id;

-- View soft-deleted POIs only
-- SELECT * FROM poi WHERE is_deleted = true;

-- Soft delete a POI (example: POI with id=5)
-- UPDATE poi SET is_deleted = true, deleted_at = NOW(), deleted_by = 'admin' WHERE id = 5;

-- Restore a POI
-- UPDATE poi SET is_deleted = false, deleted_at = NULL, deleted_by = NULL WHERE id = 5;

-- View audit log
-- SELECT * FROM poi_audit_log ORDER BY created_at DESC;

-- Clean up old deleted POIs (>90 days)
-- SELECT cleanup_deleted_pois(90);

-- ===================================================================
-- NOTES FOR DEVELOPERS
-- ===================================================================
/*

1. POI Table Changes:
   - is_deleted: BOOLEAN DEFAULT false
   - deleted_at: TIMESTAMP (when it was deleted)
   - deleted_by: TEXT (who deleted it, e.g., admin username)

2. New Objects Created:
   - active_pois VIEW: Query active POIs easily
   - poi_audit_log TABLE: Track all deletions/restorations
   - Trigger: Auto-log changes to audit table
   - Functions: Utility functions for delete/restore/cleanup

3. Sync Strategy in App:
   - App fetches all POIs from the poi table
   - POIs with is_deleted = true are considered deleted
   - App soft deletes local copy when server POI is deleted
   - After 90 days, hard delete is performed automatically

4. Query Tips:
   - For user UI: Query active_pois VIEW
   - For admin/audit: Query poi table directly
   - For sync logic: Check is_deleted flag

5. Performance:
   - Indexes on is_deleted and deleted_at for fast filtering
   - Audit log has indexes for quick lookups

6. Safety:
   - RLS policies ensure anon users only see active POIs
   - Soft delete prevents accidental permanent loss
   - Audit trail tracks all changes
   - Cleanup function removes very old deleted records

*/

-- ===================================================================
-- END OF MIGRATIONS
-- ===================================================================
