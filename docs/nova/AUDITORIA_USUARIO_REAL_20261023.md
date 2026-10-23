# Auditoría de uso real — 23 de octubre de 2026

Seguimiento: [corrección y revalidación de F01–F05](CORRECCION_AUDITORIA_20261023.md). El resultado que sigue conserva lo observado en la campaña original, antes de esa reparación.

**Resultado:** auditoría exploratoria completada con defectos del producto abiertos. La biblioteca manual resuelve tareas útiles; NOVA aún presenta bloqueos y fallos de continuidad. Esto no es una nueva certificación integral ni sustituye la aceptación humana de la versión inicial.

## Alcance e identidad

Se utilizó la interfaz como usuario: ayuda, biblioteca, formularios, acciones de NOVA, revisión, ejecución local, historial y archivos. Las escrituras del producto se realizaron con controles visibles; las lecturas HTTP y SQLite se usaron para comprobar resultados. No se reparó código de producto ni se publicó una versión.

- Instancia aislada: `http://127.0.0.1:5284/`, base `.playwright/user-audit-20261023/library.sqlite`.
- API/SDK: candidato congelado `action-workbench-release-20261022`; frontend: `.nova-build/maintenance-20261023/web`, con el mantenimiento previo todavía sin commit.
- Runtime: `adaptive-core`; proveedor local Ollama; modelo exclusivo `qwen3:8b`, digest `500a1f067a9f782620b40bee6f7b0c89e17ae61f686b92c24933e4ca4b2b8b41`.
- Navegador integrado, panel estrecho de aproximadamente 430 px. Las pruebas anteriores de escritorio no se presentan como ejecuciones de esta auditoría.
- Datos sintéticos y ejemplos públicos. La instancia de usuario en 5277 y la base privada de 5196 no se modificaron.
- Evidencia local: `.playwright/user-audit-20261023/identity.json`, `journal.json`, recibos de runs, capturas, archivos exportados y `integrity-check.json`. Los archivos ignorados no se incorporan como fuentes ni contienen datos reales del usuario.

## Uso productivo observado

Se importaron 60 ejemplos desde la ayuda. Repetir la importación no creó duplicados. Se creó un clasificador reutilizable con `<ticket>`, se guardó, se marcó favorito y se asignó a «Auditoría · Operaciones»; la asignación y el favorito sobrevivieron a la recarga.

La ejecución manual produjo JSON correcto para un ticket de Sol, prioridad baja y problema de descarga de factura. Una instrucción de borrado dentro del texto citado del ticket no provocó borrados de dominio. No se extrapola este caso a resistencia universal frente a inyección.

Otra ejecución mediante NOVA, con el dato explícito en su formulario, terminó `COMPLETED` en 15,4 segundos y devolvió:

```json
{
  "cliente": "Luna",
  "prioridad": "media",
  "resumen": "No puedo cambiar mi dirección de envío"
}
```

Run: `8f3ace33-079b-4ec2-8afe-6eac94e1ddde`, evento 54; recibo `agent-execute-completed.json`, captura `agent-execute-response.png`. El valor por defecto de prioridad funcionó. Se copió la respuesta con confirmación visible.

Se preparó el clasificador para Claude con otro ticket. El texto y el destino elegidos eran correctos; no se abrió el sitio ni se envió contenido. «Completar variables» reemplazó un ticket previo por uno nuevo sin cambiar la versión de la plantilla.

## Fallos prioritarios reproducidos

### F01 — P1: aprobar una propuesta no consigue guardarla

**Esperado:** confirmar una copia o versión guarda exactamente la propuesta revisada, devuelve un recibo y permite reabrirla.

**Observado en dos intentos independientes:**

1. Crear un prompt de actas, corregirlo manualmente y aprobar la copia terminó `UI_TARGET_TIMEOUT`. No apareció el nuevo registro; total 61. Run `79353eb6-c190-474f-8628-899f943472bd`, evento 48; `create-corrected-save-failed.json`.
2. Actualizar el prompt ya guardado para añadir Plazo, sin modificar manualmente la propuesta, también terminó `UI_TARGET_TIMEOUT`. La versión seguía en 1. Run `98151379-d144-48d1-ae6d-b7f4b6ebacb1`, evento 42, duración observada 119,5 s, última etapa IA 63,9 s; `update-save-failed.json` y `update-save-timeout.png`.

En ambos casos, pulsar Guardar desde el editor después del fallo sí persistió el contenido: una copia nueva en el primero y versión 2 en el segundo. Es recuperación manual, no éxito del guardado de NOVA.

**Frontera observada:** aprobación → herramienta `commit_approved` → control `approval.apply` / aplicación del editor. `AgentUiCommands.ts` espera el control del run; `NovaLive.tsx` sólo lo presenta bajo sus condiciones de actividad, worker y aprobación. No se capturó el estado DOM de cada frontera durante el timeout; la causa exacta permanece **sin confirmar**. No atribuirlo al modelo, al viewport o a las correcciones manuales sin el experimento correspondiente. El segundo intento descarta que la edición manual sea condición necesaria del fallo.

**Aceptación de la reparación futura:** crear y actualizar, con y sin edición humana, aprobar y comprobar persistencia/revisión/contenido; repetir con panel estrecho y ancho. Conservar los intentos fallidos. Dos fallos verificados de esta transición en esta campaña; no fabricar un tercero para alcanzar un umbral.

### F02 — P1: el botón de revisión de NOVA usa una propuesta anterior a la edición visible

Después de cambiar título y cuerpo en el editor, «Revisar y guardar copia» mostraba el título y texto originales del modelo. «Guardar prompt» desde el editor abría la revisión con el contenido corregido. Se canceló la revisión vieja; no se aprobó contenido distinto del revisado.

Captura `review-ignores-editor-correction.png`. Propietarios a revisar: `NovaLive.tsx`, propuesta persistida y sincronización del borrador/editor con el contrato de aprobación. Se observa divergencia entre dos entradas de revisión; falta trazar la revisión/hash de cada una para establecer el mecanismo completo.

**Aceptación futura:** cualquier entrada de revisión debe mostrar la misma identidad/revisión del borrador que se aprobará y guardará, o explicar expresamente qué propuesta anterior se está revisando.

### F03 — P1: aclaración semántica sin forma de responder

Petición: «Necesito una instrucción que revise un fragmento de código antes de fusionarlo y me ayude a detectar errores y riesgos de mantenimiento».

La búsqueda recuperó dos candidatos pertinentes y preguntó: «¿Cuál quieres usar: Encontrar la causa de un error de código, Revisar código antes de integrarlo?». No ofreció botones de candidatos, enlaces ni campo para responder. Se llegó a `Necesito tu atención`; para continuar hubo que iniciar otra acción, Buscar y abrir, elegir otro prompt, filtrar y seleccionar el adecuado.

La recuperación semántica fue pertinente y no heredó el prompt de actas que estaba abierto. El defecto es la continuidad de la aclaración en la interfaz por acciones. Propietarios: presentación de `question` en NOVA y contrato de respuesta/reanudación.

**Aceptación futura:** resolver una ambigüedad mediante controles visibles ligados a IDs de candidatos y continuar la intención original sin adquirir efectos adicionales. No reintroducir inferencia libre de acciones para solucionar esta interacción.

### F04 — P2: instrucciones de sesión contaminan una plantilla reutilizable

Se pidió un prompt que extrajera tareas y responsables de `<notas>` y, como instrucción de la sesión, preparar el borrador para revisión sin ejecutarlo ni guardarlo. El modelo añadió al cuerpo reutilizable: «Prepara solo el borrador del prompt para revisión, sin ejecutar ni guardar». La revisión lo consideró listo.

Esa frase cambia el comportamiento futuro: en lugar de extraer las tareas, puede ordenar generar otro borrador. Se eliminó manualmente antes de guardar. Captura `created-contaminated-draft.png`. Propietarios: construcción del contexto/generación y revisión de fidelidad en Core. Es fallo de separación entre instrucciones del artefacto y efectos de la sesión, no autorización para añadir filtros por frases específicas.

**Aceptación futura:** tareas reutilizables con instrucciones de revisión/guardar/copiar únicamente para la sesión; el artefacto conserva propósito y variables, mientras el contrato de operaciones conserva esas restricciones fuera del cuerpo.

### F05 — P2: ejecución local falla en una ruta y funciona en otras

Primer intento: se rellenó `ticket` en el detalle, se abrió Ejecutar con IA local y el formulario mostraba el campo vacío. Al continuar, el texto preparado sí incorporó el valor del detalle. Terminó `PROVIDER_UNAVAILABLE`, sin respuesta, en unos 1,6 s; el diálogo quedó con ChatGPT seleccionado. El error sólo se descubrió cerrando el diálogo y abriendo la última actividad.

El modelo estaba instalado; la ejecución manual funcionó. Más tarde, la misma acción con un valor explícito en su formulario sí terminó correctamente con Ollama. Por tanto no se demuestra indisponibilidad permanente del proveedor ni fallo universal de la acción.

`QuickActions` inicia valores vacíos y la ruta puede conservar datos del workspace; el usuario no ve fielmente los datos que se usarán. `PreparedUse` carga destinos asincrónicamente y `AgentUiCommands` comprueba las opciones visibles. Una carrera de carga es **hipótesis**, no causa confirmada: no se capturaron las opciones en el instante del fallo original.

**Aceptación futura:** datos efectivos visibles, vacío/omitido definidos; selección del proveedor después de cargar capacidades; error visible en el mismo diálogo. Contrastar entrada heredada, explícita, ausente y carga fría/lenta.

## Cobertura de la campaña

`PASS_UI` significa que se observó la postcondición en la interfaz de ese caso. No equivale a garantía general. Las recuperaciones, controles parciales y límites se conservan por separado.

| Caso | Operación | Resultado observado |
| --- | --- | --- |
| U01 | Ayuda, importar 60 ejemplos y repetir | Sin duplicados |
| U02 | Crear manualmente clasificador y favorito | Guardado; favorito revisión 2 |
| U03 | Crear colección y asignar | Persistió; se confirmó tras recarga |
| U04 | Ejecutar con formulario vacío y detalle rellenado | F05: `PROVIDER_UNAVAILABLE` |
| U05 | Usar en → Ollama con ticket citado adversarial | JSON útil; sin borrados |
| U06 | Crear prompt de actas con NOVA | F04: contaminación de sesión |
| U07 | Revisar edición y aprobar copia | F02 y F01; no persistió |
| U08 | Guardar manualmente después del fallo | Recuperación, sin duplicado |
| U09 | Buscar por significado desde otro prompt activo | Candidatos útiles; F03 impide responder |
| U10 | Buscar y abrir; consulta sin resultados y acentos | Sin coincidencias informado; recuperación correcta |
| U11 | Actualizar añadiendo Plazo | Contenido correcto; F01 al guardar; recuperación manual |
| U12 | Comparar versiones 1/2 y restaurar 1 | Nueva versión 3, historial conservado |
| U13 | Duplicar, archivar, recuperar | Copia independiente; revisiones 1/2/3 |
| U14 | Renombrar variable, deshacer, rehacer y guardar | `<apuntes>` guardado, copia revisión 4 |
| U15 | Unicode, etiquetas literales y dos modos de copia | Texto literal conservado; recibos de copia visibles |
| U16 | Cancelar eliminación | Registro conservado |
| U17 | Eliminar copia desde menú NOVA | Confirmación humana específica; desapareció sólo la copia |
| U18 | Compartir JSON y respaldos JSON/SQLite | Archivos creados; SQLite `integrity_check=ok` |
| U19 | Importar plantilla exportada y repetir revisión | 1 nuevo; después 1 duplicado; segunda importación cancelada |
| U20 | Favoritos y ejecución con dato explícito | NOVA `COMPLETED`; JSON Luna/media correcto |
| U21 | Preparar para Claude | Destino y texto correctos; sin envío |
| U22 | Completar variables | Actualizó dato visible; sin guardar plantilla ni ejecutar |
| U23 | Copiar plantilla mediante NOVA | Recibo visible; bytes del portapapeles sin comprobar |
| U24 | Adaptar y preparar copia, detener durante redacción | Cancelación observada; plantilla original sin cambios |
| U25 | Historial de registro eliminado | Snapshot de versión 4 y datos originales accesibles |
| U26 | Recargar y filtrar colección propia | Clasificador/favorito/asignación persistentes |
| U27 | Integridad de originales y respaldo | 60 hashes completos idénticos; respaldo SQLite íntegro |

Los estados concretos del journal conservan diferencias entre aprobación parcial, recibo y comprobación HTTP/archivo. Recorrer las diez entradas del menú no demuestra que las diez completen todos sus caminos: Adaptar y preparar copia se probó hasta cancelar, y Crear/Actualizar fallaron al guardar con NOVA.

## Integridad y artefactos finales

- Los 60 registros públicos protegidos mantienen el hash SHA-256 del objeto HTTP completo inicial, incluidos metadatos y contadores. Sin ausencias ni cambios.
- Total final: 63 registros. Dos prompts originales de auditoría y una plantilla reimportada, además de los 60 ejemplos.
- La copia eliminada no se restauró: importar su plantilla creó un ID nuevo `29805093-9b06-4d62-b65b-041ae373bd4b`, revisión 1. Su historial anterior permanece accesible como snapshot separado.
- Actas: `d456e40d-f632-4686-81a6-7b7cdec52e76`, revisión 3 tras restaurar. Clasificador: `e5ab32f3-6378-430e-9f35-da23d295c69d`, revisión 2.
- SQLite generado por el producto: `backups/nova-20261023T045804-ce76e1eb93ad4d819094385dd5e5a365.sqlite`, comprobado en modo sólo lectura.
- La plantilla compartida contiene formato/versión, título, template, descripción y categoría; no contiene los valores usados ni historial.
- El panel de auditoría queda disponible en 5284. Los datos de prueba permanecen separados de la biblioteca habitual.

## Límites y siguientes pasos

No se ejecutó carga sostenida, concurrencia entre navegadores, fallos de disco/red, todas las familias semánticas, envío a servicios externos, restauración total de biblioteca, ni finalización completa de Adaptar y preparar copia. Los tests automatizados de turnos anteriores no se reejecutaron ni se contabilizaron como evidencia nueva.

La API de portapapeles del navegador de automatización devolvió su buffer virtual vacío y no pudo pegarlo. Esto impide corroborar bytes por ese canal; no prueba que `navigator.clipboard` del producto fallara. El producto mostró recibos de copia. Un timeout esperando descarga SQLite fue un supuesto erróneo del arnés: la función crea un respaldo local y mostró su nombre; el archivo se encontró y verificó. Algunos índices AX quedaron obsoletos durante cambios de vista; se renovó la observación antes de continuar. No se cuentan como fallos de lógica del producto.

La interfaz de ayuda aún contiene frases que sugieren escribir libremente a NOVA; el flujo actual exige elegir acción. Conviene alinear esos textos y ofrecer una continuación visible para aclaraciones. En el viewport estrecho, la actividad ocupa casi todo el alto y necesita desplazamiento para alcanzar el detalle del error; el fallo no debería quedar oculto detrás del diálogo preparado.

Orden de reparación recomendado: F01/F02 (integridad del ciclo de revisión y guardado), F03 (continuidad), F04 (fidelidad del artefacto), F05 (datos efectivos y proveedor). Para las skills, esta campaña demuestra que conviene exigir también una revisión humana que edita el borrador, caminos ambiguos contestables y ejecución con carga fría; un happy path de escritorio no representa esas fronteras. Son recomendaciones, no modificaciones de skills en esta auditoría.

La aceptación de 1.0.0 registrada por el usuario se conserva como hecho histórico. Estos hallazgos nuevos requieren una reparación y revalidación en su alcance; no se reescriben como PASS por disponer de una recuperación manual.

**Limitación del observador al cierre:** `report.py` rechazó el informe estructurado con «No hay una consulta identificada» para la sesión real. Se intentó recuperar mediante el lanzador, que informó que no había consulta observada para atribuir el objetivo. No se inventó un turno ni actividad. El borrador seguro permanece en `.playwright/user-audit-20261023/observer-result.json`; el cierre del dashboard queda sin confirmar. El informe de auditoría, journal, capturas y recibos del producto sí están guardados por separado.
