// Asset Criticality P1: mark assessments saved before the approval workflow as Approved.
// Scores, levels, criteria and is_current stay exactly as stored; new rules apply only to new assessments.
// Idempotent: only documents without approval_status are touched. Dry run unless APPLY is set.
//
//   mongosh "<uri>/<db>" --file scripts/migrate-asset-criticality-legacy.js                  # counts only
//   mongosh "<uri>/<db>" --eval 'var APPLY=true' --file scripts/migrate-asset-criticality-legacy.js
//
// Back up first: mongodump --db <db> --collection AssetCriticalityAssessment --out <dir>
// The API already treats a missing approval_status as Approved, so this script is data hygiene, not a prerequisite.

const coll = db.getCollection('AssetCriticalityAssessment');
const legacy = { approval_status: { $exists: false } };
const apply = typeof APPLY !== 'undefined' && APPLY === true;

const total = coll.countDocuments({});
const toMigrate = coll.countDocuments(legacy);
print(`database: ${db.getName()}`);
print(`assessments: ${total}, without approval_status: ${toMigrate}, mode: ${apply ? 'APPLY' : 'DRY RUN'}`);

if (apply && toMigrate > 0) {
  // Oldest assessment of each asset is its Initial one, later ones are Reassessments.
  const firstIds = coll.aggregate([
    { $sort: { asset_registration_id: 1, assessment_date: 1, created_at: 1 } },
    { $group: { _id: '$asset_registration_id', first: { $first: '$_id' } } }
  ]).toArray().map(g => g.first);

  const now = new Date();
  const common = { approval_status: 'Approved', template_code: 'LEGACY', migrated_at: now };
  const initial = coll.updateMany({ ...legacy, _id: { $in: firstIds } }, { $set: { ...common, assessment_type: 'Initial' } });
  const re = coll.updateMany(legacy, { $set: { ...common, assessment_type: 'Reassessment' } });
  print(`updated: ${initial.modifiedCount} Initial, ${re.modifiedCount} Reassessment`);
  print(`remaining without approval_status: ${coll.countDocuments(legacy)}`);
}
