# Datos, migración y rollback

No se leyó ni migró la biblioteca privada original durante el desarrollo. Todas las pruebas nuevas utilizaron datos sintéticos en SQLite real. La aceptación sobre datos reales permanece `BLOCKED` hasta autorización explícita del usuario.

## Ruta de migración disponible

1. Desde la web anterior, exportar el respaldo completo JSON. Conservarlo fuera del repo y no compartirlo: contiene contenido e historial privados.
2. Abrir NOVA con su biblioteca nueva independiente.
3. En **Datos y respaldos**, elegir el archivo y revisar la importación. Importar es aditivo; no sobrescribe identidades existentes en conflicto.
4. Para reemplazar toda la biblioteca, marcar restauración y seleccionar un respaldo JSON v1/v2. Revisar y confirmar expresamente. La API compara el fingerprint de la biblioteca con el de revisión y crea/verifica un backup SQLite antes de reemplazar atómicamente.
5. Reiniciar y comparar prompts, variables/defaults, favoritos, grupos, perfiles, revisiones, compilados y resultados. Esta comparación de datos privados todavía no está ejecutada.

También se acepta un SQLite MAUI legado elegido explícitamente como archivo (máx. 4 MB), convertido desde una copia temporal en modo read-only. Se admiten los esquemas históricos `PromptTemplate`/`PromptTemplateDTO` descritos en los tests existentes. El SQLite de la web actual usa `Documents`; migrarlo mediante JSON exportado, no mediante el conversor MAUI. No utilizar `QuickPrompt.db3` como DataPath: la guardia nueva rechaza bases no compatibles antes de inicializar tablas.

## Compatibilidad

Se conservan identidad Guid, fechas, categorías enum, favorito, archivado, defaults, revisiones y relaciones. El esquema web continúa en v2; se preserva la migración v1→v2. El formato de respaldo sigue siendo Archive v2 y continúa leyendo v1 y JSON históricos. La propuesta de sintaxis nueva no se implementó: se conserva `<nombre>` y los escapes `\<`, `\>` y `\\`.

Los valores vacíos siguen las reglas del parser legado validado: sin default válido se consideran pendientes. No existen aún definiciones explícitas de variables opcionales; no se inventó una sintaxis ni se modificó silenciosamente ese comportamiento.

Los recibos `nova-use` son metadatos operativos locales. Se conservan en un respaldo físico SQLite y entre reinicios; Archive v2 no los transporta. Después de restaurar un JSON, no reutilizar operationIds anteriores como garantía de deduplicación entre bibliotecas restauradas. Las plantillas, contadores e historial sí forman parte del respaldo JSON.

## Respaldos

**Crear respaldo SQLite** usa `BackupDatabase`, un nombre generado por la aplicación en el directorio `backups` junto a la DB configurada y `PRAGMA integrity_check`. El cliente no puede especificar rutas arbitrarias. Exportar JSON también produce un snapshot consistente mediante una transacción de lectura.

La prueba de restauración verifica que el respaldo anterior conserva dos prompts mientras la biblioteca restaurada vuelve a uno. Fallos de validación, confirmación ausente o snapshot desactualizado no reemplazan datos.

## Rollback

Detener NOVA y volver a iniciar la web anterior con su configuración habitual. Sus fuentes y datos originales siguen disponibles; no hubo cambio de puerto ni configuración en esa instancia. Si se requiere recuperar la biblioteca NOVA, conservar su archivo actual y abrir una copia de un respaldo SQLite verificado mediante `-DataPath`, o restaurar un JSON revisado desde la UI.

No cambiar de rama sobre el árbol sin commit como procedimiento automático de rollback. No eliminar carpetas legacy ni respaldos. El retiro de Blazor exige una aceptación de paridad posterior y un cambio separado.
