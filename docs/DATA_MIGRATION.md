# Migración y recuperación

Nunca usar la DB versionada como seed. La nueva DB se crea vacía fuera del repositorio; `--QuickPrompt:Demo=true` agrega únicamente tres ejemplos ficticios si está vacía.

Desde Configuración:

1. Exportar / respaldo JSON descarga un archivo v1 completo. Es privado: contiene textos, valores opt-in, versiones, propuestas, resultados y preferencias.
2. Importar acepta respaldo v1, objeto JSON legado Title/Template/Description/VariablesJson, arrays de plantillas o DB SQLite seleccionada. Agrega; no sobrescribe identidades/contenido ya existente. Repetir es idempotente. Las propuestas importadas pendientes quedan rechazadas: importar no autoriza aplicarlas.
3. Restaurar respaldo JSON solicita confirmación, valida toda la estructura y reemplaza atómicamente. Exportar estado actual antes. Un payload corrupto o de versión desconocida conserva la DB actual. Comprobar consistencia ejecuta PRAGMA integrity_check.

Compatibilidad DB: tablas reales PromptTemplate o PromptTemplateDTO, Guid, categoría enum legada 0..28, VariablesJson, favorito, estado activo/borrado y fechas ticks UTC o texto reconocible. Se preservan IDs y fechas; nombres de variable Unicode, espacios y /n. FinalPrompt migra texto/favorito/fecha/vínculo a compilados. ExecutionHistoryEntry no contiene respuestas de modelo: PromptCompiled se migra como texto finalizado independiente; no se inventan resultados. DeviceId/sync/cloud no se transfieren. Límites y esquemas incompatibles fallan explícitamente; no se reparan silenciosamente datos del origen.

JSON legado sin metadatos recibe Guid/fecha nuevos y categoría General; el formato original de compartir no exportaba categoría ni favorito, por lo que no pueden inferirse. VariablesJson legado se usa como valores por defecto cuando sea compatible; el parser vuelve a verificar nombres contra el texto.

Sintaxis: `<nombre>`; nombre inicia letra/número/_ y admite letras Unicode BMP, marcas combinantes, números, espacios internos, `_ . - /`; máximo 80; comparación ordinal y sensible a mayúsculas, sin normalización silenciosa. Repetidos comparten campo. `\<`, `\>` y `\\` producen literales. Marcadores vacíos, nombres inválidos o sin cierre se rechazan. Para código/HTML literal escapar los ángulos. Valores multilinea y especiales no se retokenizan ni ejecutan; faltantes usan defaults o bloquean generación.

Pruebas usan DB sintética y comparan bytes de origen antes/después. No se afirma compatibilidad verificada con los registros privados de QuickPrompt.db3; no se inspeccionaron.

## GOAL06: intercambio individual y revisión previa

El respaldo vigente es Archivev2 privado; se siguen leyendo Archivev1 y los JSON históricos. Compartir plantilla genera `{"format":"quickprompt-template","formatVersion":1,"prompt":{...}}` con título, texto, descripción y categoría. No lleva IDs, defaults, historial, versiones, relaciones ni preferencias. Una importación portátil crea identidad nueva; repetir título/texto exactos se detecta como duplicado. El formato portátil no se acepta para restaurar toda la biblioteca.

Al seleccionar un archivo se valida íntegramente y se muestra preview, sin escritura. Cancelar/Escape no importa. Confirmar vuelve a validar la misma cadena y ejecuta la transacción existente. Conflictos por ID con contenido diferente se señalan y omiten en importación aditiva; restaurar muestra advertencia y solicita confirmar el reemplazo. Solo se muestran100 filas, pero se validan/procesan todas dentro de los límites existentes. Se conservan migraciones/SQLitev2; no se creó schema nuevo. [Pruebas y límites](GOAL06_ACCEPTANCE.md).
