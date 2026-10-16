# Proveedores y mecanismos

| Destino | Modo ofrecido en NOVA | Estado de comprobación |
|---|---|---|
| Ollama | LOCAL_EXECUTION | Qwen3 instalado, generación/SSE/cancelación reales `EXECUTED_PASS` |
| ChatGPT | COPY_AND_OPEN | Portapapeles + URL en fixture interceptado `EXECUTED_PASS`; sesión real `NOT_TESTED` |
| Claude | COPY_AND_OPEN | Portapapeles + URL en fixture interceptado `EXECUTED_PASS`; automatización deshabilitada |
| Gemini | COPY_AND_OPEN | Registry disponible; recorrido en sitio auténtico `NOT_TESTED` |
| Copilot | COPY_AND_OPEN | Registry disponible; recorrido en sitio auténtico `NOT_TESTED` |
| Perplexity | COPY_AND_OPEN | Registry disponible; recorrido en sitio auténtico `NOT_TESTED` |

La presencia de una URL no certifica disponibilidad ni autenticación. Por ello `LastVerifiedAt` permanece null y el estado externo es `MANUAL_PASTE`. `CanSubmit=false` en los destinos web. Tener una sesión web no se interpreta como acceso API.

Preparar una transferencia compila en la API local y resuelve un ID de proveedor en una allowlist. Solo después de la acción del usuario se copia el texto y se abre una pestaña vacía/destino. No se pone el prompt en query strings, no se transmite por HTTP a terceros ni se pulsa Enviar. Si el navegador bloquea la pestaña, se ofrece un enlace manual; si falla la copia, se cierra la pestaña reservada.

La extensión MV3 anterior permanece sin cambios y usa su contrato/origen anterior. No se anuncia como conectada al React nuevo. BROWSER_BRIDGE y OFFICIAL_API no están habilitados en el nuevo registry. Adaptar MV3 requiere revisar origen, handshake y pruebas de sitios permitidos antes de ofrecerlo. Claude no recibirá automatización por la existencia de un login.

Destinos revisados el 2026-10-16 mediante páginas oficiales: [ChatGPT](https://chatgpt.com/), [Gemini](https://gemini.google.com/), [Copilot](https://copilot.microsoft.com/) y [Perplexity](https://www.perplexity.ai/). La consulta a [Claude](https://claude.ai/) estuvo restringida; se conserva el destino del adaptador anterior sin declarar validación de integración. No se enviaron prompts a esos sitios durante las pruebas: las pruebas del fallback interceptan y sirven HTML ficticio local.
