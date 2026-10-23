const { test } = require('node:test');
const assert = require('node:assert/strict');
const { authoredContentRequested, caseAcceptance, campaignAcceptance } = require('./acceptance.cjs');
const expected = [{ scenario: 'fill', index: 1 }, { scenario: 'create', index: 1 }];
const rows = values => expected.map((item, index) => ({ ...item, correct: values[index] }));
test('a later save cannot erase authorship review required by earlier task turns', () => {
  const save = { intentions: ['REQUEST_SAVE_COPY'] }, read = { intentions: ['OPEN_PROMPT'] };
  for (const operation of ['CREATE_DRAFT', 'PROPOSE_ADAPTATION']) {
    const authoredContent = authoredContentRequested(save, { intentions: [operation] });
    assert.equal(authoredContent, true);
    assert.equal(caseAcceptance(true, { authoredContent }), null);
    assert.equal(caseAcceptance(true, { authoredContent, independentReview: 'FAIL' }), false);
  }
  assert.equal(authoredContentRequested(save, read, null), false);
});

test('observed fields can pass; generated content and free clarification require independent review', () => {
  assert.equal(caseAcceptance(true), true);
  assert.equal(caseAcceptance(true, { authoredContent: true }), null);
  assert.equal(caseAcceptance(true, { questionReview: true }), null);
  assert.equal(caseAcceptance(false, { authoredContent: true }), false);
});
test('a completed process and model COMPLETED status cannot conceal acceptance failure', () => {
  const result = campaignAcceptance(rows([true, false]).map(item => ({ ...item, status: 'COMPLETED' })), [], expected);
  assert.equal(result.status, 'FAIL'); assert.equal(result.exitCode, 2);
});
test('unreviewed semantic content cannot close a campaign', () => {
  const result = campaignAcceptance(rows([true, null]), [], expected);
  assert.equal(result.status, 'NOT_TESTED'); assert.equal(result.exitCode, 3);
});
test('independent content rejection overrides a successful UI; review never overrides a failed UI', () => {
  assert.equal(caseAcceptance(true, { authoredContent: true, independentReview: 'FAIL' }), false);
  assert.equal(caseAcceptance(true, { authoredContent: true, independentReview: 'PASS' }), true);
  assert.equal(caseAcceptance(false, { authoredContent: true, independentReview: 'PASS' }), false);
  assert.equal(caseAcceptance(true, { authoredContent: true, independentReview: 'ACCEPT' }), null);
});
test('missing, duplicate, unrelated or empty evidence cannot pass', () => {
  for (const evidence of [[], rows([true, true]).slice(0, 1), [rows([true, true])[0], rows([true, true])[0]],
    [rows([true, true])[0], { scenario: 'unrelated', index: 1, correct: true }]]) {
    assert.equal(campaignAcceptance(evidence, [], expected).status, 'FAIL');
  }
});
test('only complete, verified rows without incidents pass', () => {
  assert.deepEqual(campaignAcceptance(rows([true, true]), [], expected), { status: 'PASS', exitCode: 0, complete: true });
  assert.equal(campaignAcceptance(rows([true, true]), ['transport incident'], expected).status, 'FAIL');
});
