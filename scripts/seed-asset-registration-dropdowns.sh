#!/usr/bin/env bash
# Seeds sample data for every data-driven dropdown on the Asset Registration screens (local DB by default).
# Values match the registration UI mock-ups (Caterpillar, Machinery & Equipment > Power Generation > Generator, ...).
# Usage: ./scripts/seed-asset-registration-dropdowns.sh
#        MDM_URL=http://localhost:5500 ASSET_URL=http://localhost:5401 ./scripts/seed-asset-registration-dropdowns.sh
# Safe to re-run: each record is looked up by name first and only created when missing.
#
# Not seeded (no master list behind them): Status, Function/Purpose, Tagging Method, Compliance Category,
# Lifecycle Stage, Ownership Type, Currency, Payment Terms, Procurement Method, Warranty Type, Support Coverage,
# all unit pickers, Operating Shift, Utilization, Phase, Fuel Type, IP Rating -> GET /api/asset-registrations/options.
set -euo pipefail

MDM="${MDM_URL:-http://localhost:5500}/api/master-data"
ASSET="${ASSET_URL:-http://localhost:5401}/api"

# ensure <list url> <name field> <name> <json body>  -> prints the record id (existing or newly created)
ensure() {
  local url="$1" field="$2" name="$3" body="$4" id resp code
  id=$(curl -fsS "$url" | python3 -c '
import sys, json
field, name = sys.argv[1], sys.argv[2]
data = json.load(sys.stdin)
items = data if isinstance(data, list) else (data.get("items") or data.get("data") or [])
for d in items:
    if str(d.get(field, "")).strip().lower() == name.lower():
        print(d.get("id") or next(v for k, v in d.items() if k.lower().endswith("id") and isinstance(v, str) and v))
        break' "$field" "$name")
  if [ -n "$id" ]; then echo "  exists  $name" >&2; echo "$id"; return; fi

  resp=$(curl -sS -w '\n%{http_code}' -X POST "$url" -H 'Content-Type: application/json' -d "$body")
  code="${resp##*$'\n'}"; resp="${resp%$'\n'*}"
  if [[ "$code" != 2* ]]; then echo "POST $url ($name) failed [$code]: $resp" >&2; exit 1; fi
  echo "  added   $name" >&2
  echo "$resp" | python3 -c '
import sys, json
d = json.load(sys.stdin)
print(d.get("id") or next(v for k, v in d.items() if k.lower().endswith("id") and isinstance(v, str) and v))'
}

ref() { printf '{"id":"%s","name":"%s"}' "$1" "$2"; }

echo "== Step 1: Basic Information"
ensure "$MDM/manufacturers" manufacturerName "Caterpillar" \
  '{"manufacturerName":"Caterpillar","code":"MFR-CAT","country":"United States","website":"https://www.cat.com"}' >/dev/null
ensure "$MDM/manufacturers" manufacturerName "Cummins" \
  '{"manufacturerName":"Cummins","code":"MFR-CUM","country":"United States","website":"https://www.cummins.com"}' >/dev/null
MFR_ID=$(ensure "$MDM/manufacturers" manufacturerName "Caterpillar" '{}')

ensure "$MDM/conditions" conditionName "Good" \
  '{"conditionName":"Good","code":"COND-GOOD","type":"Operational","color":"#22C55E","defaultCondition":true,"healthScore":90}' >/dev/null
ensure "$MDM/conditions" conditionName "Fair" \
  '{"conditionName":"Fair","code":"COND-FAIR","type":"Operational","color":"#F59E0B","healthScore":60}' >/dev/null

ensure "$MDM/criticalities" criticalityName "Critical" \
  '{"criticalityName":"Critical","code":"CRIT-CRIT","riskLevel":"Critical","maintenancePriority":"P1","color":"#EF4444","description":"Failure stops operations"}' >/dev/null
ensure "$MDM/criticalities" criticalityName "High" \
  '{"criticalityName":"High","code":"CRIT-HIGH","riskLevel":"High","maintenancePriority":"P2","color":"#F97316"}' >/dev/null

echo "== Step 2: Classification (Asset service categories / types / models)"
CAT_ID=$(ensure "$ASSET/asset-categories" name "Machinery & Equipment" \
  '{"name":"Machinery & Equipment","code":"CAT-MACH","description":"Plant machinery and equipment"}')
ensure "$ASSET/asset-categories" name "IT Equipment" \
  '{"name":"IT Equipment","code":"CAT-IT","description":"Computers, servers and network devices"}' >/dev/null
SUB_ID=$(ensure "$ASSET/asset-categories" name "Power Generation" \
  "{\"parentCategoryId\":\"$CAT_ID\",\"name\":\"Power Generation\",\"code\":\"CAT-MACH-PWR\"}")
ensure "$ASSET/asset-categories" name "HVAC Systems" \
  "{\"parentCategoryId\":\"$CAT_ID\",\"name\":\"HVAC Systems\",\"code\":\"CAT-MACH-HVAC\"}" >/dev/null

TYPE_ID=$(ensure "$ASSET/asset-category-types" typeName "Generator" \
  "{\"categoryId\":\"$SUB_ID\",\"typeName\":\"Generator\",\"typeCode\":\"AT-GEN\"}")
ensure "$ASSET/asset-category-types" typeName "Transformer" \
  "{\"categoryId\":\"$SUB_ID\",\"typeName\":\"Transformer\",\"typeCode\":\"AT-TRF\"}" >/dev/null

ensure "$ASSET/asset-models" modelName "C250D6E" \
  "{\"assetTypeId\":\"$TYPE_ID\",\"modelName\":\"C250D6E\",\"modelCode\":\"MDL-C250D6E\",\"manufacturerId\":\"$MFR_ID\",\"manufacturerName\":\"Caterpillar\"}" >/dev/null
ensure "$ASSET/asset-models" modelName "C15 Generator Set" \
  "{\"assetTypeId\":\"$TYPE_ID\",\"modelName\":\"C15 Generator Set\",\"modelCode\":\"MDL-C15\",\"manufacturerId\":\"$MFR_ID\",\"manufacturerName\":\"Caterpillar\"}" >/dev/null

echo "== Step 2: Classification (Business Unit / Department)"
BU_ID=$(ensure "$MDM/business-units" businessUnitName "Head Office" '{"businessUnitName":"Head Office","code":"BU-HO"}')
ensure "$MDM/business-units" businessUnitName "Operations" '{"businessUnitName":"Operations","code":"BU-OPS"}' >/dev/null
DEP_ID=$(ensure "$MDM/departments" departmentName "Facilities Management" \
  "{\"departmentName\":\"Facilities Management\",\"code\":\"DEP-FM\",\"businessUnit\":$(ref "$BU_ID" "Head Office")}")
ensure "$MDM/departments" departmentName "Engineering" \
  "{\"departmentName\":\"Engineering\",\"code\":\"DEP-ENG\",\"businessUnit\":$(ref "$BU_ID" "Head Office")}" >/dev/null
DEP_REF=$(ref "$DEP_ID" "Facilities Management")

echo "== Step 3: Location & Assignment"
SITE_ID=$(ensure "$MDM/sites" siteName "Oman" \
  "{\"siteName\":\"Oman\",\"code\":\"SITE-OMN\",\"siteType\":\"Plant\",\"city\":\"Muscat\",\"country\":\"UAE\",\"businessUnit\":$(ref "$BU_ID" "Head Office")}")
ensure "$MDM/sites" siteName "Dubai HQ" \
  "{\"siteName\":\"Dubai HQ\",\"code\":\"SITE-DXB\",\"siteType\":\"Office\",\"city\":\"Dubai\",\"country\":\"UAE\",\"businessUnit\":$(ref "$BU_ID" "Head Office")}" >/dev/null
SITE_REF=$(ref "$SITE_ID" "Oman")

BLD_ID=$(ensure "$MDM/buildings" name "Street One" \
  "{\"name\":\"Street One\",\"code\":\"BLD-ST1\",\"site\":$SITE_REF,\"capacity\":300,\"area\":8000}")
ensure "$MDM/buildings" name "Outdoor Parking" \
  "{\"name\":\"Outdoor Parking\",\"code\":\"BLD-PRK\",\"site\":$SITE_REF,\"area\":5000}" >/dev/null

FLR_ID=$(ensure "$MDM/floors" name "Third Right" \
  "{\"name\":\"Third Right\",\"code\":\"FLR-3R\",\"site\":$SITE_REF,\"buildingId\":\"$BLD_ID\",\"area\":2500}")
ensure "$MDM/floors" name "Ground Floor" \
  "{\"name\":\"Ground Floor\",\"code\":\"FLR-G\",\"site\":$SITE_REF,\"buildingId\":\"$BLD_ID\",\"area\":2500}" >/dev/null

ensure "$MDM/zones" name "Second Colony" \
  "{\"name\":\"Second Colony\",\"code\":\"ZONE-SC\",\"site\":$SITE_REF,\"buildingId\":\"$BLD_ID\",\"floorId\":\"$FLR_ID\",\"area\":400}" >/dev/null
ensure "$MDM/zones" name "Plant Room" \
  "{\"name\":\"Plant Room\",\"code\":\"ZONE-PR\",\"site\":$SITE_REF,\"buildingId\":\"$BLD_ID\",\"floorId\":\"$FLR_ID\",\"area\":200}" >/dev/null

ensure "$MDM/locations" locationName "Generator Room" \
  '{"locationName":"Generator Room","code":"LOC-GEN","type":"Room","building":"Street One","floor":"Third Right","zone":"Second Colony"}' >/dev/null
ensure "$MDM/locations" locationName "Electrical Room" \
  '{"locationName":"Electrical Room","code":"LOC-ELE","type":"Room","building":"Street One","floor":"Third Right","zone":"Second Colony"}' >/dev/null

ensure "$MDM/employees" employeeName "Ahmed Basim" \
  "{\"employeeName\":\"Ahmed Basim\",\"employeeId\":\"EMP-101\",\"designation\":\"Facility Manager\",\"email\":\"ahmed.basim@example.com\",\"phoneNumber\":\"+971 50 000 0101\",\"department\":$DEP_REF}" >/dev/null
ensure "$MDM/employees" employeeName "John Mathew" \
  "{\"employeeName\":\"John Mathew\",\"employeeId\":\"EMP-102\",\"designation\":\"Facility Engineer\",\"email\":\"john.mathew@example.com\",\"phoneNumber\":\"+971 50 000 0102\",\"department\":$DEP_REF}" >/dev/null

ensure "$MDM/teams" teamName "MEP Team" \
  "{\"teamName\":\"MEP Team\",\"code\":\"TEAM-MEP\",\"department\":$DEP_REF,\"description\":\"Mechanical, electrical and plumbing\"}" >/dev/null
ensure "$MDM/teams" teamName "HVAC Team" \
  "{\"teamName\":\"HVAC Team\",\"code\":\"TEAM-HVAC\",\"department\":$DEP_REF,\"description\":\"Heating, ventilation and cooling\"}" >/dev/null

echo "== Step 4: Purchase & Warranty"
ensure "$MDM/suppliers" supplierName "Al-Futtaim Power Systems LLC" \
  '{"supplierName":"Al-Futtaim Power Systems LLC","code":"SUP-AFP","category":"Electrical","email":"sales@alfuttaim-power.example","country":"UAE","phone":"+971 4 000 0001","primaryContact":"Khalid Rahman"}' >/dev/null
ensure "$MDM/suppliers" supplierName "Gulf Power Supplies LLC" \
  '{"supplierName":"Gulf Power Supplies LLC","code":"SUP-GPS","category":"Electrical","email":"sales@gulfpower.example","country":"UAE","phone":"+971 4 000 0002","primaryContact":"Ahmed Khan"}' >/dev/null

ensure "$MDM/service-providers" serviceProviderName "Caterpillar (UAE)" \
  '{"serviceProviderName":"Caterpillar (UAE)","code":"SP-CAT","serviceType":"AMC","email":"service@cat-uae.example","country":"UAE","phone":"+971 4 000 0101","primaryContact":"Service Desk"}' >/dev/null
ensure "$MDM/service-providers" serviceProviderName "ProServe Maintenance" \
  '{"serviceProviderName":"ProServe Maintenance","code":"SP-PRO","serviceType":"Repair","email":"support@proserve.example","country":"UAE","phone":"+971 4 000 0102","primaryContact":"Ravi Kumar"}' >/dev/null

ensure "$MDM/uoms" uomName "Unit" '{"uomName":"Unit","code":"UOM-UNIT","type":"Quantity","symbol":"unit","conversionFactor":1}' >/dev/null
ensure "$MDM/uoms" uomName "Set" '{"uomName":"Set","code":"UOM-SET","type":"Quantity","symbol":"set","conversionFactor":1}' >/dev/null

echo "== Step 5: Additional Information"
ensure "$MDM/standards" standardName "ISO 8528" \
  '{"standardName":"ISO 8528","code":"STD-ISO8528","organization":"ISO","category":"Generators","version":"2018"}' >/dev/null
ensure "$MDM/standards" standardName "UAE Civil Defence" \
  '{"standardName":"UAE Civil Defence","code":"STD-UAECD","organization":"UAE Civil Defence","category":"Fire & Life Safety","version":"2018"}' >/dev/null
ensure "$MDM/standards" standardName "Manufacturer Standard" \
  '{"standardName":"Manufacturer Standard","code":"STD-MFR","organization":"Manufacturer","category":"General"}' >/dev/null
ensure "$MDM/shifts" shiftName "Day Shift" \
  '{"shiftName":"Day Shift","code":"SH-DAY","startTime":"08:00","endTime":"16:00","workingDays":["Monday","Tuesday","Wednesday","Thursday","Friday"],"color":"#3B82F6"}' >/dev/null

echo "Done."
