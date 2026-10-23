# Cierre — datos funcionales del catálogo

2026-10-20. Desarrollo original del candidato wave3 conserva 14/16 recorridos
sin efectos extra: 02 pide selección innecesaria y 14 agrega REQUEST_SAVE_COPY.
No se abre validation/holdout ni se cambia ningún criterio.

Propietario: PreparationInference.ResolveReference. Los títulos y revisiones del
catálogo se enviaban sin definición, aunque la referencia se puede describir por
su función. Variante 2 de la hipótesis de información de origen: ofrecer un
extracto de hasta 600 escalares Unicode, campos reales y flag Truncated que
conserva tanto el recorte anterior como el nuevo. Nunca se trunca la petición.
El contexto total conserva su límite de 32000 bytes y rechazo explícito.

```text
para cada candidato vigente y autorizado ya ofrecido por el catálogo:
  proyectar título, revisión, campos y primeros 600 escalares de su definición
  marcar truncado si ya lo estaba o el texto supera ese límite
modelo propone un rol, sin permisos ni identidad resuelta
TaskSourceResolver valida ID/revisión/alcance y lee la fuente completa
si hay duplicados o conflicto -> aclarar/rechazar sin efectos
continuar con la petición original; no reutilizar extracto como texto de autoría
```

Invariantes: definiciones son datos no confiables; no nuevas fuentes, búsquedas,
árbitros, estado, permisos, esquemas públicos, dependencias o llamadas.
Cancelación y errores conservan sus propietarios.
Alternativas: nuevo resumidor añade latencia/deriva; nuevo índice duplica estado.
Se amplía la proyección existente, sin otra abstracción o servicio.
Verificación: entrada real HTTP con Unicode, recorte previo, ID aún no resuelto;
límites de fuente/duplicados/revisión y desarrollo Qwen original congelado.
Máximo dos variantes de esta hipótesis total; ésta es la segunda.
El caso 14 no se considera resuelto por esta corrección.
Compatibilidad/rollback: sólo proyección interna del modelo; quitarla restaura
el payload previo sin migración de datos/API o configuración productiva.

## Resultado

44 pruebas dirigidas PASS, incluidas tres reproducciones antes FAIL por falta
de datos. API/evaluador compilan y comparten hashes de dominio/aplicación/infra.
Desarrollo Qwen original: 15/16 cumplen resultado y techo de efectos; el caso 02
por función ahora encuentra el ID correcto, y los demás destinos se mantienen.
El 14 continúa con REQUEST_SAVE_COPY extra: gate FAIL y no se abre holdout.
No se certifica fidelidad de contenido ni rapidez con esta campaña de planificación.
La proyección permanece como corrección acotada de entrada, sin cerrar AI-01/02.
