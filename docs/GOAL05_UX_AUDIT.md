# GOAL 05 — auditoría y diseño incremental

Baseline ejecutado: 109/109, modo normal propio sin Demo, Edge155. [Captura/mediciones](evidence/goal05/baseline/ux-baseline.json). No se consultan datos privados. La suite aprobada no invalida la fricción observada.

| Clase | Antes / causa | Después implementado y verificado | Conservación / validación |
|---|---|---|---|
| Esencial | Buscar compite con ocho entradas de sidebar y encabezados repetidos | Encabezado identidad/búsqueda/crear/configuración; biblioteca única con Todos/Favoritos/Recientes | Misma consulta SQLite paginada, atajo Ctrl+K |
| Esencial | Seleccionar → completar → Generar → Copiar; preview permanente | Campos inmediatos, Copiar compila/valida y copia; preview expandible | Compilador determinista, historial y texto exactos |
| Esencial | Cambiar plantilla reinicia valores en memoria | Borradores por identidad/revisión durante el circuito; confirmar salida de edición no guardada | Sin guardar inputs privados adicionalmente; reinicio recupera solo datos persistidos |
| Secundaria | Descripción/categoría antes del texto; modos y metadatos visibles | Título/texto primero; información adicional y editor visual expandibles | Campos, defaults, categorías/chips y versiones conservados |
| Secundaria | Grupo como sección administrativa principal y checkboxes de selección permanentes | Crear/asignar desde Organizar; filtros compactos y gestión contextual de grupos | Mismos grupos, FK, muchos-a-muchos e import/export v2 |
| Avanzada | Perfiles, ajustes, precedencia y combinaciones dentro de cada uso | Opciones avanzadas del prompt; gestión reutiliza componente existente en contexto | No borrar perfiles ni versiones ni cambiar contratos |
| Avanzada | IA y dos paneles técnicos externos siempre visibles | Usar en… abre destino contextual; Mejorar con IA discreto en opciones/editor; diagnósticos avanzados | Misma confirmación, revisión, cancelación, MV3 y fallback literal |
| Redundante visual | Sidebar/breadcrumb/hero/footer cuentan el mismo contexto | Una identidad y un título útil, lista sencilla y detalle | No eliminar servicios o entidades por retirar navegación |

Dependencias: Home orquesta estado; componentes GroupLibrary/InstructionLibrary mantienen CRUD/validación; LocalStore SQLitev2 guarda todo; TemplateCompiler/Ollama/MV3 siguen siendo límites separados. Se integran componentes existentes sin duplicar APIs ni crear rutas/pantallas nuevas. Historial, archivo y ayuda pasan a acceso secundario desde Configuración; grupos/perfiles se abren contextualmente dentro de biblioteca. No cambia esquema ni formato de backup.

Métricas baseline n=1 automatizadas por caso, excluyen arranque/tiempo humano: A4 acciones/0 navegación/160ms; B6/0/207ms; C4/0/157ms; D5/1/221ms; F6/0/587ms (crear y copiar). E/G/H/I/J no medidos en esa captura. Confirmación de campo y comprobación del clipboard incluidas en tiempo. [Comparación final A–J](GOAL05_ACCEPTANCE.md): A3, B5, C3, D4, F5 acciones, todas sin cambio de sección. Las medidas no son prueba humana ni mejora porcentual; B fue más lento en el registro final.

## Resultado observado

51→16 controles visibles en el documento seleccionado; ocho enlaces principales de sidebar→cero. La medición cuenta botones, inputs, selects, textareas y enlaces visibles, incluye los controles bajo el fold del documento y excluye disclosures plegados. No compara dos bibliotecas de tamaño arbitrario: se repite la misma búsqueda de Mensaje breve con las mismas variables. No es un índice de usabilidad.

Se consolidaron sidebar, breadcrumb, hero y footer; el encabezado mantiene identidad/búsqueda/crear/configuración. No se duplican gestores con la biblioteca: mientras un gestor contextual está abierto sustituye su contenido y se cierra para volver. Solo Copiar es primario; Usar en… secundario. La guía remite al editor para crear con IA, en lugar de duplicar un formulario. El editor muestra título/texto y detecta variables, con metadatos/chips/IA plegados.

| Captura comparable | Antes | Después |
|---|---|---|
| 1440 claro | [Imagen](evidence/goal05/baseline/selected-1440-light.png) | [Imagen](evidence/goal05/users-final/comparable-1440-light.png) |
| 390 claro | [Imagen](evidence/goal05/baseline/selected-390-light.png) | [Imagen](evidence/goal05/users-final/comparable-390-light.png) |

Inspección visual: en escritorio los campos y Copiar están junto a la lista; en móvil se muestra únicamente el detalle con Volver. Se preservan logo, azul/teal/amarillo, fuentes e iconografía SVG locales. No se añade motion decorativo; reduced motion se comprueba. El espacio del detalle sustituye los anteriores paneles permanentes de IA/composición/proveedores.

Además del cambio de jerarquía se corrigieron causas de fricción reproducidas: foco perdido tras Escape, favorito tapado por selección múltiple, composición de perfil borrado aún activa, identidad del editor tras aceptar creación IA y mejora de borrador que mutaba antes de fallar el guardado de propuesta. [Causas, FAIL originales y regresiones PASS](GOAL05_ACCEPTANCE.md).

[Geometría avanzada](evidence/goal05/accepted/goal04-experience/experience.json): 30 casos (biblioteca expandida/grupos/instrucciones × cinco anchos × dos temas), scroll y controles no tapados, alto400, ES/EN y foco. [Flujo diario](evidence/goal05/users-final/users.json): diez casos adicionales básicos, móvil lista/detalle, errores de campo y texto exacto. No se declara ausencia universal de fricción; el siguiente paso es observar tareas reales y el piloto de cinco personas, aún NOT_TESTED.
