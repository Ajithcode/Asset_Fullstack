# Asset Criticality – Implementation Plan (Release 1)

Spec: `docs/asset-criticality-spec.md`. Status: **planning only, no code changed**. Dev stack only
(`/opt/asset360-dev`, DB `Asset360Dev`). Branches: `feature/asset-criticality` in `backend` and `frontend`
repos (never push to `main` / `backend`).

---

## 0. What already exists (inspected 2026-11-17)

| Area | Exists today | Gap vs spec |
|---|---|---|
| Gateway | `asset-criticality-root` + `asset-criticality` routes → `asset-cluster` (`ApiGateway/A360.ApiGateway/appsettings.json:237-249`); `/api/master-data/{**catch-all}` → MDM (:1061) | None – all new Asset routes go under `/api/asset-criticality/**`, so **no gateway change needed** |
| Asset service API | `Endpoints/AssetCriticalityEndpoints.cs`: `GET /` (current per asset, paged), `/options`, `/current`, `/history`, `/{id}`, `POST /`, `PUT /{id}`; event log via `IEventLogger` | No status/approval, no KPIs, no export, list only shows *assessed* assets, no authorization |
| Entity | `AssetCriticalityAssessment` (collection `AssetCriticalityAssessment`, indexes on asset id+date and is_current) with embedded `criteria[]` | Copies asset name/tag/category/location (spec: no duplication); no status, template/scoring version, override, risk section, attachments, approval trail, assessment type |
| Scoring | Server-side: `Σ weight × score / 5`, levels from hard-coded `AssetCriticalityLevels` (80/60/40) and `AssetCriticalityCriteria` defaults (6 criteria) | **Client sends weights** (must come from template); ratings allow 0.5 steps (spec: integers 1–5); thresholds 85/65/40 in spec; criteria list differs; likelihood/consequence matrix mixed in (spec: keep out of baseline) |
| MDM | `Criticality` master, collection `master_criticalities` (name, code, color, risk level, maintenance priority, default) at `/api/master-data/criticalities`, seeded in `Seed/MasterDataSeedData.cs:340`; FE page `projects/master-data-management/src/app/asset/criticality/` (real API) | No score thresholds/rank; no criteria, templates, scoring rules, approval matrix, reassessment rules |
| Asset field | Two fields: `basic_information.criticality` and `classification.criticality`. Readers disagree: ReportFieldCatalog + MobileDashboard read `basic_information`; Asset dashboard reads `classification`; ReportAnalytics dashboard reads both | No link to approved assessment; set manually on registration |
| Frontend page | `projects/assets/src/app/asset-criticality/` (~2.2k lines, real APIs), route `/assets/criticality?tag=`, sidebar `sidebar.ts:120`. It is a **single-asset view** (Asset 360 tab: overview, score breakdown, risk matrix, details/impact/mitigation/history/related sub-tabs, re-assess modal) | Spec's landing page (all assets, KPIs, filters, table, history tab) does not exist |
| Asset List / 360 | Asset List already has criticality filter + sortable column + card pill; Asset 360 tab bar (`assetTabs()`) already includes "Criticality"; history view links to it | Shows the manual asset field, not the approved assessment |
| Approval pattern | Best model: **Asset Disposal** (`AssetDisposalRequest`: `Approvers[]` with level/approver/status/comments/actedBy/actedAt; endpoints `/{id}/submit|approve|reject|cancel`); FE `assets/asset-disposal` | No shared approval library/component (a `Services/Workflows` service and admin workflow-builder exist but are not wired to Asset) |
| Roles / auth | Roles seeded: Administrator, Manager, User. `Role.AssignedPermissions {featureName, viewOption, editOption}`; **enforced only in the FE** (`AccessService.canView/can`, `moduleGuard`, sidebar). Backend endpoints use no `RequireAuthorization` | Spec rule 4/5 (only authorized users assess/approve; no self-approval) needs server-side checks |
| Media | `POST /api/media/upload` (multipart `file` + `category`) returns URL; Asset uses `Services/MediaUploadClient.cs`; attachments stored inline (`FileName, FileUrl, FileType, FileSize`) | Reuse as-is |
| Audit | `IEventLogger.LogAsync / LogDetailedAsync(details)` → `EventLog` (no structured old/new values); FE "View History" reads `/api/eventlogs?entityType=` | Reuse; put transitions/diffs in `details` |
| Paging / KPI / export | Per-module paged responses (`items,totalCount,page,pageSize`); KPI example `GET /kpis` in `AssetDashboardEndpoints.cs:41`; ClosedXML 0.104.2 in Asset.Api (e.g. `GET /export` in `AssetFieldGroupEndpoints.cs`); FE has `xlsx`; **no PDF library** | Reuse ClosedXML for Excel; PDF pending open question |
| Concurrency | `BaseEntity` has no version field | Needed for rule 9 |
| Stray copy | `frontend/backend/**` (2,864 tracked files) is a stale copy of the backend; 7 of 12 criticality files differ | **Never edit it**; only `/opt/asset360-dev/backend` |

Not verified: MongoDB contents (Mongo is not in the dev compose project; I did not touch credentials). Before P1, count
`AssetCriticalityAssessment` docs and `master_criticalities` rows with a read-only query approved by the user.

---

## 1. Design decisions (reuse, no duplicates)

1. **Keep `/api/asset-criticality` in the Asset service** and extend it; scoring, assessments, approvals and
   configuration live there (scoring needs the config in-process, and the Asset service never calls MDM over HTTP).
2. **Levels = existing MDM `Criticality` master** (`master_criticalities`), extended with thresholds. No new
   `AssetCriticalityLevelMaster` collection. The Asset service reads `master_criticalities` read-only through a small
   repository (same `Asset360Dev` database) and snapshots the thresholds into each assessment.
   *Alternative if cross-service reads are not wanted:* the Asset service calls `GET /api/master-data/criticalities`
   through the gateway (one more HttpClient, like `MediaUploadClient`). → **Decision needed.**
3. **Configuration UI** lives in the MDM frontend project (spec placement: Administration → MDM → Asset Criticality
   Configuration) but calls `/api/asset-criticality/config/*` for criteria/templates/rules/matrix.
4. **Criterion responses embedded** in the assessment (`criteria[]`), not a separate
   `AssetCriticalityAssessmentResponse` collection. They are always read with the assessment and are immutable once
   approved. (Deviation from the spec's entity list; same data.)
5. **Approval steps stored in their own collection** `AssetCriticalityApproval` (spec), modelled on
   `AssetDisposalApprover`. The assessment holds a summary (`approval_status`, `current_step`).
6. **Official criticality = latest Approved assessment.** On approval the service also writes the level into
   **both** asset fields (`basic_information.criticality` and `classification.criticality`), so the Asset List,
   dashboards, reports and mobile keep working without changes. The Registration form shows the field read-only
   once an approved assessment exists.
7. **No asset data duplication:** the landing list is driven by `AssetRegistration` (all assets, including
   unassessed) joined to the current approved assessment and to the latest open draft or pending assessment
   (`$lookup` in the Asset service). The existing snapshot fields (`asset_name`, `asset_tag`, …) are kept for
   existing data but no longer used for filtering.
8. **Likelihood / consequence** stay as optional fields for the existing risk-matrix panel and are **excluded from the
   score** (spec §2).
9. **Server-side authorization** added in the Asset service: a small `CriticalityAccess` helper reading the current
   user's role or permissions (from `ICurrentUserAccessor` / JWT claims; confirm which claims carry the role). It is
   applied to assess, approve and configure endpoints. Role mapping is **open question 1**; proposed default:
   Configurator = Administrator; Assessor = Manager, Administrator; Approver = per approval matrix (role names).

---

## 2. Collections, fields, indexes

All entities extend `BaseEntity` (`_id, client_id, tenant_id, created_by/at, updated_by/at, status, is_deleted`).
Collection names follow the Asset service convention (PascalCase).

### 2.1 `master_criticalities` (MDM, existing – extend)
New fields: `min_score` (decimal), `max_score` (decimal), `rank` (int, 1 = Low … 4 = Critical),
`requires_override_authorization` (bool). Seed defaults: Critical 85–100, High 65–84, Medium 40–64, Low 20–39.
Validation: ranges must not overlap or leave gaps between 20 and 100 within a client.
Index (existing + new): `{client_id, code}` unique; `{client_id, rank}`.

### 2.2 `AssetCriticalityCriteria` (new)
`code` (unique per client), `name`, `description`, `rating_guidance` (`[{rating 1..5, label, description}]`),
`category`, `is_active`, `sort_order`.
Indexes: `{client_id, code}` unique; `{client_id, is_active, sort_order}`.

### 2.3 `AssetCriticalityTemplate` (new; includes the scoring rules)
`code`, `name`, `industry`, `description`, `is_default`, `applies_to` (`{categories[], asset_types[]}`),
`criteria[]` (`{criterion_id, code, name, weight, is_mandatory}`; Σ weight = 100),
`scoring` (`{scale_min: 1, scale_max: 5, formula: "weighted_sum_v1", min_score: 20}`),
`mandatory_overrides[]` (`{condition: "SafetyCritical" | "LegallyEssential", forced_level: "Critical"}`),
`version` (int), `state` (`Draft` | `Published` | `Retired`).
Editing a published template creates a new version (copy with version + 1), so old assessments keep pointing to the
version they used.
Indexes: `{client_id, code, version}` unique; `{client_id, state, is_default}`.

### 2.4 `AssetCriticalityApprovalRule` (new; the approval matrix)
`scenario` (`InitialLowMedium | InitialHigh | InitialCritical | DowngradeFromCritical | DowngradeFromHigh |
UpgradeAny | ReassessNoChange`), `steps[]` (`{level, approver_roles[], approval_type: Any|All}`),
`requires_justification`, `allow_direct_publish_roles[]`, `is_active`.
Seeded from spec §4. Index: `{client_id, scenario}` unique.

### 2.5 `AssetCriticalityReassessmentRule` (new; Release 1 is time-based only)
`level_code`, `review_interval_months` (e.g. Critical 6, High 12, Medium 24, Low 36), `is_active`.
`event_triggers[]` reserved for later. Index: `{client_id, level_code}` unique.

### 2.6 `AssetCriticalityAssessment` (existing – extend)
Added: `assessment_number` (auto, e.g. `ACA-2026-000123`), `assessment_type` (`Initial` | `Reassessment`),
`approval_status` (`Draft | Submitted | PendingApproval | Approved | Rejected | Cancelled`),
`template_id`, `template_code`, `template_version`, `scoring_version`, `level_thresholds_snapshot[]`,
`calculated_score`, `calculated_level`, `final_level` (after override), `is_override`, `override_reason`,
`override_authorized_by`, `justification`, `previous_approved_assessment_id`, `previous_level`,
`risk` (`{failure_consequences, alternative_equipment_available, estimated_downtime_hours,
estimated_financial_loss, currency, safety_environmental_consequences, related_risk_assessment_ref}`),
`attachments[]` (`{file_name, file_url, file_type, file_size, uploaded_by, uploaded_at}`),
`assessed_by_user_id`, `approval` (`{scenario, current_step, approver_roles[], approved_by, approved_at,
rejected_reason}`), `submitted_by/at`, `concurrency_version` (int).
Changed: `criteria[]` items gain `criterion_id`, `code`, `is_mandatory`; `score` → `rating` (integer 1–5);
weight comes from the template. `is_current` now means *current approved*.
Kept for legacy: `likelihood`, `consequence`, asset snapshot fields (no longer used for filtering).
Indexes (new): `{client_id, asset_registration_id, approval_status}`; `{client_id, is_current, final_level}`;
`{client_id, approval_status, created_at}`; `{client_id, next_review_date}`; `{assessment_number}` unique;
partial unique `{asset_registration_id}` where `approval_status ∈ {Draft, Submitted, PendingApproval}`
(at most one open assessment per asset).

### 2.7 `AssetCriticalityApproval` (new)
`assessment_id`, `asset_registration_id`, `step`, `approver_roles[]`, `approval_type`, `status`
(`Pending | Approved | Rejected | Skipped`), `acted_by`, `acted_by_user_id`, `acted_at`, `comments`.
Indexes: `{assessment_id, step}`; `{status, approver_roles}` (approver inbox).

### 2.8 `AssetCriticalityHistory` (new; approved classification history)
`asset_registration_id`, `assessment_id`, `from_level`, `to_level`, `score`, `effective_from`, `effective_to`,
`approved_by`, `change_reason`.
Indexes: `{asset_registration_id, effective_from: -1}`.

### 2.9 Migration of existing data (one-off, idempotent hosted service in the Asset service)
Existing assessments → `approval_status = Approved`, `assessment_type` Initial/Reassessment by order,
`template_code = "LEGACY"`, ratings kept (0.5 values allowed for legacy only), one `AssetCriticalityHistory` row
per approved change. Seed the default template (7 spec criteria) and the approval/reassessment rules.

---

## 3. API (Asset service unless noted; all via gateway, no gateway change)

Roles: **V** = any role with "Asset Criticality" view permission, **A** = assessor, **P** = approver per matrix,
**C** = configurator (Administrator). Lists use `page, pageSize, sortBy, sortDir` and return
`{items, totalCount, page, pageSize}`.

### P1 – landing
| Method | Route | Request | Response | Roles |
|---|---|---|---|---|
| GET | `/api/asset-criticality/assets` | filters: `search, organization, site, location, department, category, assetType, level, approvalStatus, assessor, reviewDueFrom/To, overdue, operationalStatus` + paging/sort | rows: asset id/name/category/type/site/location/department/owner/operational status/condition + current approved level/score/date/assessor/next review + open assessment status/id | V |
| GET | `/api/asset-criticality/kpis` | same filters | `{total, critical, high, medium, low, unassessed, pendingApprovals, reassessmentOverdue}` | V |
| GET | `/api/asset-criticality/distribution` | same filters | counts per level and per category × level (analytics panel) | V |
| GET | `/api/asset-criticality/assessments` | `search, assetRegistrationId, approvalStatus, assessor, from, to` + paging | Assessment History tab, newest first | V |
| GET | `/api/asset-criticality/export` | landing filters + `columns[]`, `format=xlsx` | file (ClosedXML; PDF later, open question 2) | V |
| GET | `/api/asset-criticality/filter-options` | – | distinct sites, departments, categories, types, assessors, levels (from MDM) | V |
| GET | `/api/asset-criticality` (existing) | unchanged | kept so the current FE keeps working until P3 | V |

### P2 – configuration
| Method | Route | Roles |
|---|---|---|
| GET/PUT | `/api/master-data/criticalities[/{id}]` (MDM, existing, add threshold fields and validation) | V / C |
| GET/POST/PUT/DELETE (soft) | `/api/asset-criticality/config/criteria[/{id}]` | V / C |
| GET/POST/PUT | `/api/asset-criticality/config/templates[/{id}]`; `POST /{id}/publish`, `POST /{id}/new-version`, `POST /{id}/retire` | V / C |
| GET | `/api/asset-criticality/config/templates/resolve?assetRegistrationId=` (template that applies to an asset) | A |
| GET/PUT | `/api/asset-criticality/config/approval-rules[/{scenario}]` | V / C |
| GET/PUT | `/api/asset-criticality/config/reassessment-rules[/{levelCode}]` | V / C |

Validation: weights of a template total exactly 100; at least one criterion; criteria active; level thresholds
contiguous 20–100; only Draft templates are editable.

### P3 – assessment and scoring
| Method | Route | Request / response | Roles |
|---|---|---|---|
| POST | `/api/asset-criticality/score-preview` | `{templateId, ratings[{criterionCode, rating}]}` → `{score, level, breakdown[]}` (stateless; for the live preview; same code path as save) | A |
| POST | `/api/asset-criticality/assessments` | creates a Draft: `{assetRegistrationId, templateId, assessmentDate, assessedByUserId, ratings[], override?, justification?, risk{}, attachments[], nextReviewDate?, remarks}` → full assessment | A |
| PUT | `/api/asset-criticality/assessments/{id}` | edit Draft or Rejected only; requires `concurrencyVersion` | A (owner) |
| GET | `/api/asset-criticality/assessments/{id}` | detail: breakdown, documents, approval steps, previous level | V |
| DELETE | `/api/asset-criticality/assessments/{id}` | cancel a Draft | A (owner) |
| POST | `/api/asset-criticality/assessments/{id}/attachments` | multipart → proxied to `POST /api/media/upload` (category `asset-criticality`) through `MediaUploadClient` | A |
| GET | `/api/asset-criticality/assets/{assetRegistrationId}/current` | current approved assessment + open one | V |

The server always recalculates: it loads the template version and ignores client weights or scores; ratings must be
integers 1–5 for every mandatory criterion; score = Σ(rating ÷ 5 × weight), rounded to 1 decimal; level from the
threshold snapshot; an override needs a justification and sets `requires_override_authorization`; a justification is
required when the proposed level is lower than the previous approved level.

### P4 – workflow
| Method | Route | Notes | Roles |
|---|---|---|---|
| POST | `/assessments/{id}/submit` | Draft/Rejected → PendingApproval; resolves the scenario from the matrix; creates `AssetCriticalityApproval` steps; direct publish if the user's role is in `allow_direct_publish_roles` | A |
| POST | `/assessments/{id}/approve` | `{comments, concurrencyVersion}`; approver ≠ assessor; role ∈ step roles; when the last step is done → Approved, publish | P |
| POST | `/assessments/{id}/reject` | `{comments}` required → Rejected (editable, then resubmit) | P |
| GET | `/api/asset-criticality/approvals/inbox` | pending steps for the current user's roles | P |
| GET | `/assets/{assetRegistrationId}/history` | approved classification history (`AssetCriticalityHistory`) | V |
| GET | `/api/eventlogs?entityType=AssetCriticalityAssessment&entityId=` (existing) | audit trail | V |

**Publish (one unit):** an atomic `findOneAndUpdate` with the filter `{_id, approval_status: PendingApproval,
concurrency_version}` makes double approvals impossible. It checks that the asset's current approved assessment is still
`previous_approved_assessment_id` (otherwise 409, rule 9). It then clears the old `is_current`, sets the new one, writes
`AssetCriticalityHistory`, updates both asset criticality fields, sets `next_review_date` from the reassessment rule,
and writes an `EventLog` entry (`LogDetailedAsync` with the from → to level, score and approvers).
Approved assessments reject every PUT (rule 7).

### P5 – integration
- No new endpoints. The Asset List and Asset 360 read the approved level from the asset field (kept in sync) and
  `/assets/{id}/current` for score and next review date.
- Optional: add `criticalityScore` and `nextReviewDate` to the asset list response through a lookup, only if the list
  needs them.

---

## 4. Frontend

| Phase | Item | Location |
|---|---|---|
| P1 | **New landing page** `AssetCriticalityLanding`: header + "+ New Assessment", 8 KPI cards, filter bar (`app-filter-select`), configurable-column table (column choice saved in localStorage), pagination, export button, tabs *Asset Summary* / *Assessment History*, distribution panel, right-side panel with a quick view of the selected asset | `projects/assets/src/app/asset-criticality/landing/` |
| P1 | Routes: `criticality` → landing; existing single-asset page moves to `criticality/asset/:assetRegistrationId`; keep `criticality?tag=` working by redirecting to the new path | `projects/assets/src/app/app.routes.ts` |
| P1 | Service: split `asset-criticality.service.ts` into `criticality-api.service.ts` (new endpoints) and keep the existing calls | same folder |
| P2 | **Asset Criticality Configuration** page with tabs: Levels (extend existing criticality page with min/max score and rank) · Criteria · Templates (weights editor with a live 100% total, publish/version) · Approval Matrix · Reassessment Rules | `projects/master-data-management/src/app/asset/criticality-config/` + route + `projects/shared-ui/src/lib/master-data/master-data-menu.ts` |
| P3 | **Assessment form** (sections A–D from spec §3): asset lookup (reuse `asset-lookup.service.ts`), template dropdown, 1–5 rating per criterion with guidance, live score through `/score-preview`, override + justification, risk section, upload (Media), save draft / submit | `asset-criticality/assessment-form/`, routes `criticality/new`, `criticality/assessments/:id/edit` |
| P3 | **Assessment detail view**: breakdown table, documents, approval trail, audit (event log) | `asset-criticality/assessment-detail/`, route `criticality/assessments/:id` |
| P3 | Existing single-asset page: the "Re-assess" modal is replaced by a link to the form; the score breakdown reads the new criteria shape | `asset-criticality.ts/.html` |
| P4 | Approve / Reject bar + comments on the detail view; "Pending my approval" filter on the landing page; status badges. Approval timeline component written locally (none shared; copy the disposal page pattern) | `assessment-detail/`, `landing/` |
| P5 | Asset List: column and filter already exist; switch the label to the approved level, add an "Unassessed" filter option and an optional score column. Asset 360: the Criticality tab points to the new per-asset route. Registration: criticality field read-only when an approved assessment exists | `asset-list/`, `asset-detail-tab/`, `asset-details/`, `asset-registration/` |
| all | Sidebar: keep the single "Asset Criticality" entry (`sidebar.ts:120`) → landing. Access via `AccessService.can('Asset Criticality', …)` to hide assess, approve and config actions | `src/app/components/shared/sidebar/sidebar.ts` (only if the label changes) |

Status colours: reuse `criticalityPill()` from `projects/shared-ui/src/lib/asset-data/asset-data.ts` and the MDM
level colour.

---

## 5. Phases and acceptance

| Phase | Backend | Frontend | Done when |
|---|---|---|---|
| **P1 Landing** | `/assets`, `/kpis`, `/distribution`, `/assessments`, `/export`, `/filter-options`; indexes; legacy migration (existing assessments → Approved) | Landing page, tabs, filters, export | All registered assets are listed (including unassessed); KPI numbers match the table filters; xlsx export opens |
| **P2 Config** | MDM threshold fields + seed; criteria/template/approval/reassessment endpoints + seed of the spec defaults | Configuration page in MDM | A template cannot be published unless the weights total 100; thresholds are contiguous |
| **P3 Assessment** | Draft CRUD, score-preview, server scoring from the template version, attachments, override rules | Form + detail view | Ratings 5,5,4,2,4,5,4 on the default template → **88 / Critical** from the server; tampered weights are ignored |
| **P4 Workflow** | submit/approve/reject, matrix resolution, self-approval block, publish transaction, history, audit, 409 on a stale approval | Approve/reject UI, inbox filter, timeline | Only an approved assessment changes the asset's level; a second concurrent approve gets 409; every step is in the event log |
| **P5 Integration** | Sync both asset fields on publish (+ one-time backfill) | Asset List, Asset 360, Registration | The Asset List, dashboard and reports show the approved level |

Each phase: build with `/opt/asset360-dev/stack/rebuild.sh asset` (+ `masterdatamanagement` for P2; `fe-assets`,
`fe-master-data-management` for the frontend; check the exact service names in compose) and test on
http://127.0.0.1:18080 / a360demo.

---

## 6. Conflict risk (files other developers are actively changing)

**High, keep edits minimal and append-only:**
- `backend/Services/Asset/Api/A360.Asset.Api/Middlewares/AssetApplicationBuilderExtensions.cs` (7 commits / 14 days) – one `Map…Endpoints()` line per new group
- `backend/Services/Asset/Api/A360.Asset.Api/IoC/AssetServiceCollectionExtensions.cs` (7) – repository registrations
- `backend/ApiGateway/A360.ApiGateway/appsettings.json` (6, ~4 authors) – **not needed** by this plan
- `backend/Services/MasterDataManagement/.../IoC`, `Middlewares`, `Seed/MasterDataSeedData.cs` (3–4)
- `frontend/src/app/components/shared/sidebar/sidebar.ts` (8) – avoid unless the menu label changes
- `frontend/projects/assets/src/app/app.routes.ts` (2 on 11-16) – route additions
- `frontend/projects/shared-ui/src/lib/master-data/master-data-menu.ts` – one menu entry
- `frontend/src/environments/environment*.ts` (8) – **no change needed**

**Medium:** `asset-registration/` (6), `asset-list/`, `asset-detail-tab/`, `AssetRegistrationRepository.cs` (3),
`Asset Dashboard` repo/endpoints (5; P5 only).
**Don't touch:** `frontend/backend/**` (stale tracked copy of the backend; flag it to the team for removal).
Mitigation: rebase the feature branches on `origin/backend` and `origin/main` before each phase; do P5 last.

---

## 7. Open questions / decisions for the manager or user

1. Role mapping (spec Q1): only Administrator, Manager and User exist. Create roles such as "Maintenance Manager"
   and "Risk Authority", or map them to Manager?
2. Export format (spec Q2): xlsx in P1; PDF needs a new library (QuestPDF) – yes/no?
3. Low = 20–39 (spec Q3), confirmed by the formula; existing legacy scores below 20 stay as they are.
4. Reassessment: time-based only in Release 1 (spec Q4), per-level intervals as in §2.5?
5. Levels: Asset service reads `master_criticalities` directly (same DB) **or** over HTTP through the gateway (§1.2)?
6. Server-side authorization: confirm which JWT claim carries the role/permissions (permissions are currently
   enforced only in the frontend).
7. Read-only Mongo access to confirm existing data volumes before the migration (not checked; Mongo is outside the
   dev compose project).
