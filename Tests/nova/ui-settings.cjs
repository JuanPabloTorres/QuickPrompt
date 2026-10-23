async function openAssistant(page) {
  const launcher = page.getByRole('button', { name: 'Abrir asistente NOVA', exact: true });
  if (await launcher.count()) await launcher.click();
}
async function configurePreparation(page, { mode, speed, remember } = {}) {
  await page.getByRole('button', { name: 'Configuración', exact: true }).click();
  const settings = page.getByRole('dialog', { name: 'Configuración', exact: true });
  await settings.waitFor();
  if (mode) await settings.getByLabel('Presentación del resultado', { exact: true }).selectOption(mode);
  if (speed) await settings.getByLabel('Velocidad de escritura', { exact: true }).selectOption(speed);
  if (remember !== undefined) await settings.getByLabel('Conservar borradores y respuestas del modo «Solo mostrar el resultado» en este equipo', { exact: true }).setChecked(remember);
  await settings.getByRole('button', { name: 'Cerrar diálogo', exact: true }).click();
  await settings.waitFor({ state: 'hidden' });
  await openAssistant(page);
}
module.exports = { configurePreparation, openAssistant };
