# GOAL 07 — QuickPrompt Experience: simplicidad, usabilidad y acceso inmediato

## MISIÓN

Continuar el proyecto existente `C:\Projects\Repos\QuickPrompt-Web` desde los resultados de GOAL 06.

La meta NO es añadir nuevas funcionalidades, sino convertir las capacidades existentes en una experiencia web realmente simple, profesional y agradable.

Prioridad absoluta:

**Encontrar → Completar si es necesario → Copiar o utilizar.**

No reconstruyas la arquitectura ni agregues otro framework de UI.

## FASE 1 — Auditar la experiencia real

1. Inspecciona código y pruebas actuales.
2. Ejecuta el baseline de 150 pruebas y documenta cualquier diferencia.
3. Inicia QuickPrompt sin modo Demo.
4. Navega con Playwright en Edge y captura cada estado relevante.
5. Audita visualmente espaciado, alineación, controles, jerarquía, scroll, overlays y legibilidad.
6. Cuenta pasos y transiciones para reutilizar una plantilla.
7. Identifica funciones repetidas, botones innecesarios y paneles que deben consolidarse.

No asumas que un layout es profesional únicamente porque carece de overflow.

## FASE 2 — Una experiencia principal

Consolida el uso cotidiano en una sola pantalla:

- Buscar prompts.
- Ver favoritos y recientes.
- Filtrar por categoría y grupo.
- Seleccionar plantilla.
- Completar variables.
- Copiar prompt.
- Utilizar en un proveedor.

El editor de creación debe ser contextual y requerir solo título y texto.

Conserva crear variable, deshacer variable, renombrar, Undo/Redo y visualización de marcadores.

Los prompts sin variables se utilizarán directamente.

Mantén compartir, exportar JSON, importar y configuración, pero como acciones contextuales.

No introduzcas un dashboard.

## FASE 3 — Quick Assist discreto

Convierte Quick Assist en una paleta de comandos compacta.

Debe permitir encontrar, crear y mejorar prompts mediante órdenes breves.

Las acciones deterministas se ejecutan sin LLM.

Las propuestas de Qwen3 se validan y requieren aceptación antes de modificar datos.

Muestra loading localizado, cancelación, errores recuperables y resultados claros.

No añadas una conversación permanente ni un segundo editor.

## FASE 4 — Panel lateral del navegador

Verifica la extensión MV3 existente en Edge y Chrome mediante perfiles de prueba aislados.

Prueba el docking real del Side Panel, no solamente su documento HTML.

Comprueba:
- Apertura y cierre.
- Anchos estrechos.
- Búsqueda.
- Favoritos.
- Variables.
- Compilación exacta.
- Copiar.
- Apertura del proveedor.
- Reconexión con QuickPrompt local.
- Servidor desconectado.
- Estado y seguridad de permisos.

No copies secretos ni estados de autenticación privados a los fixtures.

No habilites inserción automatizada en proveedores auténticos sin verificar que la interacción esté permitida y se encuentre autorizada.

Claude conserva el fallback manual mientras no exista autorización aplicable.

## FASE 5 — Consolidación de proveedores

Todos los destinos deben consumir el mismo CompiledPrompt inmutable.

Destinos:
- Copiar.
- Qwen local.
- ChatGPT web.
- Claude web.
- Otros destinos configurables de copiar/abrir.

Los destinos externos deben informar con precisión si el contenido fue copiado, insertado, enviado, bloqueado o no verificado.

No presentar copiar/abrir como envío exitoso.

No implementar APIs de pago ni añadir credenciales sin autorización.

## FASE 6 — Pruebas de experiencia

Ejecuta con Playwright los siguientes flujos:

A. Abrir → buscar → copiar prompt sin variables.

B. Abrir → seleccionar → completar dos variables → copiar.

C. Crear → seleccionar texto → convertir en variable → guardar → utilizar.

D. Deshacer variable → guardar → copiar.

E. Favorito → localizar → utilizar.

F. Crear grupo → asignar prompt → filtrar → utilizar.

G. Exportar plantilla JSON → importar en base aislada → verificar equivalencia.

H. Mejorar prompt con Qwen3 → aceptar o rechazar.

I. Ejecutar prompt con Qwen3 → recibir streaming → copiar respuesta.

J. Abrir Side Panel → seleccionar → compilar → copiar.

K. Ollama desconectado → funciones básicas operativas.

L. Reiniciar servidor → datos íntegros.

Revisar geometría real, navegación por teclado, errores de JavaScript, estados de carga, foco, scroll y contraste.

Validar escritorio, móvil y anchos de panel lateral.

Registrar también los tiempos automatizados, aclarando que no equivalen a tiempos humanos.

Preparar un protocolo de prueba moderada con cinco usuarios. No simular participantes ni declarar una evaluación humana que no ocurrió.

## CRITERIOS DE ACEPTACIÓN

- El flujo principal utiliza una pantalla.
- Crear una plantilla exige únicamente título y contenido.
- Los prompts sin variables no muestran formularios innecesarios.
- Los prompts con variables se completan sin entrar en edición.
- No hay pantallas redundantes para acciones principales.
- La extensión se puede abrir y utilizar visualmente en un navegador real dentro del alcance autorizado.
- Quick Assist no bloquea la biblioteca.
- Los destinos utilizan exactamente el texto compilado.
- No hay pérdida de datos, envíos involuntarios ni regresiones.
- La suite existente y las pruebas nuevas pasan.
- La experiencia queda lista para evaluación humana, sin confundirla con aceptación comercial.

## ENTREGA

Documenta:
- Auditoría visual antes/después.
- Cambios de UI y funciones consolidadas.
- Capturas reales.
- Pruebas unitarias e integración.
- E2E Edge/Chrome.
- Side Panel docking.
- Estado de proveedores.
- Resultados y bloqueos.
- Evidencia de persistencia y recuperación.
- Guía breve de uso para nuevos usuarios.

Conserva la evidencia anterior, los datos privados y los contratos actuales.

No instalar software, modelos ni servicios externos sin autorización.

Comienza auditando la experiencia real y corrige primero cualquier fricción que afecte a buscar, completar y copiar un prompt.