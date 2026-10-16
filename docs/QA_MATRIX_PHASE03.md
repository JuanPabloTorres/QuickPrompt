# Matriz QA — Fase 03, 2026-10-15

PASS de un fixture significa únicamente que el código actuó sobre un DOM simulado. No certifica ChatGPT/Claude, sus términos, una sesión autenticada ni aceptación del modelo. El [estado final](PHASE03_ACCEPTANCE.md) distingue ambas puertas.

| Requisito / escenario | Prueba y evidencia | Resultado / límite |
|---|---|---|
| Baseline build / suite anterior | baseline/build.log, baseline/tests.log; TRX phase03-baseline | Build limpio; 70/70 PASS reales |
| Baseline navegador | baseline/browser-offline.json; fallo de selección documentado en baseline/observed-failures.json | Primer intento FAIL; causa corregida, selección exacta esperada sin modificar criterio |
| Regresión de baseline corregido | baseline-after-fix/*.json | PASS_COMPLETE_BROWSER_ACCEPTANCE, Qwen3/reinicio reales |
| Dominio y contratos actuales | races-final/tests.log y unit-tests.json | 81/81, cero omitidas; 70 anteriores, 9 contratos iniciales y 2 regresiones del transporte web |
| CRUD, variables, copiar, favoritos, archivo, editor visual, historial/versiones, import/export, restore, fechas y preferencias | final/browser-workflows.json; SQLite real en suite anterior | PASS. Importación solo sintética/autorizada; DB privada no leída |
| Funciones básicas sin Ollama | final/browser-offline.json; ui-regression.json | PASS con endpoint local inaccesible; no se detuvo Ollama del usuario |
| IA local/versionado/no aplicación silenciosa/cancelación/reinicio | final/browser-ai.json; final/browser-restart.json | PASS Qwen3 real; no se afirma mejora de calidad general ni se borra el fallo Qwen2.5 de GOAL 02 |
| Loopback, Host, Origin, CSP, antiforgery y no requests externos incidentales | final/http-security.json; browser-workflows.json | PASS en alcance ejecutado. El fallback externo se activa solo por clic/consentimiento |
| Contratos MV3/origen exacto/URL allowlist/TTL/límites/replay concurrente y tras reiniciar ledger/manifiesto | bridge-contracts.mjs; bridge-contracts.log | PASS 6 pruebas Node; no pruebas ficticias de sesiones externas |
| Extensión detectada, protocolo real, preparar Unicode/LF/comillas/ángulos/emoji, no envío en PrepareOnly | extension-runtime.json, bridge-fixture.png | PASS fixture en Edge con manifiesto de producción y SW real |
| Enviar explícito, un intento, confirmación UI, volver a Preparar | extension-runtime.json y adapter-fixtures.json | PASS fixture. Envío no confirmado se registra como manual, nunca como éxito del modelo |
| Cancelación web→content script→SW→adapter | extension-runtime.json | PASS fixture; después del clic no promete deshacerlo |
| Chrome | chrome/extension-runtime.json | PASS fixture con versión real en JSON; no proveedor real |
| Ambos adaptadores × preparar/enviar | adapter-fixtures.json | 30 escenarios DOM, explícitamente simulados. Claude sigue bloqueado en el SW de producción |
| Render reemplaza editor, auth antes/después del clic, generación en curso, deadlines sin mutación y frames pausados | races-final/bridge-races.json | PASS 14 escenarios adicionales, ambos adaptadores, DOM simulado; fallos previos preservados |
| Almacenamiento de reserva denegado, scripting denegado y fallo de auditoría final | races-final/worker-failures.json | PASS 3 fallos API inyectados en extensión real Edge; rechaza actuar sin reserva y no queda bloqueado por fallo de registro final |
| Editor con otro borrador, login, editor lento, SPA, botón deshabilitado/ambiguo, selector ausente, cancelación, XSS, largo 20k, expired | adapter-fixtures.json | PASS fixture, texto ajeno conservado; sin scraping de conversación |
| Pérdida de foco | adapter-fixtures.json; SW verifica pestaña activa/ventana enfocada/documento | Guard DOM probado con simulación de hasFocus=false; cambio real de ventana humana pendiente, no equivalente a prueba manual |
| Mensaje repetido / origen ajeno / mensaje sin clic | bridge-contracts.log, extension-runtime.json | Reserva durable rechaza replay; emisor/frame/origen inválidos y gesto no confiable rechazados |
| Extensión ausente/deshabilitada | ui-regression.json usa contexto sin extensión | PASS ausencia y fallback; deshabilitación manual durante un envío no probada |
| Permisos host revocados por usuario | Errores controlados por SW; no prueba manual de la UI de permisos | PENDIENTE manual; no se inventa PASS |
| Pestaña existente/nueva | Existente probada en fixture; nueva creada en primera reproducción, no hubo editor válido | Apertura observada; éxito en nueva pestaña externa BLOQUEADO. Edge no enruta su primera petición creada por extensión al fixture |
| ChatGPT real × preparar/enviar autenticado | real-providers.json, chatgpt-readiness.png | BLOQUEADO: HTTP 403, cero editores en navegador aislado, sin sesión; no bypass ni envío |
| Claude real × preparar/enviar | real-providers.json; términos 3.7 | BLOQUEADO POR PROVEEDOR: sin permiso expreso de Anthropic; no acceso automatizado |
| Fallback clipboard + URL fija root, no envío registrado | ui-regression.json; fallo previo en fallback-reproduction | PASS con clipboard-write por defecto, sin permisos preconcedidos, portapapeles real y destino enrutado; no texto en query |
| Responsive 320/390/768/1024/1440; tema claro/oscuro; ES/EN | ui-regression.json y capturas | PASS UI local, sin overflow de documento |
| Contraste, labels, foco/teclado/modales, Escape | final/accessibility.json | PASS auditoría automatizada propia; lector de pantalla humano no disponible, sin certificación WCAG completa |
| Reduced motion / skeleton / arranque lento recuperable | ui-regression.json, startup-recovery.png | PASS; animaciones acotadas, recuperación a 12 s |
| Tailwind local v4, lockfile y CSS reproducible | tailwind/css-build.log, css-check-after-ci.log, ui/ui-regression.json; package-lock.json | PASS: 4.3.0 autorizado, recompilación byte idéntica tras npm ci offline; Blazor sirve solo quickprompt.css y Edge valida contenido/token calculado |
| Métricas select→copy / prepare / envío | ui-regression.json; extension-runtime.json | Muestras/tamaño y alcance declarados. Cero muestras de inserción/envío en proveedores reales; no porcentajes externos inventados |

Todos los enlaces de evidencia sin prefijo están bajo `docs/evidence/phase03`. Los harnesses usan DB/perfiles nuevos y requieren readiness de su proceso antes de acciones destructivas. No modificar fixtures/umbrales para pasar. No ejecutar pruebas normal-daily de GOAL 02 contra una biblioteca privada.

La primera reproducción de carreras obtuvo 0/10; el caso de reemplazo de editor inicialmente dependía del scheduling del fixture. Se aisló la transición dentro de input para demostrar el reemplazo durante la preparación, manteniendo la misma aserción y conservando salidas intermedias. La ampliación final añadió intercepción de login tras el clic y frames pausados: 14/14. No son resultados de un proveedor autenticado. La suite original de 30 casos y sus criterios no cambiaron.

Repetición final con Tailwind en `evidence/phase03/tailwind`: solution-build/tests (81/81), contracts (6), adapter-fixtures (30), bridge-races (14), worker-failures (3), extensión real en Edge/Chrome sobre fixtures y UI con CSS compilado. `local-regression/` repite CRUD/variables/copia/importación/backup/restore, modo sin Ollama, IA Qwen3 real, reinicio, HTTP y accesibilidad. `restore-preferences-after.json` y `kestrel-override-after.json` vuelven a pasar las regresiones GOAL 02. `normal-start.json`: aplicación entregada activa sin Demo/DataPath, HTTP 200 y solo loopback. npm audit final: cero vulnerabilidades reportadas; el primer árbol vulnerable se conserva como evidencia y no es el lockfile entregado. Las 64 evidencias originales, dos datasets y persistencia/cliente Ollama siguen intactos por hash (`tailwind/preservation.json`).
