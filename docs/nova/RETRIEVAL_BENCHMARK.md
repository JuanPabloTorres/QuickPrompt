# Recuperación y lecturas de origen

La recuperación actual usa SQL sobre Documents, normalización `qp_fold`, recall OR acotado, filtros y ranking determinista, con un máximo de seis candidatos. No usa FTS5/BM25. El buscador evalúa texto/categoría/grupos y cuenta resultados antes de paginar. Existen múltiples consultas por término y consultas de grupos por candidato; el conjunto materializado está acotado. No se cargan todos los cuerpos para enviarlos al modelo.

En el recorrido principal, recuperar y comprobar el origen costó milisegundos frente a decenas de segundos de inferencia. No se justifica una migración de índice ni un motor vectorial para ese cuello de botella.

Una identidad y revisión ya vinculadas no necesitan una búsqueda con favoritos, recientes y ranking. `IPromptRetriever.ReadSource` añade una lectura fresca; su implementación SQLite usa `FindTemplate`, rechaza archivados/ausentes y conserva el chequeo de revisión de SemanticPlanning. La implementación por defecto mantiene compatibilidad con adaptadores existentes. No hay caché: CRUD, archivo, restauración e importación quedan reflejados en la siguiente lectura.

Benchmark reproducible: `dotnet run --project Tests/nova/benchmarks/LatencyBenchmarks.csproj`. Crea exclusivamente un SQLite sintético con 1000 registros. Alterna 30 observaciones de la lectura anterior mediante ranking y 30 lecturas directas; excluye calentamiento. Compara identidad, revisión y documento completo. También ejecuta 30 consultas de título exacto de la biblioteca pública de ejemplos y calcula Recall@6/MRR. Ese conjunto verifica búsqueda literal, no acredita recall de paráfrasis.

La recuperación/ranking general no se cambia; no se atribuyen mejoras de calidad a una modificación inexistente.

## Resultado

Evidencia: `docs/evidence/retrieval-1792285207311/measurements.json`, 1000 documentos sintéticos, 30 observaciones alternadas por ruta tras calentamiento.

| Ruta | n | p50 ms | p95 ms | Operaciones/s de servicio |
|---|---:|---:|---:|---:|
| Origen por recuperación/ranking | 30 | 16.4916 | 24.3991 | 55.64 |
| Origen por identidad vinculada | 30 | 0.4997 | 0.7320 | 1970.22 |

Los 60 documentos leídos coinciden íntegramente, incluida revisión. En las 30 consultas de título exacto: Recall@6=1 y MRR=1. No son consultas semánticas ni medición de recall de paráfrasis. Throughput representa el inverso del tiempo de servicio medio, sin transporte HTTP ni inferencia. Los valores crudos y muestras de recursos del proceso están en el JSON; no son picos de memoria.

Working set observado: 68485120 bytes. CPU acumulada del proceso: 7296.875 ms, incluido crear el corpus; no se atribuye ese total exclusivamente a las lecturas medidas.

Las pruebas de integración comprueban lectura actualizada después de editar, archivar/restaurar, identidad ausente y revisión. No se modifica el esquema ni los índices SQLite, la paginación, filtros, categorías, permisos o ranking de búsquedas generales.
