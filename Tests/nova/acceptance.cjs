// Evidence classification only. Model declarations never constitute acceptance.
function authoredContentRequested(...plans) {
  return plans.some(plan => plan?.intentions?.some(operation => ['CREATE_DRAFT', 'PROPOSE_ADAPTATION'].includes(operation)) === true);
}
function caseAcceptance(controlCorrect, { questionReview = false, authoredContent = false, independentReview = null } = {}) {
  if (!controlCorrect || independentReview === 'FAIL') return false;
  return (questionReview || authoredContent) && independentReview !== 'PASS' ? null : true;
}

function campaignAcceptance(results, errors, expectedCases) {
  const key = item => JSON.stringify([item.scenario, item.index]);
  const expected = new Set(expectedCases.map(key));
  const actual = results.map(key);
  const complete = expected.size === expectedCases.length && actual.length === expected.size
    && new Set(actual).size === actual.length && actual.every(item => expected.has(item));
  const failed = errors.length > 0 || !complete || results.some(item => item.correct === false);
  const status = failed ? 'FAIL' : results.some(item => item.correct !== true) ? 'NOT_TESTED' : 'PASS';
  return { status, exitCode: status === 'PASS' ? 0 : status === 'FAIL' ? 2 : 3, complete };
}

module.exports = { authoredContentRequested, caseAcceptance, campaignAcceptance };
