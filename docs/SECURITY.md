# Límites de seguridad y privacidad

Uso local, un usuario del equipo. Datos sin cifrado propio protegidos por la cuenta y permisos del sistema operativo. No hay autenticación multiusuario; no exponer esta instancia a LAN/Internet. Un proceso malicioso ejecutándose como el mismo usuario puede leer la DB y el puerto local: no se declara protección frente a host comprometido.

Servidor enlaza 127.0.0.1 (5188 por defecto) y rechaza IP remota no loopback, Host distinto de localhost/127.0.0.1 y Origin diferente del origen exacto. Blazor usa antiforgery y circuitos del origen; no se ofrecen endpoints REST mutadores. Sin CORS permisivo. CSP self, frame-ancestors none, scripts/fuentes locales, nosniff y no-referrer. Ollama backend-only, HTTP con dirección IP loopback literal; sin proxy ni redirects. No auto-submit a proveedores externos, URLs arbitrarias ni herramientas de shell en runtime.

Prompts siempre tienen autoridad de usuario; las variables se concatenan como datos por tokens sin volver a interpretarlas. Salidas se renderizan como texto Razor escapado, sin MarkupString/innerHTML. SQL parametrizado; nombres de tabla del migrador en lista fija. La respuesta del modelo no autoriza mutaciones. Mejoras se revisan lado a lado; aceptación explícita, control de revisión y variables en almacenamiento. Ejecución exige checkbox y acción del usuario; se invalida al cambiar valores/modelo.

Límites: título 160, descripción 2000, plantilla 32000, 60 variables, nombres 80, valor 8000, compilado/respuesta 64000 caracteres; imports 4MB/5000 plantillas, JSON profundidad 32 y claves duplicadas rechazadas. DB seleccionada se copia a temporal privado aleatorio, se abre read-only y se elimina en finally. No se usa el nombre del archivo como ruta destino. Restore valida todo antes de borrar, en transacción. No se lee ni distribuye QuickPrompt.db3 del repositorio.

El texto final y versiones son datos privados persistentes. Valores estructurados anteriores opt-in; borrado de historial elimina compilados, ejecuciones y valores. Respuestas guardadas solo con acción explícita. Propuestas y sus razones se conservan para auditoría; borrar plantilla elimina sus propuestas y versiones. Export/backups son privados y contienen esos datos; el navegador decide destino. No prompts en logs de la app; evidencias solo contienen fixtures ficticios. Logs de infraestructura no equivalen a telemetría.

Revisiones y pruebas: ver src/QuickPrompt.Tests, tests/e2e y MVP_ACCEPTANCE. No se declara auditoría externa, cifrado en reposo, aislamiento frente a otro proceso local ni escaneo de vulnerabilidades actualizado de NuGet (restauración offline desde cache).

## Fronteras GOAL06

[Auditoría de cambios y límites](GOAL06_AUDIT.md). Compartir usa proyección allowlist; el respaldo completo sigue siendo privado. Importar valida antes de preview y de commit. Quick Assist admite como máximo4 acciones tipadas y liga revisión/identidad antes de aprobar cambios; no tiene enviar/borrar/script/auth. PanelSession solo expone búsqueda y compilación validadas a la extensión mediante la pestaña local; no crea rutas HTTP. MV3 agrega solo sidePanel, conservando hosts, aislamiento de frame/origen y bloqueo de Claude. La memoria del panel se descarta al desconectar; un ID de pestaña se conserva en storage.session, sin texto.

La supervisión Playwright solo usa orígenes exactos y acciones accesibles. La política entregada admite loopback; cada interacción externa necesitaría aprobación humana en terminal, además de una política revisada. Sin autenticación, cookies, perfiles del usuario, eval ni scripts del modelo. Las capturas solicitadas quedan locales; pueden incluir datos de la página autorizada. No se habilitó supervisión de terceros.
