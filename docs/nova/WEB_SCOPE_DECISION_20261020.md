# Alcance vigente: versión web

El usuario respondió a la propuesta de herramientas MAUI: **«Continuar sólo con la
versión web»**. Desde esta decisión, la meta activa se centra en React, API, SQLite,
NOVA web y compatibilidad web existente. La retirada de Blazor conserva su criterio
de paridad separado; no se autoriza implícitamente por este cambio de alcance.

- MAUI/.NET 9/workloads quedan fuera del incremento y de la aceptación web actual.
  La propuesta de instalación deja de estar pendiente; no se instala ninguna herramienta.
- Los cambios históricos del host nativo permanecen sin validar. No se descartan
  archivos de trabajo anteriores ni se transforma ese estado en PASS.
- La matriz canónica ya define web local y excluye afirmar aceptación MAUI/Android.
  Ninguna de sus 88 filas exige SDK/MAUI. Se conservan IDs, criterios y estados,
  34 cerrados/54 abiertos; MAUI deja de citarse como impedimento del cierre web.
- Permanecen los requisitos web: comprensión/fidelidad del agente, campos, navegación,
  aprobaciones, persistencia, seguridad, paridad y latencia según la matriz.
  El portapapeles del sistema desde el navegador sigue siendo un requisito web;
  no se confunde con la aplicación MAUI excluida.

Las notas anteriores que describen autorización/herramientas MAUI pendientes son
históricas y quedan supersedidas por esta decisión, sin alterar sus pruebas/evidencias.
