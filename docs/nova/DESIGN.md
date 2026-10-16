# Living Canvas

GOAL 07 sustituye la entrada principal de Discover por intención natural → resultado preparado en el mismo canvas, con título automático, campos faltantes, Copiar/Guardar/Usar/Ajustar y Detener durante inferencia. La búsqueda léxica sigue filtrando la biblioteca al escribir sin consultar Ollama. El editor manual sigue disponible como acción voluntaria. El nuevo borrador solo persiste contenido/valores tras marcar el consentimiento de conservación; el tema sigue siendo el único dato en localStorage. [Arquitectura y límites](GOAL07_ARCHITECTURE.md).

Una sola superficie, sin sidebar permanente, dashboard ni panel de chat. Discover presenta búsqueda y prompts reales; Focus muestra campos y acciones de uso; Create exige solo título/contenido. Los tres estados conservan el shell. Las listas son filas abiertas con separadores discretos; las acciones secundarias se abren en contexto.

Los tokens viven en `apps/web/src/design/tokens.css`. Dark usa azul noche `#08131e`, texto `#eaf1f8` y acento teal `#65e3d2`. Light usa fondo `#f4f7f8`, texto `#102b3b` y acento `#086c66`. Muted y límites de controles se definen por tema, sin depender de glow. Segoe UI/system-ui para lectura; Consolas solo en indicadores breves. No se descargan fuentes, imágenes ni recursos de terceros.

La consulta a `ui-ux-pro-max` recomendó jerarquía minimalista y espacio abierto. Se tomaron esos principios; la paleta propuesta por la búsqueda no sustituye la identidad azul/teal del SOW.

Motion usa transiciones de 200 ms entre estados. El núcleo SVG solo gira cuando hay una búsqueda u operación real. `prefers-reduced-motion` desactiva animación CSS y Motion respeta la preferencia. El cambio de visibilidad del documento pausa animaciones SVG. No hay WebGL ni escena 3D.

Los diálogos usan Radix para nombre/descripción, Escape y focus trapping; overlay y bloqueo de scroll propios evitan la inyección dinámica de estilos incompatible con CSP. Inputs con labels, botones de icono con nombres accesibles, skip link, foco visible y toast de copia/guardado. Borradores y valores permanecen en memoria durante cambios de contexto; no se guardan en localStorage. Allí solo se almacena el tema.

En móvil las variables forman una columna y el dock inferior presenta Copiar/Usar dentro del flujo, con el feedback debajo. No se superponen toasts ni una barra fija a los campos o acciones. El objetivo de toque es 44 px para acciones. `scripts/nova-design-check.cjs` mide contraste de tokens de texto, controles y botones en ambos temas; [resultados](../evidence/nova/contrast.json). Esto no sustituye una auditoría WCAG completa.

Se probaron 320, 390, 768, 1024 y 1440 px, altura corta, hit testing de Copiar, movimiento reducido y Ctrl K. [Captura desktop de revisión](../evidence/nova/review-desktop.png), [Focus móvil claro actual](../evidence/nova/review-mobile-light.png). Zoom nativo 200%, lector de pantalla y usabilidad con personas permanecen `NOT_TESTED`.
