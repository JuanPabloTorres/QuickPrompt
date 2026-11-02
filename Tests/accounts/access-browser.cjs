// Real UI/API/Identity/PostgreSQL; only the explicitly enabled private QA mailbox is read.
const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const assert = require('node:assert/strict');
const { chromium } = require('playwright');

const origin = 'http://127.0.0.1:5297';
const output = path.resolve(process.argv[2] || '.playwright/account-access-20261101/browser');
const email = `qa-access-${crypto.randomUUID()}@example.invalid`;
const password = 'Synthetic-Account!79';
const changedPassword = 'Changed-Synthetic!81';
const mailbox = path.join(process.env.LOCALAPPDATA, 'QuickPromptNova', 'saas-development', 'mail',
  crypto.createHash('sha256').update(email.toUpperCase()).digest('hex'));
const checks = [], browserErrors = [];
fs.mkdirSync(output, { recursive: true });

function mail(purpose) {
  const messages = fs.readdirSync(mailbox).map(file => {
    const target = path.join(mailbox, file);
    return { message: JSON.parse(fs.readFileSync(target, 'utf8')), time: fs.statSync(target).mtimeMs };
  }).filter(item => item.message.Email === email && item.message.Purpose === purpose).sort((a, b) => b.time - a.time);
  assert.ok(messages.length, 'A message for this synthetic mailbox must exist');
  const link = new URL(messages[0].message.Link);
  assert.equal(link.origin, origin); assert.equal(link.pathname, `/account/${purpose}`);
  return { link: link.href, count: messages.length };
}

(async () => {
  const executablePath = process.env.QUICKPROMPT_TEST_CHROMIUM;
  if (executablePath && (!path.isAbsolute(executablePath) || !fs.statSync(executablePath).isFile()))
    throw new Error('The explicit existing QA browser executable is invalid.');
  const browser = await chromium.launch({ headless: true, ...(executablePath ? { executablePath } : {}) });
  const context = await browser.newContext({ viewport: { width: 1365, height: 1000 }, serviceWorkers: 'block' });
  let step = 'require explicit local QA mail';
  try {
    const config = await context.request.get(origin + '/account/options');
    assert.equal(config.status(), 200);
    const options = await config.json();
    assert.equal(options.mailDelivery, 'development-pickup'); assert.equal(options.email, true);
    assert.equal(options.google, false); assert.equal(options.delegation, false);
    const page = await context.newPage();
    page.on('pageerror', error => browserErrors.push(error.name));
    await page.route('**/*', route => new URL(route.request().url()).origin === origin ? route.continue() : route.abort());
    const fillEmail = () => page.getByLabel('Correo electrónico', { exact: true }).fill(email);
    const fillNewPassword = async value => {
      await page.getByLabel('Contraseña', { exact: true }).fill(value);
      await page.getByLabel('Repite la contraseña', { exact: true }).fill(value);
    };
    const submit = label => page.getByRole('button', { name: label, exact: true }).click();
    step = 'register and resend';
    await page.goto(origin + '/account/register');
    await fillEmail(); await fillNewPassword(password);
    await submit('Crear cuenta'); await page.getByRole('status').filter({ hasText: 'Si corresponde' }).waitFor();
    assert.equal(mail('confirm').count, 1);
    await page.screenshot({ path: path.join(output, 'register-desktop.png'), fullPage: true });
    await submit('Reenviar correo de confirmación'); await fillEmail(); await submit('Reenviar confirmación');
    await page.getByRole('status').filter({ hasText: 'Si corresponde' }).waitFor();
    assert.equal(mail('confirm').count, 2);
    checks.push('Direct registration and explicit resend reach Identity and the private QA mailbox');

    step = 'confirm mailbox ownership';
    const confirmation = mail('confirm').link;
    const invalid = new URL(confirmation); invalid.searchParams.set('code', 'invalid-synthetic-code');
    await page.goto(invalid.href); await submit('Confirmar correo'); await page.getByRole('alert').waitFor();
    await page.goto(confirmation); await submit('Confirmar correo');
    await page.getByText('Correo confirmado. Ya puedes entrar.', { exact: true }).waitFor();
    assert.equal(new URL(page.url()).search, ''); assert.equal(new URL(page.url()).pathname, '/account/login');
    checks.push('Invalid confirmation is rejected; valid confirmation removes the URL token and returns to login');

    step = 'sign in and request recovery';
    await fillEmail(); await page.getByLabel('Contraseña', { exact: true }).fill(password); await submit('Entrar');
    await page.getByRole('heading', { name: 'Mis bibliotecas', exact: true }).waitFor();
    // Preserve a second real authenticated session to test reset revocation.
    const second = await browser.newContext();
    const csrf = await (await second.request.get(origin + '/account/csrf')).json();
    const login = await second.request.post(origin + '/account/login', { data: { email, password }, headers: { 'X-QP-CSRF': csrf.token } });
    assert.equal(login.status(), 204);
    assert.equal((await second.request.get(origin + '/account/me')).status(), 200);
    await submit('Cerrar sesión'); await submit('Olvidé mi contraseña'); await fillEmail(); await submit('Enviar recuperación');
    await page.getByRole('status').filter({ hasText: 'Si corresponde' }).waitFor();
    const reset = mail('reset').link;
    checks.push('Confirmed account signs in and requests recovery through the UI');

    step = 'reset password and reject replay';
    await page.goto(reset); await fillNewPassword(changedPassword); await submit('Guardar contraseña');
    await page.getByText('Contraseña actualizada. Ya puedes entrar.', { exact: true }).waitFor();
    assert.equal(new URL(page.url()).search, '');
    assert.equal((await second.request.get(origin + '/account/me')).status(), 401);
    await second.close();
    await page.goto(reset); await fillNewPassword(changedPassword); await submit('Guardar contraseña');
    await page.getByRole('alert').waitFor();
    checks.push('Reset changes the real Identity password, revokes another session and rejects token replay');

    step = 'verify old and new passwords';
    await submit('Volver a iniciar sesión'); await fillEmail();
    await page.getByLabel('Contraseña', { exact: true }).fill(password); await submit('Entrar');
    await page.getByRole('alert').waitFor();
    await page.getByLabel('Contraseña', { exact: true }).fill(changedPassword); await submit('Entrar');
    await page.getByRole('heading', { name: 'Mis bibliotecas', exact: true }).waitFor();
    await submit('Cerrar sesión');
    checks.push('Old password is rejected and the new password opens the private account workspace');

    step = 'check mobile entry and refresh';
    await page.setViewportSize({ width: 390, height: 844 }); await submit('Crear una cuenta'); await page.reload();
    await page.getByRole('heading', { name: 'Crea tu cuenta', exact: true }).waitFor();
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
    await page.screenshot({ path: path.join(output, 'register-mobile.png'), fullPage: true });
    assert.equal(browserErrors.length, 0);
    checks.push('Mobile registration survives reload with no horizontal overflow or page errors');
    fs.writeFileSync(path.join(output, 'result.json'), JSON.stringify({ status: 'PASS', checks,
      backend: 'REAL_LOCAL_IDENTITY_POSTGRESQL', mail: 'PRIVATE_DEVELOPMENT_PICKUP', externalMailVerified: false,
      publicDeploymentVerified: false, modelCalls: 0, browserErrors }, null, 2));
    console.log(JSON.stringify({ status: 'PASS', checks, externalMailVerified: false }));
  } catch (error) {
    // Error messages and browser URLs can contain credentials/tokens; retain only a safe stage and type.
    fs.writeFileSync(path.join(output, 'result.json'), JSON.stringify({ status: 'FAIL', step, errorType: error.name, checks }, null, 2));
    console.error(JSON.stringify({ status: 'FAIL', step, errorType: error.name })); process.exitCode = 1;
  } finally { await context.close(); await browser.close(); }
})();
