// Generates an editable Excalidraw scene and an SVG preview for the target
// school Finance operating flow. No application runtime depends on this file.
const fs = require('node:fs');
const path = require('node:path');

const OUT = __dirname;
const W = 7620;
const H = 5400;
const NOW = Date.UTC(2026, 8, 29, 12, 0, 0);
const C = {
  ink: '#183153', muted: '#52677e', line: '#b9c6d3', page: '#fffaf5',
  purple: '#7048c9', purpleFill: '#f2edff',
  blue: '#1971c2', blueFill: '#eaf4ff',
  green: '#2f9e44', greenFill: '#e9f7ec',
  amber: '#b7791f', amberFill: '#fff7d6',
  orange: '#e67700', orangeFill: '#fff0dc',
};
const elements = [];
let serial = 0;
function core(type, x, y, width, height, strokeColor, backgroundColor, strokeWidth = 2) {
  const n = ++serial;
  return {
    id: `detailed_finance_${String(n).padStart(4, '0')}`, type, x, y, width, height,
    angle: 0, strokeColor, backgroundColor, fillStyle: 'solid', strokeWidth,
    strokeStyle: 'solid', roughness: 0, opacity: 100, groupIds: [], frameId: null,
    roundness: type === 'rectangle' ? { type: 3 } : null,
    seed: 100000 + n * 37, version: 1, versionNonce: 200000 + n * 53,
    isDeleted: false, boundElements: null, updated: NOW, created: NOW,
    link: null, locked: false,
  };
}
function rect(x, y, width, height, fill, stroke = C.line, strokeWidth = 2) {
  elements.push(core('rectangle', x, y, width, height, stroke, fill, strokeWidth));
}
function txt(x, y, width, value, size = 21, color = C.ink) {
  const height = Math.ceil(value.split('\n').length * size * 1.32);
  elements.push({
    ...core('text', x, y, width, height, color, 'transparent'),
    text: value, originalText: value, rawText: value, fontSize: size,
    fontFamily: 2, textAlign: 'left', verticalAlign: 'top',
    containerId: null, autoResize: false, lineHeight: 1.25,
  });
}
function arrow(x1, y1, x2, y2, color = C.muted) {
  elements.push({
    ...core('arrow', x1, y1, x2 - x1, y2 - y1, color, 'transparent', 3),
    points: [[0, 0], [x2 - x1, y2 - y1]], lastCommittedPoint: null,
    startBinding: null, endBinding: null, startArrowhead: null,
    endArrowhead: 'arrow', elbowed: false,
  });
}
function card(x, y, width, height, title, lines, accent, fill) {
  rect(x, y, width, height, fill, accent);
  txt(x + 22, y + 17, width - 44, title, 29, accent);
  txt(x + 22, y + 64, width - 44, lines.join('\n'), 21, C.ink);
}
function section(x, width, title, sub, accent) {
  rect(x, 245, width, 4420, '#ffffff', C.line);
  txt(x + 26, 265, width - 52, title, 34, accent);
  txt(x + 26, 310, width - 52, sub, 19, C.muted);
}

txt(78, 42, 7000, 'School Finance | detailed target workflow and form contracts', 49, C.ink);
txt(80, 110, 7200,
  'Editable team map. Fields below are the recommended target contract: retain current working screens; add the missing fields and controls.',
  22, C.muted);
rect(80, 160, 7460, 64, '#ffffff', C.line);
txt(105, 177, 7380,
  'Setup  ->  Assign structure  ->  Generate demand  ->  Student Account  ->  Collect / reduce / adjust / refund  ->  Auto-post  ->  Reconcile  ->  Close',
  24, C.ink);

const cols = [
  { x: 80, w: 1760, accent: C.purple, fill: C.purpleFill,
    title: '01  SETUP AND SETTINGS', sub: 'Administrator configures once; effective versions are pinned.' },
  { x: 1880, w: 1760, accent: C.blue, fill: C.blueFill,
    title: '02  FEES AND STUDENT WORK', sub: 'Finance officer enters business facts; optional paths stay optional.' },
  { x: 3680, w: 1760, accent: C.green, fill: C.greenFill,
    title: '03  ACCOUNTS AND CONTROL', sub: 'Accountant reviews postings, settlement, reports and close.' },
  { x: 5480, w: 2060, accent: C.amber, fill: C.amberFill,
    title: '04  SCHOOL-DAY EXAMPLES', sub: 'Amounts are illustrative; journals are system-generated.' },
];
cols.forEach(c => section(c.x, c.w, c.title, c.sub, c.accent));

const setup = [
  ['Account Heads', [
    'FIELDS  Code*, name*, type (asset/liability/income/expense)*, parent,',
    'group/posting, control-account flag, reconciliation flag, currency,',
    'effective from/to, active status.',
    'RULE  Unique code per school; used heads cannot be deleted/retyped.',
    'ACTION  Save draft, activate, retire, view dependencies and ledger.',
    'API  account-heads; dependencies; account ledger.'
  ]],
  ['Bank Accounts', [
    'FIELDS  Bank name*, account holder*, masked account number*, IFSC*,',
    'branch, linked asset head*, reconciliation enabled, active status.',
    'RULE  One asset mapping; mask details by permission; no deletion if used.',
    'ACTION  Create/edit, deactivate, view dependencies and bank book.',
    'API  bank-accounts; dependencies; sensitive-detail reveal.'
  ]],
  ['Payment Modes', [
    'FIELDS  Mode*, initial account*, reference required?, verification?',
    'settlement required?, allowed for collection/income/payout?, dates, status.',
    'RULE  Cash -> cash head; UPI/cheque/card -> clearing until settled.',
    'ACTION  Activate/deactivate with effective version.',
    'API  payment-modes GET/POST/PATCH.'
  ]],
  ['Income and Expense Categories', [
    'FIELDS  Code*, name*, kind (income/expense)*, default GL head*,',
    'description, approval threshold, effective dates, active status.',
    'RULE  Used category mapping is superseded, never changed historically.',
    'ACTION  Save, activate, retire; show downstream usage.',
    'API  income-categories; expense-categories.'
  ]],
  ['Fee Categories and Fee Heads', [
    'FIELDS  Category/name/code*, head name/code*, revenue/deposit/advance/',
    'pass-through class*, linked GL head*, refundable?, tax treatment if used,',
    'academic-year scope, effective dates, active status.',
    'RULE  Refundable security deposit maps to liability, not fee income.',
    'ACTION  Save, preview accounting, activate/retire.',
    'API  fee-categories; fee-heads; accounting-preview.'
  ]],
  ['Payment Terms and Installments', [
    'FIELDS  Term name*, schedule type*, installment number/date or offset*,',
    'percentage/fixed amount*, grace days, late-fee rule, rounding, status.',
    'RULE  Shares total 100% or exact amount; due dates ordered/non-overlap.',
    'ACTION  Preview schedule, publish version, retire.',
    'API  payment-terms; schedule preview.'
  ]],
  ['Fee Structure', [
    'FIELDS  Name*, school/academic year*, class/grade*, fee-head rows*',
    '(amount, frequency, term), effective dates, policy versions.',
    'RULE  Published version immutable; no missing account mapping.',
    'ACTION  Preview amounts/installments, publish, supersede.',
    'API  fee-structures; schedule/readiness preview.'
  ]],
  ['Collection and Concession Policies', [
    'COLLECTION  Grace/late fee, rounding, head scope, partial/split/advance',
    'rules, allocation priority, provisional instruments, reversal window.',
    'CONCESSION  Eligibility, head scope, percent/cap, evidence, threshold,',
    'effective date, before/after demand and after-payment behavior.',
    'RULE  Request pins policy version; approval threshold enforced.',
    'ACTION  Simulate, publish version, retire.'
  ]],
  ['Refund and Proration Policies', [
    'REFUND  Eligible source/head, paid charge/late fee, partial cap,',
    'deductions, payout modes, approver, evidence and reversal window.',
    'PRORATION  Head, cutoff, minimum days, rounding, after-cutoff rule.',
    'RULE  After demand, create a traceable reduction/reversal; no overwrite.',
    'ACTION  Simulate, publish version, retire.'
  ]],
  ['Accounting Mappings', [
    'FIELDS  Event type*, debit source*, credit source*, conditions,',
    'effective from/to*, version*, status Draft/Active/Retired.',
    'RULE  Fixed supported event templates; active used version immutable.',
    'ACTION  Simulate amount/account resolution and balancing; activate.',
    'API  accounting-mappings; simulate; activate; retire.'
  ]],
  ['Periods, Opening Balances, Readiness', [
    'PERIOD  Financial year, month start/end, Open/Closed, closed by/at,',
    'reopen reason and approval. Backdating allowed only into open period.',
    'OPENING  As-of date, account/debit/credit rows, source, balancing fund.',
    'READINESS  Missing mappings, inactive heads, invalid terms, closed',
    'period, incomplete bank/payment configuration.',
    'API  accounting-periods; opening-balances; setup/readiness.'
  ]],
];

const fees = [
  ['Fee Structure Assignment', [
    'FIELDS  Student or cohort*, academic year*, structure version*,',
    'effective date*, pinned term and policy versions, exclusion reason.',
    'RULE  Enrollment eligible; avoid overlapping active assignments.',
    'ACTION  Preview roster/amounts, confirm with token and fingerprint.',
    'STATUS  Previewed -> Assigned -> Superseded/Cancelled.',
    'API  assignment preview and commit.'
  ]],
  ['Fee Demands and Installments', [
    'FIELDS  Student/cohort*, installment/period*, due date*, fee-head lines,',
    'amounts, demand number generated, notes and cancellation reason.',
    'RULE  Idempotent by assignment/version/installment; no duplicate bill.',
    'ACTION  Preview, generate, cancel/reinstate with linked reversal.',
    'STATUS  Scheduled -> Generated -> Partial -> Paid; Cancelled/Reversed.',
    'API  fee-demands preview/generate/detail/cancel.'
  ]],
  ['Student Financial Account', [
    'FILTERS  Student*, year, date range, source type and status.',
    'SHOW  Demand, charge, allocation, concession, waiver, adjustment,',
    'advance, refund, reversal, running due/credit, receipt and journal.',
    'RULE  Student subledger totals reconcile to GL control accounts.',
    'ACTION  Open source/receipt/audit; start valid operation.',
    'API  students/{id}/financial-account and /credits.'
  ]],
  ['Charges', [
    'FIELDS  Student*, charge category/head*, amount*, effective/due date,',
    'reason*, evidence, approval if threshold crossed.',
    'RULE  Cannot add duplicate source charge or negative charge.',
    'ACTION  Preview, assess, reverse with reason.',
    'STATUS  Draft -> Approved -> Posted -> Reversed.',
    'POST  Dr Student Receivable / Cr Charge Income.'
  ]],
  ['Concession Request', [
    'FIELDS  Student*, policy/type*, fee head/installment scope*, amount',
    'or percent*, reason*, evidence, effective date, requested by.',
    'RULE  Within cap; no duplicated benefit; paid demand creates credit/',
    'refund payable rather than negative outstanding.',
    'ACTION  Preview, submit, approve/reject, apply, reverse.',
    'API  concessions approval-preview/approve/reverse.'
  ]],
  ['Collections and Receipts', [
    'FIELDS  Student*, open demand/head allocations*, payment date*,',
    'instrument rows (mode, amount, bank, reference, evidence),',
    'unallocated/advance choice, remarks and receipt book.',
    'RULE  Sum instruments = received; allocations <= eligible due; one',
    'idempotency key; receipt number server-generated.',
    'ACTION  Preview -> Confirm -> Receipt; reverse by linked entry.',
    'API  fee-collections preview/create/reverse.'
  ]],
  ['Payment Verification', [
    'FIELDS  Instrument*, provider/cheque reference, amount, initiated',
    'date, settlement evidence/date, verifier decision and reason.',
    'RULE  Operates per instrument, not entire split receipt; cash skips.',
    'ACTION  Verify, reject, mark failed; preserve other instruments.',
    'STATUS  Pending -> Verified/Failed/Rejected -> Reversed.',
    'API  payment-verifications/{instrumentId}/approve|reject|mark-failed.'
  ]],
  ['Waiver in Collections', [
    'FIELDS  Eligible charge/late-fee row*, mandatory reason*, evidence',
    'and approval if above policy threshold. Amount is server-calculated.',
    'RULE  Only current unpaid eligible balance; not a cash transaction.',
    'ACTION  Preview, waive, reverse with full audit.',
    'POST  Dr Waiver / Cr Student Receivable.',
    'API  waivers/preview; waivers/apply; waivers/{id}/reverse.'
  ]],
  ['Adjustments', [
    'FIELDS  Student*, genuine credit source* (advance/approved credit),',
    'target demand/head*, amount*, effective date, reason and evidence.',
    'RULE  Unpaid due is not available credit; amount <= source and target.',
    'ACTION  Preview, submit/approve, apply atomically, reverse.',
    'STATUS  Draft -> Approved -> Applied -> Reversed.',
    'API  adjustment-credits/targets; adjustments/preview/apply.'
  ]],
  ['Refunds', [
    'FIELDS  Student*, original credit/payment source*, requested amount*,',
    'reason*, evidence, policy/version, deductions, approver; payout bank*,',
    'method*, date*, reference*, failure reason if needed.',
    'RULE  Reserve refundable balance; partial permitted per policy;',
    'fee refund/advance/deposit have different accounting.',
    'ACTION  Request -> Review -> Approve -> Process/Fail -> Reverse.',
    'API  refunds accounting-preview/process/mark-failed/reverse.'
  ]],
  ['Receipts, Cancellation and Reversal', [
    'RECEIPT  Number, student, allocations, instruments, verification state,',
    'issued/voided by, download and source links. No editable amount.',
    'CANCEL  Paid demand cannot be silently removed; show impact preview.',
    'REVERSE  Original source/journal remains; linked contra-entry records',
    'reason, actor, time and reopened receivable/advance.',
    'API  source-specific reverse-preview and reverse.'
  ]],
];

const accounts = [
  ['Other Income (not school fees)', [
    'FIELDS  Category*, payer/source*, amount*, transaction date*, mode*,',
    'cash/bank/receivable destination*, reference, description, evidence.',
    'RULE  Fee receipt cannot be entered again as Income; period open.',
    'ACTION  Preview, save draft, submit, approve, post, reverse.',
    'STATUS  Draft -> Submitted -> Approved -> Posted -> Reversed.',
    'API  incomes/preview/create/submit/approve/post/reverse.'
  ]],
  ['Expenses and Vendor Payable', [
    'FIELDS  Category*, vendor*, invoice no/date*, amount*, tax if used,',
    'description*, due date, evidence; Already paid? Yes/No.',
    'IF YES  Mode*, cash/bank*, paid date*, reference* where required.',
    'IF NO  Create vendor payable; later choose bank/mode/ref/date.',
    'RULE  Duplicate vendor invoice blocked; refund is not an expense.',
    'API  expenses/preview/create/submit/approve/pay/reverse.'
  ]],
  ['Bank Reconciliation', [
    'FIELDS  Bank account*, statement period*, opening/closing balance,',
    'import file*, statement line date/amount/reference/description.',
    'RULE  Detect duplicate file/lines; match/partial-match to bank book;',
    'unresolved lines stay visible; matching never edits original receipt.',
    'ACTION  Import -> Suggest -> Confirm match -> Review -> Close.',
    'API  bank-statements/imports; candidates; reconciliation-matches.'
  ]],
  ['Journals and General Ledger', [
    'FILTERS  Period, account, source module/id, posting state, date range.',
    'SHOW  Entry no/date, debit/credit lines, source, mapping version, actor,',
    'reversal link, account running balance and drill-through evidence.',
    'RULE  Routine fee/income/expense actions post automatically.',
    'ACTION  View/export; exceptional manual journal restricted.',
    'API  journals/list/detail/reverse; ledgers/accounts/{id}.'
  ]],
  ['Posting Exceptions', [
    'FIELDS  Source event/id, school, amount, reason code, mapping/period,',
    'retry count, owner, created/resolved at and resolution evidence.',
    'RULE  No silent partial posting or duplicate journal on retry.',
    'ACTION  Fix mapping/account/period, retry safely, escalate.',
    'STATUS  Open -> Retrying -> Resolved/Escalated.',
    'API  posting-exceptions/list/detail/retry.'
  ]],
  ['Accounting Period Close', [
    'FIELDS  Financial year/month*, start/end, close date, checker, note;',
    'reopen action needs reason*, approval and audit.',
    'BLOCKERS  Unposted events, exceptions, pending verification,',
    'unreconciled lines, draft journals and high-risk approvals.',
    'ACTION  Close preview, close, audited reopen.',
    'API  accounting-periods/generate/close-preview/close/reopen.'
  ]],
  ['Reports and Daily Control', [
    'FILTERS  School, year/period, account/head, class, mode, bank, status.',
    'REPORTS  Collection, outstanding/aging, refunds/concessions, bank',
    'book, cash book, trial balance, income-expense, P&L, balance sheet.',
    'RULE  Student due = receivable control; debits = credits; clearing',
    'and statement differences have named exceptions.',
    'ACTION  Drill down, export with permissions.'
  ]],
  ['Permissions and Audit', [
    'ROLES  Officer records; approver authorizes; accountant closes;',
    'auditor reads; admin configures. Enforce tenant/year scope server-side.',
    'AUDIT  Who/when/from-to/reason/evidence/policy/mapping version,',
    'idempotency key, source document and journal/reversal links.',
    'RULE  No self-approval above limit; sensitive bank details masked.',
    'ACTION  Filter/export audit trail; no destructive posted edits.'
  ]],
];

const examples = [
  ['1  Aarav | Grade 8 fee demand', [
    'Fee Structure: tuition INR 12,000 + transport INR 3,000.',
    'Assignment pins term/policy versions; demand total INR 15,000.',
    'POST  Dr Student Receivable 15,000; Cr Tuition Income 12,000;',
    'Cr Transport Income 3,000. Student due = INR 15,000.'
  ]],
  ['2  Merit concession before payment', [
    'Approved concession INR 1,000 against tuition.',
    'POST  Dr Fee Concession 1,000; Cr Student Receivable 1,000.',
    'Student due = INR 14,000. Original demand stays visible.'
  ]],
  ['3  Split receipt: cash plus UPI', [
    'Aarav pays INR 4,000 cash + INR 6,000 UPI.',
    'POST  Dr Cash 4,000; Dr UPI Clearing 6,000;',
    'Cr Student Receivable 10,000. Student due = INR 4,000.',
    'UPI instrument alone awaits verification/settlement.'
  ]],
  ['4  UPI reaches the school bank', [
    'Provider settlement INR 6,000 matches the UPI instrument.',
    'POST  Dr School Bank 6,000; Cr UPI Clearing 6,000.',
    'Bank reconciliation matches statement line; clearing = zero.'
  ]],
  ['5  Final payment exceeds due', [
    'Aarav pays INR 5,000 by bank; due was INR 4,000.',
    'POST  Dr School Bank 5,000; Cr Receivable 4,000;',
    'Cr Student Advance 1,000. Due = zero; advance = INR 1,000.'
  ]],
  ['6  Parent asks for advance refund', [
    'Approved payout INR 1,000; advance is reserved before payment.',
    'POST  Dr Student Advance 1,000; Cr School Bank 1,000.',
    'This is a liability payout, NOT an ordinary school expense.'
  ]],
  ['7  Meera | credit adjustment + waiver', [
    'Meera owes transport INR 3,000; prior approved advance INR 2,000.',
    'ADJUST  Dr Student Advance 2,000; Cr Receivable 2,000.',
    'Due = INR 1,000. Late charge 500 is then fully waived:',
    'Dr Waiver 500; Cr Receivable 500. Charge/waiver stay auditable.'
  ]],
  ['8  Paid fee later refunded', [
    'Approved return of recognized tuition INR 2,000.',
    'APPROVE  Dr Fee Refund 2,000; Cr Refund Payable 2,000.',
    'PAYOUT  Dr Refund Payable 2,000; Cr Bank 2,000.',
    'Not an Expenses voucher; source and payout reconcile.'
  ]],
  ['9  Non-fee income and true expense', [
    'School receives donation INR 10,000:',
    'Dr Bank 10,000; Cr Donation Income 10,000.',
    'School pays electricity bill INR 25,000:',
    'Dr Electricity Expense 25,000; Cr Bank 25,000.'
  ]],
  ['10  End-of-day and month-end checks', [
    'Aarav: due 0; advance 0; UPI clearing 0; net fee income 14,000.',
    'Cash + bank receipts net of Aarav refund = INR 14,000.',
    'Trial balance debits = credits; bank lines matched.',
    'Unresolved exceptions block close; reopening requires reason.'
  ]],
];

function drawColumn(data, column, cardHeight) {
  const x = column.x + 26;
  const width = column.w - 52;
  let y = 362;
  data.forEach(([title, lines], i) => {
    card(x, y, width, cardHeight, title, lines, column.accent, column.fill);
    if (i < data.length - 1) {
      // Down arrows indicate reading order, not mandatory execution of every action.
      arrow(x + width / 2, y + cardHeight + 4, x + width / 2, y + cardHeight + 19, column.accent);
    }
    y += cardHeight + 28;
  });
  return y;
}
drawColumn(setup, cols[0], 360);
drawColumn(fees, cols[1], 360);
drawColumn(accounts, cols[2], 360);
drawColumn(examples, cols[3], 360);

rect(80, 4785, 7460, 540, '#ffffff', C.line);
txt(108, 4805, 7200, '05  BACKEND CONTRACT | every operation, without asking the finance officer to write journals', 34, C.ink);
const controlX = [108, 1960, 3812, 5664];
const controlW = 1780;
const controls = [
  ['Command and validation', [
    'Authenticate school/year and permission.',
    'Validate status transition, policy version,',
    'effective date, evidence, open period,',
    'amount and account readiness.',
    'Check idempotency key + row version +',
    'backend preview fingerprint.'
  ]],
  ['Atomic automatic posting', [
    'Save business transition and Financial Event.',
    'Resolve active event-to-account mapping.',
    'Create balanced Journal and lines.',
    'Update rebuildable student/GL projections.',
    'Commit in one database transaction.',
    'Publish outbox work after commit.'
  ]],
  ['Failure, reversal, concurrency', [
    'Unique source-event and journal constraints.',
    'Lock affected student credit/receivable rows.',
    'Retry returns the same result, never reposts.',
    'Failed posting -> visible exception.',
    'Posted source is never deleted or edited;',
    'reverse with linked contra-entry.'
  ]],
  ['Control totals and close', [
    'Student subledger = receivable/advance GL.',
    'Debits = credits per journal and trial balance.',
    'Bank book = matched statement + exceptions.',
    'Clearing balances explain unsettled instruments.',
    'Period close blocks unresolved high-risk work.',
    'Audit stores actor, reason, source and version.'
  ]],
];
controls.forEach(([title, lines], i) => card(controlX[i], 4875, controlW, 365,
  title, lines, [C.orange, C.green, C.blue, C.purple][i],
  [C.orangeFill, C.greenFill, C.blueFill, C.purpleFill][i]));
txt(108, 5270, 7000,
  'Important: the arrows between cards show reading order. Charges, concessions, waiver, adjustments and refunds are optional business paths, not mandatory steps.',
  20, C.muted);

const scene = {
  type: 'excalidraw', version: 2, source: 'https://excalidraw.com', elements,
  appState: { gridSize: null, viewBackgroundColor: C.page, zoom: { value: 1 },
    scrollX: 0, scrollY: 0, currentItemStrokeColor: C.ink,
    currentItemBackgroundColor: 'transparent' }, files: {},
};
const excalidrawPath = path.join(OUT, 'school-finance-management-detailed.excalidraw');
fs.writeFileSync(excalidrawPath, JSON.stringify(scene, null, 2) + '\n');

function esc(s) { return String(s).replace(/[&<>"']/g, ch => ({
  '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&apos;',
}[ch])); }
const svg = [`<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}" role="img" aria-label="Detailed school finance management flow">`,
  '<defs><marker id="arrow" markerWidth="12" markerHeight="12" refX="10" refY="6" orient="auto"><path d="M 1 1 L 11 6 L 1 11" fill="none" stroke="#52677e" stroke-width="2"/></marker></defs>',
  `<rect width="${W}" height="${H}" fill="${C.page}"/>`];
for (const e of elements) {
  if (e.type === 'rectangle') {
    svg.push(`<rect x="${e.x}" y="${e.y}" width="${e.width}" height="${e.height}" rx="18" fill="${e.backgroundColor}" stroke="${e.strokeColor}" stroke-width="${e.strokeWidth}"/>`);
  } else if (e.type === 'arrow') {
    const p = e.points.map(([dx, dy]) => `${e.x + dx},${e.y + dy}`).join(' ');
    svg.push(`<polyline points="${p}" fill="none" stroke="${e.strokeColor}" stroke-width="${e.strokeWidth}" marker-end="url(#arrow)"/>`);
  } else if (e.type === 'text') {
    const lines = e.text.split('\n');
    svg.push(`<text x="${e.x}" y="${e.y + e.fontSize}" fill="${e.strokeColor}" font-family="Arial, sans-serif" font-size="${e.fontSize}" font-weight="${e.fontSize >= 28 ? 700 : 400}">`);
    lines.forEach((line, i) => svg.push(`<tspan x="${e.x}" dy="${i ? e.fontSize * 1.25 : 0}">${esc(line)}</tspan>`));
    svg.push('</text>');
  }
}
svg.push('</svg>');
fs.writeFileSync(path.join(OUT, 'school-finance-management-detailed.svg'), svg.join('\n') + '\n');
console.log(`Generated ${elements.length} editable elements at ${excalidrawPath}`);
