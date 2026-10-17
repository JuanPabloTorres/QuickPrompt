// Legacy regression entry point: natural commands now use the unified semantic preparation path.
// The shared harness verifies real headed UI navigation, adaptation, ambiguity and explicit saves.
if (!process.argv.includes('--canvas-only')) process.argv.push('--canvas-only');
require('./semantic-intent.cjs');
