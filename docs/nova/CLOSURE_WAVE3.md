# Cierre — identidad del catálogo y lectura vigente

2026-10-20. Evidencia previa: el recorrido original `readOnly` elige correctamente
`CATALOG_ENTRY_1 = Reescribir anuncios`; al convertirlo en otra consulta textual,
un segundo selector devuelve varios anuncios y pide una aclaración innecesaria.

Propietarios: `PreparationInference.ResolveReference` conserva la propuesta del
rol ofrecido; `TaskSourceResolver` verifica catálogo, alcance y lectura fresca
antes de crear una referencia resuelta. `ResolvedTaskReference` conserva la
ligadura a petición/revisión. No se concede identidad por un score o nombre.

Se extiende el contrato actual con `ProposedCatalogSource`, exclusivo del proceso
y excluido de JSON. No es `ResolvedTargetId`. Las propuestas ajenas, cambios de
revisión, salida de alcance y títulos duplicados no autorizan ejecución. Una
lectura válida elimina la propuesta y liga la identidad real; no se pregunta a
otro modelo para cambiar una decisión ya tomada sobre un rol concreto.

```text
rol de catálogo -> propuesta C# de ID/revisión del elemento realmente ofrecido
validar pertenencia al catálogo vigente de esta petición
leer fuente y recuperar candidatos en el mismo alcance
si duplicados de la misma identidad nominal -> pedir selección, sin inferir tarea
si falta, cambia o sale del alcance -> conflicto, sin efectos
si es único y vigente -> ligar ID/revisión leídos a la petición original
continuar interpretación del objetivo con esa referencia resuelta
```

Alternativas: otro revisor duplica la elección; resolver por palabras de la
petición añade routing frágil. La lectura verificada en el propietario actual
conserva la decisión semántica y elimina una llamada redundante. Corpus y
umbrales permanecen intactos; variante 1, máximo dos variantes justificadas.

## Resultado

42 contratos dirigidos PASS. Qwen/UI original abrir, ambigüedad e inexistente
pasan controles y revisión independiente atribuida a Codex (no piloto humano):
`docs/evidence/latency-1792492824884/acceptance.json`. Abrir el mismo destino
desde otro detalle también PASS (`latency-1792493002752/validation.json`); no
se cambió la API para un bloqueo de origen que esa prueba refutó.

Desarrollo original completo: 14/16 sin fallo de resultado/techo de efectos,
intención/destino 0,9375; 02 pide selección innecesaria y 14 solicita copia extra.
Gate FAIL conservado. Esta corrección recupera la identidad ofrecida, no certifica
la comprensión general ni el workflow. No se abrió validation/holdout.
