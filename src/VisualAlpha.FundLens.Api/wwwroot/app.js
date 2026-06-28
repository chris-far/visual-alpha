// ── Configuration ────────────────────────────────────────────
// Change this to match your Kestrel port (check launchSettings.json)
const API = 'http://localhost:5291';

// ── State ────────────────────────────────────────────────────
let currentFile            = null;  // File object selected by user
let extractResult          = null;  // Last response from /api/onboarding/onboard
let allHoldings            = [];    // Full holdings array for filtering
let _lastOnboardingReport  = null;  // ReportConfig returned by last onboard call (not yet saved)
let _freshConfig           = null;  // Config open in editor that hasn't been saved yet

// ── Tab navigation ───────────────────────────────────────────
document.querySelectorAll('.nav-item').forEach(btn => {
    btn.addEventListener('click', () => showTab(btn.dataset.tab));
});

document.getElementById('btn-goto-extract').addEventListener('click', () => showTab('onboarding'));

function showTab(name) {
    document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
    document.querySelectorAll('.nav-item').forEach(b => b.classList.remove('active'));
    const tab = document.getElementById('tab-' + name);
    const btn = document.querySelector(`.nav-item[data-tab="${name}"]`);
    if (tab) tab.classList.add('active');
    if (btn) btn.classList.add('active');
    if (name === 'dashboard') loadDashboard();

    if (name === 'config')    loadConfigReports();
}

// ── Dashboard ────────────────────────────────────────────────
async function loadDashboard() {
    try {
        const reports = await apiFetch('/api/runs/reports');
        document.getElementById('m-total').textContent = reports.length;
        renderReportsTable(reports);
    } catch {
        document.getElementById('reports-tbody').innerHTML =
            '<tr><td colspan="5" class="empty">Could not reach API — is the server running?</td></tr>';
    }
}

function renderReportsTable(reports) {
    const tbody = document.getElementById('reports-tbody');
    if (!reports.length) {
        tbody.innerHTML = '<tr><td colspan="4" class="empty">No reports onboarded yet.</td></tr>';
        return;
    }
    tbody.innerHTML = reports.map(r => `
    <tr class="clickable" onclick="openConfig('${esc(r.reportId)}')">
      <td>${esc(r.publisher ?? r.reportId)}</td>
      <td>${esc(r.displayName ?? r.reportId)}</td>
      <td>${r.createdAt ? new Date(r.createdAt).toLocaleDateString('en-US', { year: 'numeric', month: 'long', day: 'numeric' }) : '—'}</td>
      <td style="display:flex;gap:6px;justify-content:flex-end">
        <button class="btn" onclick="event.stopPropagation();extractReport('${esc(r.reportId)}')">Get Holdings</button>
        <button class="btn btn-danger-ghost" onclick="event.stopPropagation();deleteReport('${esc(r.reportId)}','${esc(r.displayName ?? r.reportId)}')">Delete</button>
      </td>
    </tr>`).join('');
}

function extractReport(reportId) {
    showTab('extract');
    loadExtractReports().then(() => { exReportSel.value = reportId; exReportId = reportId; });
}

async function deleteReport(reportId, displayName) {
    if (!confirm(`Delete "${displayName ?? reportId}"? This cannot be undone.`)) return;
    try {
        const res = await fetch(API + `/api/runs/reports/${encodeURIComponent(reportId)}`, { method: 'DELETE' });
        if (!res.ok) throw new Error(res.statusText);
        await Promise.all([loadDashboard(), loadConfigReports(), loadExtractReports()]);
    } catch (e) {
        alert('Delete failed: ' + e.message);
    }
}


function openConfig(reportId) {
    showTab('config');
    document.getElementById('config-report-select').value = reportId;
    loadConfig(reportId);
}

// ── Onboarding tab ───────────────────────────────────────────
const dropZone   = document.getElementById('drop-zone');
const fileInput  = document.getElementById('file-input');
const btnOnboarding = document.getElementById('btn-onboarding');

// Drag-and-drop
dropZone.addEventListener('dragover', e => { e.preventDefault(); dropZone.classList.add('dragging'); });
dropZone.addEventListener('dragleave', () => dropZone.classList.remove('dragging'));
dropZone.addEventListener('drop', e => {
    e.preventDefault();
    dropZone.classList.remove('dragging');
    const file = e.dataTransfer.files[0];
    if (file?.type === 'application/pdf') setFile(file);
});

fileInput.addEventListener('change', () => {
    if (fileInput.files[0]) setFile(fileInput.files[0]);
});

function setFile(file) {
    currentFile = file;
    document.getElementById('drop-filename').textContent = file.name;
    btnOnboarding.disabled = false;
}

// Run extraction
btnOnboarding.addEventListener('click', runOnboarding);

async function runOnboarding() {
    if (!currentFile) return;
    btnOnboarding.disabled = true;

    // Show progress
    const progressPanel  = document.getElementById('progress-panel');
    const resultsContainer = document.getElementById('ob-results-container');
    progressPanel.hidden   = false;
    resultsContainer.hidden = true;
    setProgress(15, 'Ingesting PDF…');

    const formData = new FormData();
    formData.append('pdf', currentFile);

    try {
        setProgress(40, 'Generating / loading config…');
        const result = await fetch(`${API}/api/onboarding/onboard`, { method: 'POST', body: formData })
            .then(r => { if (!r.ok) throw new Error(r.statusText); return r.json(); });

        setProgress(80, 'Validating holdings…');
        await delay(400);
        setProgress(100, 'Done.');
        await delay(300);

        extractResult          = result;
        allHoldings            = result.extractions?.flatMap(p => (p.extraction ?? p).holdings) ?? [];
        _lastOnboardingReport  = result.report ?? null;
        progressPanel.hidden = true;
        document.getElementById('upload-panel').hidden = true;
        renderResults(result, resultsContainer);
        resultsContainer.hidden = false;
        document.getElementById('btn-cancel-onboarding').hidden = false;
        document.getElementById('btn-review-onboarding').hidden = false;
    } catch (err) {
        setProgress(100, '');
        progressPanel.hidden = true;
        alert('Extraction failed: ' + err.message);
    } finally {
        btnOnboarding.disabled = false;
    }
}

function setProgress(pct, msg) {
    document.getElementById('progress-bar').style.width = pct + '%';
    document.getElementById('progress-status').textContent = msg;
}

function resetOnboardingPane() {
    currentFile = null;
    _lastOnboardingReport = null;
    document.getElementById('drop-filename').textContent = '';
    fileInput.value = '';
    btnOnboarding.disabled = true;
    document.getElementById('ob-results-container').hidden = true;
    document.getElementById('progress-panel').hidden = true;
    document.getElementById('upload-panel').hidden   = false;
    document.getElementById('btn-cancel-onboarding').hidden = true;
    document.getElementById('btn-review-onboarding').hidden = true;
}

document.getElementById('btn-cancel-onboarding').addEventListener('click', resetOnboardingPane);

document.getElementById('btn-review-onboarding').addEventListener('click', () => {
    if (!_lastOnboardingReport) return;
    showTab('config');
    clearConfigEditor();
    _freshConfig = _lastOnboardingReport;
    setFreshMode(true);
    const editor = document.getElementById('config-editor');
    editor.value = JSON.stringify(_freshConfig, null, 2);
    document.getElementById('config-view-toggle').hidden = false;
    renderOverviewPanel(_freshConfig);
    renderIssuesPanel(_freshConfig);
    renderConfigViz(_freshConfig);
    renderRegexEditor(_freshConfig);
    renderFundsTable(_freshConfig.funds ?? []);
});

function renderResults(result, container) {
    container.innerHTML = '';

    const paired      = result.extractions ?? [];
    const extractions = paired.map(p => p.extraction ?? p);
    const failed      = extractions.filter(e => e.status === 'Failed');
    const failErrors  = failed.map(e => e.errorMessage).filter(Boolean);
    const extWarnings = extractions.flatMap(e => (e.warnings ?? []).map(w => ({ severity: 'Warning', ruleName: 'Extraction', message: w, fieldName: null })));
    const valFindings = paired.flatMap(p => p.validation?.findings ?? []);
    const allFindings = [...valFindings, ...extWarnings];
    const errors      = allFindings.filter(f => f.severity === 'Error' || f.severity === 'Critical');
    const warnings    = allFindings.filter(f => f.severity === 'Warning');

    const bannerCls  = failed.length || errors.length ? 'fail' : warnings.length ? 'warn' : 'ok';
    const bannerIcon = failed.length || errors.length ? 'ti-circle-x' : warnings.length ? 'ti-alert-triangle' : 'ti-circle-check';
    const bannerMsg  = failed.length
        ? `Extraction failed: ${failErrors.map(esc).join('; ') || 'Unknown error'}`
        : errors.length   ? `${errors.length} error(s) found — see findings below`
        : warnings.length ? `${warnings.length} warning(s) — see findings below`
        : 'All validation checks passed.';

    // Summary panel
    const summaryPanel = document.createElement('div');
    summaryPanel.className = 'panel';
    summaryPanel.innerHTML = `
    <div class="panel-header">
        Results — ${allHoldings.length} holding${allHoldings.length !== 1 ? 's' : ''}
        <div style="display:flex;gap:8px">
            <button class="btn" id="btn-dl-json">Download JSON</button>
            <button class="btn" id="btn-dl-csv">Download CSV</button>
        </div>
    </div>
    <div class="validation-banner ${bannerCls}" style="margin:12px 16px">
        <i class="ti ${bannerIcon}"></i> ${bannerMsg}
    </div>`;
    summaryPanel.querySelector('#btn-dl-json').onclick = () => downloadJSON(result);
    summaryPanel.querySelector('#btn-dl-csv').onclick  = () => downloadCSV(allHoldings);
    container.appendChild(summaryPanel);

    // Collapsible findings panel
    if (allFindings.length) {
        const sevBadge = s => {
            const map = { Info: 'badge-new', Warning: 'badge-warn', Error: 'badge-fail', Critical: 'badge-fail' };
            return `<span class="badge ${map[s] ?? 'badge-new'}">${esc(s)}</span>`;
        };
        const findingsPanel = document.createElement('div');
        findingsPanel.className = 'panel';
        findingsPanel.innerHTML = `
        <div class="panel-header findings-toggle" style="cursor:pointer;user-select:none">
            Validation findings (${allFindings.length})
            <i class="ti ti-chevron-down"></i>
        </div>
        <div class="findings-body">
            <div class="table-scroll">
                <table>
                    <thead><tr><th>Severity</th><th>Rule</th><th>Message</th><th>Field</th></tr></thead>
                    <tbody>${allFindings.map(f => `
                    <tr>
                        <td>${sevBadge(f.severity)}</td>
                        <td>${esc(f.ruleName ?? '—')}</td>
                        <td>${esc(f.message ?? '—')}</td>
                        <td>${esc(f.fieldName ?? '—')}</td>
                    </tr>`).join('')}
                    </tbody>
                </table>
            </div>
        </div>`;
        findingsPanel.querySelector('.findings-toggle').addEventListener('click', () => {
            const body    = findingsPanel.querySelector('.findings-body');
            const chevron = findingsPanel.querySelector('.ti');
            body.hidden   = !body.hidden;
            chevron.className = `ti ${body.hidden ? 'ti-chevron-right' : 'ti-chevron-down'}`;
        });
        container.appendChild(findingsPanel);
    }

    // Holdings panel
    const holdingsPanel = document.createElement('div');
    holdingsPanel.className = 'panel';
    holdingsPanel.innerHTML = `
    <div class="panel-header">Holdings</div>
    <div class="search-bar">
        <i class="ti ti-search" aria-hidden="true"></i>
        <input type="text" id="holdings-search" placeholder="Filter by name, sector, country…" />
    </div>
    <div class="table-scroll holdings-scroll">
        <table id="holdings-table">
            <thead><tr>
                <th>Security name</th><th>Type</th><th>Sector</th>
                <th>Country</th><th>Shares</th><th>Principal</th>
                <th class="num">Market value</th>
            </tr></thead>
            <tbody id="holdings-tbody"></tbody>
        </table>
    </div>
    <div class="table-footer" id="holdings-count"></div>`;
    holdingsPanel.querySelector('#holdings-search').addEventListener('input', e => {
        const q = e.target.value.toLowerCase();
        const filtered = allHoldings.filter(h =>
            [h.securityName, h.securityType, h.sector, h.country].some(v => v?.toLowerCase().includes(q))
        );
        renderHoldingsTable(filtered);
    });
    container.appendChild(holdingsPanel);
    renderHoldingsTable(allHoldings);
}

function renderHoldingsTable(holdings) {
    const tbody = document.getElementById('holdings-tbody');
    document.getElementById('holdings-count').textContent =
        `Showing ${holdings.length} holding${holdings.length !== 1 ? 's' : ''}`;

    if (!holdings.length) {
        tbody.innerHTML = '<tr><td colspan="7" class="empty">No holdings extracted.</td></tr>';
        return;
    }

    tbody.innerHTML = holdings.map(h => `
    <tr>
      <td>${esc(h.securityName ?? '—')}</td>
      <td>${esc(h.securityType ?? '—')}</td>
      <td>${esc(h.sector ?? '—')}</td>
      <td>${esc(h.country ?? '—')}</td>
      <td class="num">${h.shares != null ? Number(h.shares).toLocaleString() : '—'}</td>
      <td class="num">${h.principal != null ? '$' + Number(h.principal).toLocaleString() : '—'}</td>
      <td class="num">${h.marketValue != null ? '$' + Number(h.marketValue).toLocaleString() : '—'}</td>
    </tr>`).join('');
}


function downloadJSON(result) {
    const blob = new Blob([JSON.stringify(result, null, 2)], { type: 'application/json' });
    triggerDownload(blob, 'extraction-result.json');
}

function downloadCSV(holdings) {
    const cols = ['securityName','securityType','sector','country','shares','principal','marketValue'];
    const header = cols.join(',');
    const rows = holdings.map(h => cols.map(c => `"${(h[c] ?? '').toString().replace(/"/g, '""')}"`).join(','));
    const blob = new Blob([header + '\n' + rows.join('\n')], { type: 'text/csv' });
    triggerDownload(blob, 'holdings.csv');
}

function triggerDownload(blob, filename) {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url; a.download = filename; a.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
}

// ── Extract tab ──────────────────────────────────────────────
let exCurrentFile = null;
let exReportId    = '';
let exAllHoldings = [];

const exDropZone  = document.getElementById('ex-drop-zone');
const exFileInput = document.getElementById('ex-file-input');
const btnExtract    = document.getElementById('btn-extract');
const exReportSel = document.getElementById('ex-report-select');

async function loadExtractReports() {
    try {
        const configs = await apiFetch('/api/runs/reports');
        const prev = exReportSel.value;
        exReportSel.innerHTML = '<option value="">Select a report…</option>';
        configs.forEach(c => {
            const opt = document.createElement('option');
            opt.value = c.reportId;
            opt.textContent = c.displayName ?? c.publisher ?? c.reportId;
            exReportSel.appendChild(opt);
        });
        if (prev && configs.some(c => c.reportId === prev)) exReportSel.value = prev;
    } catch { /* silently skip */ }
}
loadExtractReports();

function resetExtractPane() {
    exCurrentFile = null;
    exReportId    = '';
    exReportSel.value = '';
    document.getElementById('ex-drop-filename').textContent = '';
    exFileInput.value = '';
    document.getElementById('ex-upload-panel').hidden    = false;
    document.getElementById('ex-progress-panel').hidden  = true;
    document.getElementById('ex-results-container').innerHTML = '';
    document.getElementById('ex-btn-clear').hidden = true;
    btnExtract.disabled = true;
}

document.getElementById('ex-btn-clear').addEventListener('click', resetExtractPane);

exReportSel.addEventListener('change', () => {
    exReportId = exReportSel.value;
    if (!exReportId) {
        resetExtractPane();
        return;
    }
    btnExtract.disabled = !(exCurrentFile && exReportId);
});

exDropZone.addEventListener('dragover', e => { e.preventDefault(); exDropZone.classList.add('dragging'); });
exDropZone.addEventListener('dragleave', () => exDropZone.classList.remove('dragging'));
exDropZone.addEventListener('drop', e => {
    e.preventDefault();
    exDropZone.classList.remove('dragging');
    const file = e.dataTransfer.files[0];
    if (file?.type === 'application/pdf') setExFile(file);
});
exFileInput.addEventListener('change', () => { if (exFileInput.files[0]) setExFile(exFileInput.files[0]); });

function setExFile(file) {
    exCurrentFile = file;
    document.getElementById('ex-drop-filename').textContent = file.name;
    btnExtract.disabled = !exReportId;
}

btnExtract.addEventListener('click', runExtract);

async function runExtract() {
    if (!exCurrentFile || !exReportId) return;
    btnExtract.disabled = true;

    const progressPanel    = document.getElementById('ex-progress-panel');
    const resultsContainer = document.getElementById('ex-results-container');
    progressPanel.hidden     = false;
    resultsContainer.innerHTML = '';
    setExProgress(20, 'Uploading PDF…');

    const formData = new FormData();
    formData.append('pdf', exCurrentFile);

    try {
        setExProgress(50, 'Extracting holdings…');
        const results = await fetch(`${API}/api/extract/${encodeURIComponent(exReportId)}`, {
            method: 'POST', body: formData,
        }).then(r => { if (!r.ok) throw new Error(r.statusText); return r.json(); });

        setExProgress(90, 'Running validation…');
        await delay(200);
        setExProgress(100, 'Done.');
        await delay(200);

        exAllHoldings = results.flatMap(r => r.extraction?.holdings ?? []);
        progressPanel.hidden = true;
        document.getElementById('ex-upload-panel').hidden = true;
        document.getElementById('ex-btn-clear').hidden = false;
        renderExtractResults(results, resultsContainer);
    } catch (err) {
        progressPanel.hidden = true;
        alert('Extraction failed: ' + err.message);
    } finally {
        btnExtract.disabled = false;
    }
}

function setExProgress(pct, msg) {
    document.getElementById('ex-progress-bar').style.width = pct + '%';
    document.getElementById('ex-progress-status').textContent = msg;
}

function renderExtractResults(results, container) {
    const allFindings = results.flatMap(r => r.validation?.findings ?? []);
    const errors      = allFindings.filter(f => f.severity === 'Error' || f.severity === 'Critical');
    const warnings    = allFindings.filter(f => f.severity === 'Warning');

    const bannerCls  = errors.length ? 'fail' : warnings.length ? 'warn' : 'ok';
    const bannerIcon = errors.length ? 'ti-circle-x' : warnings.length ? 'ti-alert-triangle' : 'ti-circle-check';
    const bannerMsg  = errors.length
        ? `${errors.length} error(s) found across ${results.length} fund(s)`
        : warnings.length ? `${warnings.length} warning(s)` : 'All validation checks passed.';

    // Summary panel
    const summaryPanel = document.createElement('div');
    summaryPanel.className = 'panel';
    summaryPanel.innerHTML = `
    <div class="panel-header">
        Results — ${exAllHoldings.length} holding${exAllHoldings.length !== 1 ? 's' : ''} across ${results.length} fund${results.length !== 1 ? 's' : ''}
        <div style="display:flex;gap:8px">
            <button class="btn" id="ex-btn-dl-json">Download JSON</button>
            <button class="btn" id="ex-btn-dl-csv">Download CSV</button>
        </div>
    </div>
    <div class="validation-banner ${bannerCls}" style="margin:12px 16px">
        <i class="ti ${bannerIcon}"></i> ${esc(bannerMsg)}
    </div>`;
    summaryPanel.querySelector('#ex-btn-dl-json').onclick = () => downloadJSON(results);
    summaryPanel.querySelector('#ex-btn-dl-csv').onclick  = () => downloadCSV(exAllHoldings);
    container.appendChild(summaryPanel);

    // Validation findings
    if (allFindings.length) {
        const findingsPanel = document.createElement('div');
        findingsPanel.className = 'panel';
        const sevBadge = s => {
            const map = { Info: 'badge-new', Warning: 'badge-warn', Error: 'badge-fail', Critical: 'badge-fail' };
            return `<span class="badge ${map[s] ?? 'badge-new'}">${esc(s)}</span>`;
        };
        findingsPanel.innerHTML = `
        <div class="panel-header">Validation findings (${allFindings.length})</div>
        <div class="table-scroll">
            <table>
                <thead><tr><th>Severity</th><th>Rule</th><th>Message</th><th>Field</th></tr></thead>
                <tbody>${allFindings.map(f => `
                <tr>
                    <td>${sevBadge(f.severity)}</td>
                    <td>${esc(f.ruleName ?? '—')}</td>
                    <td>${esc(f.message ?? '—')}</td>
                    <td>${esc(f.fieldName ?? '—')}</td>
                </tr>`).join('')}
                </tbody>
            </table>
        </div>`;
        container.appendChild(findingsPanel);
    }

    // Holdings
    const holdingsPanel = document.createElement('div');
    holdingsPanel.className = 'panel';
    holdingsPanel.innerHTML = `
    <div class="panel-header">Holdings</div>
    <div class="search-bar">
        <i class="ti ti-search" aria-hidden="true"></i>
        <input type="text" id="ex-holdings-search" placeholder="Filter by name, sector, country…" />
    </div>
    <div class="table-scroll">
        <table>
            <thead><tr>
                <th>Security name</th><th>Type</th><th>Sector</th>
                <th>Country</th><th>Shares</th><th>Principal</th><th class="num">Market value</th>
            </tr></thead>
            <tbody id="ex-holdings-tbody">${renderExHoldingRows(exAllHoldings)}</tbody>
        </table>
    </div>
    <div class="table-footer" id="ex-holdings-count">Showing ${exAllHoldings.length} holding${exAllHoldings.length !== 1 ? 's' : ''}</div>`;
    holdingsPanel.querySelector('#ex-holdings-search').addEventListener('input', e => {
        const q = e.target.value.toLowerCase();
        const filtered = exAllHoldings.filter(h =>
            [h.securityName, h.securityType, h.sector, h.country].some(v => v?.toLowerCase().includes(q))
        );
        document.getElementById('ex-holdings-tbody').innerHTML = renderExHoldingRows(filtered);
        document.getElementById('ex-holdings-count').textContent =
            `Showing ${filtered.length} holding${filtered.length !== 1 ? 's' : ''}`;
    });
    container.appendChild(holdingsPanel);
}

function renderExHoldingRows(holdings) {
    if (!holdings.length) return '<tr><td colspan="7" class="empty">No holdings extracted.</td></tr>';
    return holdings.map(h => `
    <tr>
        <td>${esc(h.securityName ?? '—')}</td>
        <td>${esc(h.securityType ?? '—')}</td>
        <td>${esc(h.sector ?? '—')}</td>
        <td>${esc(h.country ?? '—')}</td>
        <td class="num">${h.shares != null ? Number(h.shares).toLocaleString() : '—'}</td>
        <td class="num">${h.principal != null ? '$' + Number(h.principal).toLocaleString() : '—'}</td>
        <td class="num">${h.marketValue != null ? '$' + Number(h.marketValue).toLocaleString() : '—'}</td>
    </tr>`).join('');
}

// ── Config editor ────────────────────────────────────────────
let _hasIssues = false;

document.getElementById('btn-view-viz').addEventListener('click',  () => switchConfigView('viz'));
document.getElementById('btn-view-json').addEventListener('click', () => switchConfigView('json'));

function switchConfigView(view) {
    document.getElementById('overview-panel').hidden      = view !== 'viz' || !_showOverview;
    document.getElementById('config-empty-state').hidden  = true;
    document.getElementById('config-layout-pane').hidden  = view !== 'viz';
    document.getElementById('issues-panel').hidden        = view !== 'viz' || !_hasIssues;
    document.getElementById('config-json-panel').hidden   = view !== 'json';
    document.getElementById('btn-view-viz').classList.toggle('active',  view === 'viz');
    document.getElementById('btn-view-json').classList.toggle('active', view === 'json');
}
async function loadConfigReports() {
    try {
        const configs = await apiFetch('/api/runs/reports');
        const sel = document.getElementById('config-report-select');
        const prev = sel.value;
        sel.innerHTML = '<option value="">Select a report…</option>';
        configs.forEach(c => {
            const opt = document.createElement('option');
            opt.value = c.reportId;
            opt.textContent = c.displayName ?? c.publisher ?? c.reportId;
            sel.appendChild(opt);
        });
        if (prev && configs.some(c => c.reportId === prev)) sel.value = prev;
        sel.onchange = () => {
            if (sel.value) {
                loadConfig(sel.value);
            } else {
                clearConfigEditor();
            }
        };
    } catch { /* silently fail */ }
}

function setFreshMode(isFresh) {
    document.getElementById('config-report-select').hidden  = isFresh;
    document.getElementById('btn-cancel-config').hidden     = !isFresh;
    document.getElementById('btn-save-config').textContent  = isFresh ? 'Onboard Report' : 'Update Report';
    document.getElementById('btn-save-config').disabled     = !isFresh;
}

document.getElementById('btn-cancel-config').addEventListener('click', () => {
    _freshConfig = null;
    setFreshMode(false);
    clearConfigEditor();
});

document.getElementById('btn-clear-config').addEventListener('click', clearConfigEditor);

function clearConfigEditor() {
    _freshConfig = null;
    _showOverview = false;
    setFreshMode(false);
    document.getElementById('config-editor').value = '';
    document.getElementById('config-viz').innerHTML = '';
    document.getElementById('config-empty-state').hidden = false;
    document.getElementById('overview-panel').hidden     = true;
    document.getElementById('config-layout-pane').hidden = true;
    document.getElementById('issues-panel').hidden       = true;
    document.getElementById('config-json-panel').hidden  = true;
    document.getElementById('config-view-toggle').hidden = true;
    document.getElementById('patterns-panel').hidden     = true;
    document.getElementById('regex-panel').hidden        = true;
    document.getElementById('btn-clear-config').hidden   = true;
    _hasIssues = false;
    document.getElementById('btn-view-viz').classList.add('active');
    document.getElementById('btn-view-json').classList.remove('active');
    document.getElementById('config-report-select').value = '';
    _vizConfig = null;
}

function renderOverviewPanel(config) {
    const typeLabel = { SingleFund: 'Single Fund', MultiFund: 'Multi Fund' };
    const name = config.displayName ?? config.reportId ?? '—';
    document.getElementById('overview-panel-title').textContent = name;
    document.getElementById('overview-name-input').value        = config.displayName ?? '';
    document.getElementById('ov-id').textContent        = config.reportId ?? '—';
    document.getElementById('ov-publisher').textContent = config.publisher ?? '—';
    document.getElementById('ov-publisher-input').value = config.publisher ?? '';
    document.getElementById('ov-type').textContent      = typeLabel[config.reportType] ?? config.reportType ?? '—';
    document.getElementById('ov-created').textContent   = config.createdAt
        ? new Date(config.createdAt).toLocaleDateString('en-US', { year: 'numeric', month: 'long', day: 'numeric' })
        : '—';
    _showOverview = true;
    document.getElementById('overview-panel').hidden = false;
}

document.getElementById('ov-publisher-edit-btn').addEventListener('click', () => {
    document.getElementById('ov-publisher-view').hidden     = true;
    document.getElementById('ov-publisher-edit-row').hidden = false;
    document.getElementById('ov-publisher-input').focus();
});

document.getElementById('ov-publisher-done-btn').addEventListener('click', () => {
    const value = document.getElementById('ov-publisher-input').value.trim() || null;
    document.getElementById('ov-publisher').textContent         = value ?? '—';
    document.getElementById('ov-publisher-view').hidden         = false;
    document.getElementById('ov-publisher-edit-row').hidden     = true;
    try {
        const config = JSON.parse(document.getElementById('config-editor').value);
        config.publisher = value;
        document.getElementById('config-editor').value = JSON.stringify(config, null, 2);
    } catch { /* ignore mid-edit */ }
});

document.getElementById('overview-name-edit-btn').addEventListener('click', () => {
    document.getElementById('overview-name-view').hidden = true;
    document.getElementById('overview-name-edit').hidden = false;
    document.getElementById('overview-name-input').focus();
});

document.getElementById('overview-name-done-btn').addEventListener('click', () => {
    const value = document.getElementById('overview-name-input').value.trim() || null;
    document.getElementById('overview-panel-title').textContent = value ?? '—';
    document.getElementById('overview-name-view').hidden = false;
    document.getElementById('overview-name-edit').hidden = true;
    try {
        const config = JSON.parse(document.getElementById('config-editor').value);
        config.displayName = value;
        document.getElementById('config-editor').value = JSON.stringify(config, null, 2);
    } catch { /* ignore mid-edit */ }
});

async function loadConfig(reportId) {
    _freshConfig = null;
    setFreshMode(false);
    try {
        const configs = await apiFetch('/api/runs/reports');
        const config = configs.find(c => c.reportId === reportId);
        if (!config) throw new Error('Not found');
        document.getElementById('config-editor').value = JSON.stringify(config, null, 2);
        document.getElementById('btn-save-config').disabled = false;
        document.getElementById('btn-clear-config').hidden = false;
        document.getElementById('config-view-toggle').hidden = false;
        renderOverviewPanel(config);
        renderIssuesPanel(config);
        renderConfigViz(config);
        renderRegexEditor(config);
        renderFundsTable(config.funds ?? []);
        document.getElementById('config-report-select').value = reportId;
    } catch {
        document.getElementById('config-editor').value = '// Could not load config for this report.';
    }
}

// ── Column viz drag state (module-level, handlers wired once) ─
let _vizConfig     = null;
let _showOverview  = false;
let _vizWidth  = 612;
let _vizDrag   = null;
const _bc      = ['band-left', 'band-right', 'band-mark', 'band-4', 'band-5', 'band-6', 'band-7', 'band-8'];

document.addEventListener('mousemove', e => {
    if (!_vizDrag) return;
    const { gi, fi, side, trackEl } = _vizDrag;
    const rect   = trackEl.getBoundingClientRect();
    const rawX   = Math.round(((e.clientX - rect.left) / rect.width) * _vizWidth);
    const fields = _vizConfig.reportLayout.tableConfig.columnGroups[gi].fields;
    const f      = fields[fi];

    if (side === 'left') {
        const minX = fi > 0 ? fields[fi - 1].rightX + 2 : 0;
        f.leftX = Math.max(minX, Math.min(rawX, f.rightX - 10));
    } else {
        const maxX = fi < fields.length - 1 ? fields[fi + 1].leftX - 2 : _vizWidth;
        f.rightX = Math.min(maxX, Math.max(rawX, f.leftX + 10));
    }

    const pct  = x => ((x / _vizWidth) * 100).toFixed(3) + '%';
    const wPct = (l, r) => (((r - l) / _vizWidth) * 100).toFixed(3) + '%';
    const band = trackEl.querySelector(`.col-band[data-gi="${gi}"][data-fi="${fi}"]`);
    if (band) { band.style.left = pct(f.leftX); band.style.width = wPct(f.leftX, f.rightX); }

    const coord = document.getElementById(`col-coord-${gi}-${fi}`);
    if (coord) {
        const dot = coord.querySelector('.col-coord-dot').outerHTML;
        coord.innerHTML = `${dot} ${esc(f.headerText ?? String(f.field))}: ${f.leftX}–${f.rightX} pt`;
    }
});

document.addEventListener('mouseup', () => {
    if (!_vizDrag) return;
    const { gi, fi } = _vizDrag;
    _vizDrag = null;
    try {
        const current = JSON.parse(document.getElementById('config-editor').value);
        const f = _vizConfig.reportLayout.tableConfig.columnGroups[gi].fields[fi];
        current.reportLayout.tableConfig.columnGroups[gi].fields[fi].leftX  = f.leftX;
        current.reportLayout.tableConfig.columnGroups[gi].fields[fi].rightX = f.rightX;
        document.getElementById('config-editor').value = JSON.stringify(current, null, 2);
    } catch { /* ignore */ }
});

function renderIssuesPanel(config) {
    const issues = config.issues ?? config.Issues ?? [];
    const panel  = document.getElementById('issues-panel');
    if (!issues.length) {
        panel.hidden = true;
        _hasIssues = false;
        return;
    }
    _hasIssues = true;
    document.getElementById('issues-tbody').innerHTML = issues.map(msg => `
    <tr>
        <td><i class="ti ti-alert-triangle" style="color:var(--text-hint);font-size:14px"></i></td>
        <td>${esc(msg)}</td>
    </tr>`).join('');
    panel.querySelector('.issues-toggle').addEventListener('click', () => {
        const body    = panel.querySelector('.issues-body');
        const chevron = panel.querySelector('.issues-toggle .ti');
        body.hidden   = !body.hidden;
        chevron.className = `ti ${body.hidden ? 'ti-chevron-right' : 'ti-chevron-down'}`;
    });
    panel.hidden = false;
}

function renderConfigViz(config) {
    document.getElementById('config-empty-state').hidden = true;
    document.getElementById('config-layout-pane').hidden = false;

    const viz    = document.getElementById('config-viz');
    const groups = config.reportLayout?.tableConfig?.columnGroups ?? [];
    const allFields = groups.flatMap(g => g.fields ?? []);

    if (!allFields.length) {
        viz.innerHTML = '<p class="empty">No column layout found in config.</p>';
        return;
    }

    _vizConfig = config;
    _vizWidth  = config.reportLayout?.tableConfig?.totalWidth
        ?? Math.max(...allFields.map(f => f.rightX ?? 0), 612);

    const w    = _vizWidth;
    const pct  = x => ((x / w) * 100).toFixed(3) + '%';
    const wPct = (l, r) => (((r - l) / w) * 100).toFixed(3) + '%';

    const ruler = `
    <div class="page-ruler">
      <span>0</span><span>${Math.round(w * 0.25)}</span>
      <span>${Math.round(w * 0.5)}</span><span>${Math.round(w * 0.75)}</span>
      <span>${w} pt</span>
    </div>`;

    const allBands = groups.flatMap((group, gi) =>
        (group.fields ?? []).map((f, fi) => `
        <div class="band ${_bc[fi % _bc.length]} col-band"
             data-gi="${gi}" data-fi="${fi}"
             style="left:${pct(f.leftX)};width:${wPct(f.leftX, f.rightX)}">
          <div class="col-handle col-handle-left"  data-gi="${gi}" data-fi="${fi}" data-side="left"></div>
          <span class="col-band-label">${esc(f.headerText ?? String(f.field))}</span>
          <div class="col-handle col-handle-right" data-gi="${gi}" data-fi="${fi}" data-side="right"></div>
        </div>`)
    ).join('');

    const allCoords = groups.flatMap((group, gi) =>
        (group.fields ?? []).map((f, fi) => `
        <span class="col-coord-item" id="col-coord-${gi}-${fi}">
          <span class="col-coord-dot" style="background:var(--${_bc[fi % _bc.length]})"></span>
          ${esc(f.headerText ?? String(f.field))}: ${f.leftX}–${f.rightX} pt
        </span>`)
    ).join('');

    viz.innerHTML = ruler + `
    <div class="band-track col-viz-track">${allBands}</div>
    <div class="col-coords">${allCoords}</div>`;

    const trackEl = viz.querySelector('.col-viz-track');
    viz.querySelectorAll('.col-handle').forEach(handle => {
        handle.addEventListener('mousedown', e => {
            e.preventDefault();
            _vizDrag = { gi: +handle.dataset.gi, fi: +handle.dataset.fi,
                         side: handle.dataset.side, trackEl };
        });
    });
}

function renderRegexEditor(config) {
    const panel  = document.getElementById('regex-editor-panel');
    document.getElementById('regex-panel').hidden = false;
    const layout = config.reportLayout ?? {};

    panel.innerHTML = [
        regexPatternRow('reportDatePattern',          'Report Date',   layout.reportDatePattern),
        regexPatternRow('securityTypePattern',        'Security Type', layout.securityTypePattern),
        regexPatternRow('countryPattern',             'Country',       layout.countryPattern),
        regexPatternRow('sectorPattern',              'Sector',        layout.sectorPattern),
        regexPatternRow('securityNameCleaningPattern','Name Cleaning', layout.securityNameCleaningPattern,
            'Regex applied to strip boilerplate from raw security names — e.g. fund class suffixes, share type codes, or trailing punctuation.'),
        regexPatternRow('subtotalRowPattern',         'Subtotals',     layout.subtotalRowPattern),
        regexPatternRow('footnotePattern',            'Footnotes',     layout.footnotePattern),
    ].join('');

    panel.querySelectorAll('.regex-input').forEach(input =>
        input.addEventListener('input', syncRegexToEditor)
    );
    panel.querySelectorAll('.pattern-edit-btn').forEach(btn =>
        btn.addEventListener('click', () => {
            const cell = btn.closest('.pattern-cell');
            cell.querySelector('.pattern-view').hidden = true;
            cell.querySelector('.pattern-edit').hidden = false;
            cell.querySelector('.regex-input').focus();
        })
    );
    panel.querySelectorAll('.pattern-done-btn').forEach(btn =>
        btn.addEventListener('click', () => {
            const cell = btn.closest('.pattern-cell');
            cell.querySelector('.pattern-view').hidden = false;
            cell.querySelector('.pattern-edit').hidden = true;
        })
    );
}

function regexPatternRow(key, label, pattern, tooltip = null) {
    const labelHtml = tooltip
        ? `${esc(label)}<span class="field-hint" data-tooltip="${esc(tooltip)}"><i class="ti ti-info-circle" aria-hidden="true"></i></span>`
        : esc(label);
    return `
    <div class="regex-row" data-key="${key}">
        <div class="regex-label">${labelHtml}</div>
        <div class="pattern-cell">
            <div class="pattern-view">
                <button class="pattern-edit-btn" aria-label="Edit regex"><i class="ti ti-pencil" aria-hidden="true"></i></button>
                <div class="pattern-view-body">
                    ${pattern?.example ? `<div class="pattern-example">${esc(pattern.example)}</div>` : '<span class="pattern-empty">—</span>'}
                </div>
            </div>
            <div class="pattern-edit" hidden>
                <button class="pattern-done-btn" aria-label="Done"><i class="ti ti-check" aria-hidden="true"></i></button>
                <div class="regex-input-wrap">
                    <span class="regex-delimiter">/</span>
                    <input class="regex-input" type="text" data-key="${key}"
                           value="${esc(pattern?.regex ?? '')}" placeholder="(no pattern)" />
                    <span class="regex-delimiter">/</span>
                </div>
            </div>
        </div>
    </div>`;
}

function regexStringRow(key, label, value, tooltip = null) {
    const labelHtml = tooltip
        ? `${esc(label)}<span class="field-hint" data-tooltip="${esc(tooltip)}"><i class="ti ti-info-circle" aria-hidden="true"></i></span>`
        : esc(label);
    return `
    <div class="regex-row" data-key="${key}">
        <div class="regex-label">${labelHtml}</div>
        <div class="regex-input-wrap">
            <span class="regex-delimiter">/</span>
            <input class="regex-input" type="text" data-key="${key}"
                   value="${esc(value ?? '')}" placeholder="(no pattern)" />
            <span class="regex-delimiter">/</span>
        </div>
    </div>`;
}


function syncRegexToEditor() {
    try {
        const config = JSON.parse(document.getElementById('config-editor').value);
        document.getElementById('regex-editor-panel').querySelectorAll('.regex-input').forEach(input => {
            setNestedRegex(config, input.dataset.key, input.value || null);
        });
        document.getElementById('config-editor').value = JSON.stringify(config, null, 2);
    } catch { /* ignore while user is mid-edit */ }
}

function setNestedRegex(config, key, value) {
    const layout = config.reportLayout ?? config.ReportLayout;
    if (!layout) return;
    layout[key] = value ? { ...(layout[key] ?? {}), regex: value } : null;
}

function renderFundsTable(funds) {
    const panel = document.getElementById('patterns-panel');
    const tbody = document.getElementById('patterns-tbody');
    if (!funds.length) { panel.hidden = true; return; }
    panel.hidden = false;
    tbody.innerHTML = funds.map((f, i) => {
        const sl = f.scheduleLocator ?? f.ScheduleLocator;
        const namePat = f.fundNamePattern ?? f.FundNamePattern ?? null;
        const startPat  = sl?.startPattern ?? sl?.StartPattern ?? null;
        const endPat    = sl?.terminationPattern ?? sl?.TerminationPattern ?? null;
        return `
    <tr class="fund-row" data-detail="fund-detail-${i}">
      <td class="fund-toggle-col">
        <button class="fund-expand-btn" aria-label="Expand">
          <i class="ti ti-chevron-right" aria-hidden="true"></i>
        </button>
      </td>
      <td>${esc(f.fundId ?? f.FundId ?? '—')}</td>
      <td>${esc(f.displayName ?? f.DisplayName ?? '—')}</td>
    </tr>
    <tr class="fund-detail-row" id="fund-detail-${i}" hidden>
      <td></td>
      <td colspan="2">
        <div class="fund-detail regex-editor">
          ${fundPatternRow(i, 'fundNamePattern', 'Name Pattern', namePat,
              'Text used to identify this fund by name in the report — matches the fund name as it appears in headers or titles.')}
          ${fundPatternRow(i, 'startPattern', 'Start Pattern', startPat,
              'Text that marks the beginning of this fund\'s list of holdings in the report.')}
          ${fundPatternRow(i, 'terminationPattern', 'End Pattern', endPat,
              'Text that marks the end of this fund\'s holdings — typically a total or summary row that appears after the last holding.')}
        </div>
      </td>
    </tr>`;
    }).join('');

    tbody.querySelectorAll('.fund-row').forEach(row =>
        row.addEventListener('click', () => {
            const detail = document.getElementById(row.dataset.detail);
            const btn    = row.querySelector('.fund-expand-btn');
            const open   = detail.hidden;
            detail.hidden = !open;
            btn.classList.toggle('expanded', open);
        })
    );
    tbody.querySelectorAll('.fund-detail .pattern-edit-btn').forEach(btn =>
        btn.addEventListener('click', () => {
            const cell = btn.closest('.pattern-cell');
            cell.querySelector('.pattern-view').hidden = true;
            cell.querySelector('.pattern-edit').hidden = false;
            cell.querySelector('.regex-input').focus();
        })
    );
    tbody.querySelectorAll('.fund-detail .pattern-done-btn').forEach(btn =>
        btn.addEventListener('click', () => {
            const cell = btn.closest('.pattern-cell');
            cell.querySelector('.pattern-view').hidden = false;
            cell.querySelector('.pattern-edit').hidden = true;
        })
    );
    tbody.querySelectorAll('.fund-detail .regex-input').forEach(input =>
        input.addEventListener('input', () =>
            syncFundRegexToEditor(+input.dataset.fund, input.dataset.key, input.value)
        )
    );
}

function fundPatternRow(fundIdx, key, label, pattern, tooltip = null) {
    const labelHtml = tooltip
        ? `${esc(label)}<span class="field-hint" data-tooltip="${esc(tooltip)}"><i class="ti ti-info-circle" aria-hidden="true"></i></span>`
        : esc(label);
    if (!pattern) return `
    <div class="regex-row" data-fund="${fundIdx}" data-key="${key}">
        <div class="regex-label">${labelHtml}</div>
        <span class="pattern-empty">—</span>
    </div>`;
    const viewText = pattern.example || pattern.regex || '';
    return `
    <div class="regex-row" data-fund="${fundIdx}" data-key="${key}">
        <div class="regex-label">${labelHtml}</div>
        <div class="pattern-cell">
            <div class="pattern-view">
                <button class="pattern-edit-btn" aria-label="Edit regex"><i class="ti ti-pencil" aria-hidden="true"></i></button>
                <div class="pattern-view-body">
                    <div class="pattern-example">${esc(viewText)}</div>
                </div>
            </div>
            <div class="pattern-edit" hidden>
                <button class="pattern-done-btn" aria-label="Done"><i class="ti ti-check" aria-hidden="true"></i></button>
                <div class="regex-input-wrap">
                    <span class="regex-delimiter">/</span>
                    <input class="regex-input" type="text"
                           data-fund="${fundIdx}" data-key="${key}"
                           value="${esc(pattern.regex ?? '')}" placeholder="(no pattern)" />
                    <span class="regex-delimiter">/</span>
                </div>
            </div>
        </div>
    </div>`;
}

function syncFundRegexToEditor(fundIdx, key, value) {
    try {
        const config = JSON.parse(document.getElementById('config-editor').value);
        const funds  = config.Funds ?? config.funds ?? [];
        const fund   = funds[fundIdx];
        if (!fund) return;
        if (key === 'fundNamePattern') {
            const pascal = 'FundNamePattern' in fund;
            const prop   = pascal ? 'FundNamePattern' : 'fundNamePattern';
            const rKey   = pascal ? 'Regex' : 'regex';
            fund[prop]   = value ? { ...(fund[prop] ?? {}), [rKey]: value } : null;
        } else {
            const sl = fund.ScheduleLocator ?? fund.scheduleLocator;
            if (!sl) return;
            const pascal = 'StartPattern' in sl || 'TerminationPattern' in sl;
            const prop   = key === 'startPattern'
                ? (pascal ? 'StartPattern' : 'startPattern')
                : (pascal ? 'TerminationPattern' : 'terminationPattern');
            const rKey   = pascal ? 'Regex' : 'regex';
            sl[prop]     = value ? { ...(sl[prop] ?? {}), [rKey]: value } : null;
        }
        document.getElementById('config-editor').value = JSON.stringify(config, null, 2);
    } catch { /* ignore mid-edit */ }
}

// Save / onboard config
document.getElementById('btn-save-config').addEventListener('click', async () => {
    let parsed;
    try {
        parsed = JSON.parse(document.getElementById('config-editor').value);
    } catch {
        return alert('Config JSON is invalid — fix any errors before saving.');
    }
    if (!parsed?.reportId) return alert('No config loaded.');
    try {
        await apiFetch('/api/onboarding/save-config', { method: 'POST', body: JSON.stringify(parsed),
            headers: { 'Content-Type': 'application/json' } });
        const displayName = parsed.displayName ?? parsed.DisplayName
            ?? parsed.funds?.[0]?.displayName ?? parsed.Funds?.[0]?.DisplayName
            ?? parsed.reportId ?? parsed.ReportId;
        if (_freshConfig) {
            clearConfigEditor();
            resetOnboardingPane();
            await Promise.all([loadConfigReports(), loadExtractReports(), loadDashboard()]);
            showTab('dashboard');
            showDashboardBanner(`${displayName} onboarded successfully.`);
        } else {
            showConfigBanner(`${displayName} updated successfully.`);
        }
    } catch (e) {
        alert('Save failed: ' + e.message);
    }
});


// ── Helpers ──────────────────────────────────────────────────
async function apiFetch(path, options = {}) {
    const res = await fetch(API + path, options);
    if (!res.ok) throw new Error(`${res.status} ${res.statusText}`);
    return res.json();
}

function esc(str) {
    if (str == null) return '';
    return String(str)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;')
        .replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

function badge(status) {
    const map = { Clean: 'badge-ok', Warnings: 'badge-warn', Failed: 'badge-fail', Onboarding: 'badge-new' };
    const cls = map[status] ?? 'badge-new';
    return `<span class="badge ${cls}">${esc(status ?? 'Unknown')}</span>`;
}

function delay(ms) { return new Promise(r => setTimeout(r, ms)); }

let _bannerTimer = null;
function showDashboardBanner(message) {
    const banner = document.getElementById('dashboard-banner');
    document.getElementById('dashboard-banner-text').textContent = message;
    banner.hidden = false;
    document.querySelector('#tab-dashboard .content').scrollTop = 0;
    if (_bannerTimer) clearTimeout(_bannerTimer);
    _bannerTimer = setTimeout(() => { banner.hidden = true; }, 5000);
}

let _configBannerTimer = null;
function showConfigBanner(message) {
    const banner = document.getElementById('config-banner');
    document.getElementById('config-banner-text').textContent = message;
    banner.hidden = false;
    document.querySelector('#tab-config .content').scrollTop = 0;
    if (_configBannerTimer) clearTimeout(_configBannerTimer);
    _configBannerTimer = setTimeout(() => { banner.hidden = true; }, 5000);
}

// ── Init ─────────────────────────────────────────────────────
loadDashboard();
