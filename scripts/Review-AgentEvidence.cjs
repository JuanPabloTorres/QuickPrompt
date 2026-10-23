// Compose external adjudication with observed UI evidence, never with a model's
// self-verdict. The raw campaign and failed experiments remain unchanged.
const fs = require('node:fs'), path = require('node:path'), assert = require('node:assert/strict'), { createHash } = require('node:crypto');
const { authoredContentRequested, caseAcceptance, campaignAcceptance } = require('../Tests/nova/acceptance.cjs');
const root = path.resolve(__dirname, '..');
assert.equal(process.argv.length, 3, 'Supply one owned evidence directory');
const directory = path.resolve(root, process.argv[2]);
assert.ok(directory.startsWith(path.join(root, 'docs/evidence') + path.sep), 'Only owned QA evidence');
const bytes = fs.readFileSync(path.join(directory, 'measurements.json'));
const rows = JSON.parse(bytes), raw = JSON.parse(fs.readFileSync(path.join(directory, 'validation.json'), 'utf8'));
const review = JSON.parse(fs.readFileSync(path.join(directory, 'independent-content-review.json'), 'utf8'));
assert.equal(review.measurementsSha256, createHash('sha256').update(bytes).digest('hex'), 'Review belongs to another campaign');
assert.ok(review.reviewer && typeof review.humanReview === 'boolean', 'Review attribution required');
const key = item => JSON.stringify([item.scenario, item.index]);
assert.equal(new Set(review.rows.map(key)).size, review.rows.length, 'Duplicate review');
assert.ok(review.rows.every(item => rows.some(row => key(row) === key(item))), 'Unrelated review');
const adjudicated = rows.map(row => {
  const item = review.rows.find(item => key(item) === key(row));
  const independentReview = item?.contentReview ?? null;
  assert.ok(independentReview === null || ['PASS', 'FAIL', 'NOT_APPLICABLE', 'NOT_TESTED'].includes(independentReview));
  const contentReviewRequired = row.contentAcceptance?.required === true || authoredContentRequested(row.semanticSummary,
    ...(row.precedingRuns ?? []).map(run => run.semantic), row.unsavedCreation?.semantic);
  return { scenario: row.scenario, index: row.index, controlCorrect: row.controlCorrect, independentReview, contentReviewRequired,
    correct: caseAcceptance(row.controlCorrect === true, { authoredContent: contentReviewRequired,
      questionReview: !!row.referenceQuestionReview, independentReview }) };
});
assert.equal(raw.complete, true, 'Raw evidence lacks expected-case completeness');
const acceptance = campaignAcceptance(adjudicated, raw.errors, rows);
const output = path.join(directory, 'acceptance.json');
assert.ok(!fs.existsSync(output), 'Preserve prior adjudication');
fs.writeFileSync(output, JSON.stringify({ ...acceptance, rawStatus: raw.status, reviewer: review.reviewer,
  humanReview: review.humanReview, measurementsSha256: review.measurementsSha256, rows: adjudicated }, null, 2));
console.log(JSON.stringify({ status: acceptance.status, rawStatus: raw.status, output: path.relative(root, output) }));
process.exitCode = acceptance.exitCode;
