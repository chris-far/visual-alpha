# Fund Lens

Extracts structured holdings data from mutual fund PDF reports using AI-assisted configuration.

## Prerequisites

An Anthropic API key is required. Add it to `src/VisualAlpha.FundLens.Api/appsettings.json`:

```json
"Anthropic": {
  "ApiKey": "sk-ant-..."
}
```

---

## Running the API

**Via terminal:**
```bash
cd src/VisualAlpha.FundLens.Api
dotnet run
```

**Via IDE:**

Run or debug the project **VisualAlpha.FundLens.Api** from your IDE (VS Code, Rider, Visual Studio)

---

Open Swagger at **http://localhost:5291/swagger**

---

## Sample data

Pre-generated examples are included under `samples/institutions` organised by institution (e.g. `/BlackRock`, `/Vanguard`, `/BNY`). Each folder contains a source PDF, a saved `.config.json`, and an extracted `holdings.json` showing the expected output.

---

## Option 1: Analyse a new PDF (onboarding)

Use this when you have a PDF for a fund that has not been seen before. A config will be generated and a test extraction will be run in one step.

**Endpoint:** `POST /api/onboarding/analyse`

1. In Swagger, click **POST /api/onboarding/analyse** → **Try it out**
2. Upload any fund report PDF using the `pdf` file picker
3. Leave `reportId` blank to generate a fresh config via AI, or supply the name of an existing config to skip the AI call
4. Click **Execute**

The response contains two things:
- `report`: the generated `ReportConfig` (column mappings, regex patterns, fund locators)
- `extractions`: a test extraction showing the holdings found in the uploaded PDF

The config will be saved to `samples/config/{reportId}.config.json` and can be reused for future extractions.

---

## Option 2: Extract holdings using a saved config

Use this for re-running extraction on a new period PDF for a fund that has already been onboarded.

**Endpoint:** `POST /api/extract/{reportId}`

Steps:

1. In Swagger, click **POST /api/extract/{reportId}** → **Try it out**
2. Enter a `reportId` matching a `.config.json` in the `samples/config` folder
3. Upload the matching fund PDF using the `pdf` file picker
4. Click **Execute**

The response is an array — one entry per fund in the report — each containing:
- `extraction` — the holdings with security names, market values, shares, country, confidence scores
- `validation` — pass/fail result with any findings (missing values, low confidence, invalid country codes)

---

## Tests

The tests are still a work in progress as this is still a prototype