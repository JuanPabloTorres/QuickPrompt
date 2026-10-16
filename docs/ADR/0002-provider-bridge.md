# Fase 03: límite de confianza del puente web

Blazor y SQLite conservan su arquitectura y formato v1. El navegador local delega únicamente un texto compilado y una operación explícita a una extensión MV3. No hay proxy servidor a proveedores, credenciales, lectura de conversaciones ni llamadas API externas.

Contrato v1 tipado; texto máximo 20 000 caracteres, UUID, expiración máxima dos minutos, proveedor/acción en allowlist. La extensión valida nuevamente, exige un clic DOM confiable en el origen exacto http://127.0.0.1:5188 y reserva el requestId persistentemente antes de actuar. El registro contiene solo metadatos; el prompt vive en memoria durante la operación. No se cambia SQLite ni el respaldo existente: el registro del puente queda separado en almacenamiento local de la extensión y la sesión del componente.

Preparar y enviar son acciones distintas; envío desactivado por defecto, consentimiento de privacidad por operación. No se sobrescribe un editor con otro texto, no se reutilizan conversaciones, no se reintenta el envío. SubmittedUI exige observar un cambio del compositor tras una única interacción; no prueba respuesta o aceptación del modelo. Cancelar después de un clic no puede deshacer ese clic.

Claude: los términos consultados el 2026-10-15, sección 3.7, restringen acceso automatizado sin permiso expreso. La distribución local bloquea su adaptador antes de acceder al sitio; ofrece copiar/abrir manualmente. Los tests de DOM de Claude son fixtures, nunca evidencia del proveedor. No se añade un interruptor para eludir esta puerta.

ChatGPT: validar en navegador real sin extraer Output ni sortear autenticación/protecciones. Si falta sesión o existe una restricción, reportar bloqueo y usar fallback. No se presume autorización o estabilidad del DOM por aprobar un fixture.

Tailwind: requiere aprobación puntual de tooling ausente; mientras se espera, se conserva CSS funcional. CSS compilado local se entrega con el producto; dotnet run no invoca npm ni red. Lucide ya está instalado: usar un subconjunto SVG local conservando licencia ISC/MIT, sin runtime.

Fuentes: [mensajería MV3](https://developer.chrome.com/docs/extensions/develop/concepts/messaging), [scripts aislados](https://developer.chrome.com/docs/extensions/develop/concepts/content-scripts), [términos Anthropic](https://www.anthropic.com/legal/consumer-terms), [términos OpenAI](https://openai.com/policies/terms-of-use/).
