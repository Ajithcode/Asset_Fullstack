# Asset360 – Asset Criticality Management (Specification)

Source: manager's design note. This file is the single source of truth for the feature.

## Purpose
Identify, assess, classify and monitor assets by the business impact of their failure or unavailability.
Criticality = consequences of failure. It is NOT condition, health score, or maintenance priority (but feeds all three).
Example: a generator for a hospital's critical operations is more critical than the same generator backing a non-essential store.

Placement: dedicated submodule under **Assets**. Configuration under **Administration → Master Data Management → Asset Criticality Configuration**.

## Release scope
**Release 1 (build now):**
1. Centralized landing page (KPIs, filters, table, assessment history, distribution analytics)
2. Configurable criticality levels, criteria and assessment templates
3. Weighted scoring + classification (backend-calculated)
4. Assessment form, approval workflow, version history
5. Integration: Asset List / Registration / Asset 360° View (show approved criticality). Maintenance, Inspection, Lifecycle integration after core is stable.

**Later:** automated reassessment triggers, dependency-based impact analysis, risk heatmaps, advanced reliability analytics.

---

## 1. Landing page (Level 1)
Shows ALL registered assets with their criticality – not a single assessment by default.
Sections: KPI cards · search & filters · asset criticality table · assessment history tab · criticality analytics (distribution).
Header: title "Asset Criticality", subtitle "Assess and prioritize assets based on business impact", button "+ New Assessment".
Desktop: full-width table, configurable columns, right-side assessment panel, export.

### KPI cards
| KPI | Meaning |
|---|---|
| Total Assets | Assets in selected scope |
| Critical | Approved level = Critical |
| High | Approved level = High |
| Medium | Approved level = Medium |
| Low | Approved level = Low |
| Unassessed | No approved assessment |
| Pending Approvals | Assessments awaiting approval |
| Reassessment Overdue | Next review date < today |

### Table columns
Asset ID · Asset Name · Category/Type · Site/Location · Department/Owner · Operational Status · Current Condition · Criticality Level (badge) · Criticality Score (x/100) · Assessment Date · Assessed By · Next Review Date · Approval Status (Draft/Pending/Approved/Rejected) · Actions (View, Assess, Reassess, History)

### Filters
Organization, Site, Department, Asset Category, Asset Type, Criticality Level, Assessment Status, Assessor, Review Due Date, Operational Status. Plus free-text search (asset ID, name, type), sorting, pagination, export.

Tabs: **Asset Summary** (table above) · **Assessment History** (all assessments, newest first).

---

## 2. Scoring
Each criterion rated 1–5 (1 = lowest impact, 5 = highest).
**Score = Σ (rating ÷ 5 × weight)**, weights total 100%. Minimum possible score = 20.

Default criteria (configurable per template/industry):
| Criterion | Weight |
|---|---|
| Safety & Human Impact | 25% |
| Operational / Production Impact | 25% |
| Financial Impact | 15% |
| Environmental Impact | 10% |
| Regulatory / Compliance Impact | 10% |
| Redundancy / Alternative Availability (lack of) | 10% |
| Customer / Service Impact | 5% |

Default classification (configurable):
| Score | Level |
|---|---|
| 85–100 | Critical |
| 65–84 | High |
| 40–64 | Medium |
| 20–39 | Low |

Example: ratings 5,5,4,2,4,5,4 → 88 → Critical.
Mandatory override: policy may force Critical (safety-critical / legally essential) regardless of score – requires justification + authorization.
Backend always recalculates and validates the score; never trust the frontend value.
Do not mix condition or failure likelihood into the baseline criticality score (likelihood × consequence risk = later).

---

## 3. New Assessment form (Level 2)
**A – Asset information:** Assessment ID (auto) · Asset (searchable selector, required) · Category/Type (auto) · Site/Location (auto) · Department/Owner (auto) · Current Criticality (read-only) · Assessment Type (Initial / Reassessment) · Assessment Date · Assessed By (user selector)

**B – Criticality assessment:** Template (dropdown) · one 1–5 rating per template criterion (required) · Calculated Score (read-only, live preview) · Proposed Criticality (read-only, override allowed with justification) · Assessment Justification (required when overriding or lowering criticality)

**C – Risk & supporting info:** Failure Consequences · Alternative Equipment Available (Y/N) · Estimated Downtime Impact (duration) · Estimated Financial Loss (amount) · Safety/Environmental Consequences · Supporting Documents (upload – reuse Media service) · Related Risk Assessment (reference) · Next Reassessment Date · Remarks

**D – Approval:** Approval Required (rule-based) · Approver (user/role) · Approval Status · Approval Comments · Approved By (auto) · Approval Date (auto)

Detailed assessment view: score breakdown per criterion, documents, approval trail, audit history.

---

## 4. Workflow
Draft → Submitted → Pending Approval → Approved / Rejected (rejected can be corrected and resubmitted).
Only an **approved** assessment updates the asset's official criticality (unless policy allows direct publish by designated roles).

Approval matrix (configurable):
| Scenario | Approver |
|---|---|
| Initial Low / Medium | Asset Owner or authorized assessor |
| Initial High | Maintenance Manager |
| Initial Critical | Maintenance Manager + Risk/Operations authority |
| Critical → High or lower | Higher-level approval + justification |
| High → Medium/Low | Maintenance Manager |
| Periodic reassessment, no change | Simplified review |

---

## 5. Configuration masters
Criticality Level Master · Assessment Criteria Master · Assessment Template (criteria + weights per industry) · Scoring Rules (scale, formula, thresholds, mandatory overrides) · Approval Matrix · Reassessment Rules (periodic + event-triggered).

## 6. Data (MongoDB collections – follow existing naming conventions)
| Entity | Responsibility |
|---|---|
| AssetCriticalityLevelMaster | Levels + thresholds |
| AssetCriticalityCriteria | Criteria |
| AssetCriticalityTemplate | Template + weights + scoring version |
| AssetCriticalityAssessment | Assessments (link by existing Asset ID, no asset data duplication) |
| AssetCriticalityAssessmentResponse | Per-criterion rating + justification |
| AssetCriticalityApproval | Approval records |
| AssetCriticalityHistory | Approved classification history |
Indexes, timestamps, audit fields (createdBy/At, updatedBy/At), soft delete where appropriate.

## 7. Validation rules
1. Published assessment must reference a valid registered asset.
2. All mandatory criteria completed.
3. Weighted template weights total 100%.
4. Only authorized users assess/approve.
5. Assessor cannot approve own assessment when approval is mandatory.
6. Overrides need justification + authorization.
7. Approved assessments are immutable; reassessment creates a new version.
8. Template and scoring versions preserved on each assessment.
9. Prevent concurrent approvals overwriting a newer assessment.
10. Reassessment triggered by configured material changes (loss of redundancy, process change, dependency change) – later release.

## 8. Integration (after core)
Asset Registration / Asset List (column, filter, sort) · Asset 360° View (score, level, history) · Status & Condition (shown alongside) · Maintenance (input to priority – not sole driver) · Inspection (frequency, approvals) · Health Score (risk prioritization, kept separate) · Lifecycle (replacement priority) · Relationships · Movement & Custody (extra approval for critical) · WIP/Operations · Audit & Compliance · Reports & Analytics.

## Open questions for the manager
1. Which existing roles map to Assessor, Approver (Maintenance Manager, Risk/Operations authority) and Configurator?
2. Export format: Excel, PDF or both?
3. Mockup shows a Low asset with 18/100 – the model's minimum is 20; confirm Low = 20–39.
4. Which reassessment events matter first (time-based only for Release 1?).
