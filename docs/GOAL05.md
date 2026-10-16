# GOAL 05 — QuickPrompt Web: Back to Simplicity

## MISIÓN

Actúa como arquitecto de software, diseñador senior de producto, especialista en UX/UI y desarrollador Blazor/Tailwind.

Proyecto: `C:\Projects\Repos\QuickPrompt-Web`

El usuario considera que QuickPrompt se ha vuelto demasiado complejo y se ha alejado de su propósito original.

**Debemos simplificar radicalmente la experiencia sin perder datos ni destruir funcionalidades válidas.**

QuickPrompt NO es un chatbot, un dashboard de IA, un editor avanzado de instrucciones ni una plataforma de agentes.

QuickPrompt es una biblioteca inteligente para crear, guardar, encontrar, completar y reutilizar prompts que las personas utilizan frecuentemente.

Su propuesta de valor principal:

**Guardar una vez. Encontrar rápido. Completar variables. Utilizar inmediatamente.**

Un prompt puede servir para cualquier ámbito: programación, trabajo, estudios, escritura, investigación, uso personal o cualquier otra necesidad.

No diseñes la aplicación alrededor de casos específicos como programación o Codex. El dominio debe ser completamente genérico.

## 1. REVISIÓN DEL PRODUCTO EXISTENTE

Antes de modificar código:

1. Inspecciona las pantallas, componentes, servicios, navegación, CSS y estado actual.
2. Ejecuta el baseline de pruebas. La entrega anterior reportó 109/109; compruébalo realmente.
3. Inicia la web en modo normal, sin Demo.
4. Abre cada pantalla en Edge y documenta qué controles y pasos necesita un usuario para reutilizar un prompt.
5. Identifica navegación innecesaria, opciones duplicadas, controles excesivos, formularios demasiado largos y menús confusos.
6. Clasifica las funciones en: esenciales, secundarias, avanzadas y redundantes.
7. Conserva SQLite v2, migraciones, respaldos, categorías, grupos, perfiles, versiones, integración Ollama y extensión MV3.
8. No borres código ni entidades sin revisar referencias, datos, contratos y pruebas.

Produce una matriz funcional antes/después y evidencia visual del flujo existente.

**No uses una prueba automatizada aprobada como excusa para ignorar el problema de usabilidad reportado por el usuario.**

## 2. NUEVA ESTRUCTURA DE LA APLICACIÓN

Elimina el enfoque de múltiples pantallas principales.

La experiencia cotidiana debe concentrarse en UNA pantalla: **Biblioteca de prompts**.

### Encabezado

Mostrar únicamente:

- Identidad QuickPrompt.
- Búsqueda global.
- Crear nuevo prompt.
- Configuración mediante un control discreto.

No presentar un dashboard inicial ni estadísticas innecesarias.

### Biblioteca

Mostrar una lista sencilla de prompts con:

- Título.
- Descripción breve, si aporta contexto.
- Indicador de favorito.
- Acceso para seleccionar.

La cantidad de variables o el grupo pueden mostrarse como metadatos secundarios, sin saturar cada tarjeta.

La búsqueda debe ser el mecanismo principal para recuperar contenido.

Filtros de acceso rápido:

- Todos.
- Favoritos.
- Recientes.

Incluir selector opcional de categoría y grupo dentro de un único control compacto de filtros.

No mostrar todas las categorías y grupos simultáneamente en una barra saturada.

### Prompt seleccionado

Cuando el usuario selecciona un prompt, mostrar en el área principal:

1. Título.
2. Descripción breve opcional.
3. Campos de variables detectadas automáticamente.
4. Acción primaria: Copiar prompt.
5. Acción secundaria: Usar en…
6. Menú discreto: Editar, Duplicar, Favorito, Organizar y otras acciones.

No navegar a otra pantalla solamente para completar variables.

El usuario debe poder cambiar entre prompts sin perder accidentalmente los cambios pendientes.

### Responsive

En escritorio:

- Lista de prompts a la izquierda.
- Prompt seleccionado a la derecha.

En móvil:

- Biblioteca como vista inicial.
- Selección abre el detalle en el mismo flujo de navegación.
- Regreso claro a la biblioteca.
- Acciones principales accesibles.

Evita paneles triples, dashboards, sidebars anidados y modales innecesarios.

## 3. REUTILIZACIÓN DE PROMPTS COMO FLUJO CENTRAL

Implementa un flujo determinista y robusto:

`Seleccionar → Completar variables → Compilar → Copiar/Usar`

El sistema debe soportar:

- Prompts sin variables.
- Prompts con una variable.
- Prompts con muchas variables.
- Variables opcionales y obligatorias, si el esquema las distingue.
- Valores predeterminados.
- Valores reutilizables autorizados.
- Variables repetidas en diferentes partes de la plantilla.
- Textos largos.
- Caracteres especiales.
- Contenido multilínea.

Usa el compilador determinista existente, mejorándolo únicamente donde haya defectos demostrados.

No uses un LLM para reemplazar variables.

Muestra campos generados automáticamente a partir de la plantilla.

Cuando un campo obligatorio esté vacío, señala cuál falta y enfócalo.

El usuario no debe necesitar abrir un editor avanzado para utilizar una plantilla existente.

El texto final debe ser exactamente la combinación esperada de plantilla y variables.

No modifiques la plantilla original al completar variables.

La previsualización completa será expandible, no un panel permanentemente visible ocupando espacio.

La acción primaria Copiar debe permanecer claramente identificada.

## 4. CREACIÓN Y EDICIÓN SIMPLIFICADAS

Crear un prompt debe requerir únicamente:

- Título.
- Texto de plantilla.

Descripción, categoría, grupos y configuraciones adicionales deben ser opcionales.

Utiliza un editor simple que permita escribir variables con la sintaxis existente.

Detecta y muestra automáticamente las variables.

El editor visual/chips puede permanecer como una alternativa discreta, no como una interfaz compleja con múltiples modos visibles todo el tiempo.

Al guardar:

- Validar.
- Persistir.
- Mostrar confirmación breve.
- Regresar al uso normal o conservar el prompt seleccionado.

Permitir edición posterior sin obligar a completar todos los metadatos.

Evitar formularios extensos y pasos tipo wizard.

## 5. GRUPOS Y CATEGORÍAS SIN COMPLEJIDAD

Conservar grupos personalizados y categorías porque son útiles para organizar muchos prompts.

Sin embargo:

**La organización nunca debe complicar la reutilización.**

Implementar un único mecanismo de organización visible cuando sea necesario.

Los grupos existentes deben conservar sus relaciones muchos-a-muchos.

Permitir:

- Crear un grupo desde el selector correspondiente.
- Asignar un prompt a uno o varios grupos.
- Filtrar por grupo.
- Renombrar o eliminar un grupo desde un menú secundario.

No crear pantallas administrativas separadas para operaciones simples.

No obligar a categorizar cada prompt.

Mantener la categoría original como metadato opcional.

Eliminar un grupo no debe eliminar sus prompts.

Conservar import/export, migraciones, integridad y respaldos.

## 6. INSTRUCCIONES REUTILIZABLES SIN EXPONER COMPLEJIDAD

GOAL 04 añadió perfiles de instrucciones.

No elimines esos datos ni su funcionalidad.

Pero no hagas que el usuario deba gestionar perfiles para utilizar un prompt.

La composición con instrucciones debe ser completamente opcional.

Dentro de las opciones avanzadas del prompt, permitir seleccionar una instrucción guardada.

Si no se selecciona ninguna, el prompt se compila normalmente.

No presentar constantemente controles de precedencia, perfiles, versiones o composición en el flujo básico.

Las instrucciones y sus versiones deben continuar siendo accesibles desde una sección avanzada o un selector contextual.

Mantener compatibilidad de datos y contratos existentes.

## 7. INTEGRACIÓN DE IA COMO FUNCIÓN SECUNDARIA

La aplicación debe seguir siendo completamente útil sin Ollama.

Mantener:

- Qwen3 local.
- Mejorar prompt.
- Sugerir variables.
- Generar una plantilla desde una descripción.
- Ejecutar una consulta.

Pero agrupar estas capacidades de forma lógica.

No presentar seis botones de IA en la pantalla principal.

En el editor puede existir una acción discreta: **Mejorar con IA**.

En la plantilla seleccionada puede existir **Usar en…** para elegir el destino.

Cuando el usuario elige Qwen local:

- Compilar el prompt.
- Ejecutar en Ollama.
- Mostrar streaming y resultado en un panel contextual sencillo.
- Permitir detener, copiar y guardar.
- Poder cerrar el resultado y continuar usando la biblioteca.

No crear una interfaz de chatbot permanente.

Las opciones avanzadas de IA deben permanecer ocultas hasta que el usuario las necesite.

## 8. CHATGPT Y CLAUDE

Mantén la integración y los controles de seguridad existentes.

La experiencia visible debe ser sencilla:

**Usar en…**
- Qwen local.
- ChatGPT.
- Claude.

La aplicación debe generar primero el prompt definitivo.

Nunca transferir plantillas con variables pendientes sin avisar.

ChatGPT y Claude:

- Intentar integración directa únicamente cuando esté permitida y disponible.
- No asumir que funciona porque los fixtures pasan.
- No automatizar autenticación ni evadir restricciones.
- Cuando no esté disponible, copiar el texto exacto y abrir el sitio.
- Indicar claramente que el usuario deberá pegarlo manualmente.
- No registrar un envío que no ocurrió.

No dedicar una pantalla completa a los detalles del puente MV3.

Los diagnósticos técnicos deben quedar en Configuración avanzada.

## 9. ELIMINAR RUIDO VISUAL

Revisar y simplificar:

- Cantidad de botones visibles.
- Cantidad de tarjetas.
- Encabezados repetidos.
- Bordes y sombras.
- Colores de acento.
- Badges decorativos.
- Información técnica.
- Iconos innecesarios.
- Tooltips redundantes.
- Menús demasiado largos.
- Modales evitables.
- Animaciones.
- Paneles de estado.

Conservar Tailwind CSS 4.3.0 y la identidad de QuickPrompt.

Usar una jerarquía de acciones consistente:

1. Una acción primaria clara.
2. Una acción secundaria contextual.
3. Acciones restantes en menús de opciones.

No convertir todo en botones del mismo nivel visual.

No mostrar etiquetas técnicas como nombres de clases, contratos o estados internos.

Usar iconos SVG uniformes junto con texto cuando sea necesario.

Priorizar espacios en blanco, alineación, tipografía legible y una distribución calmada.

Mostrar loading únicamente en la región que está trabajando; no bloquear toda la pantalla salvo que sea necesario.

Proporcionar feedback de guardado, copia o error sin abrir diálogos innecesarios.

## 10. SIMPLIFICAR LA NAVEGACIÓN

El uso diario no debe requerir recorrer secciones como Dashboard, Composer, Instructions, Groups, AI, History y Providers.

Reorganiza estas funcionalidades mediante divulgación progresiva:

**Nivel principal:** biblioteca y uso de prompts.

**Nivel contextual:** edición, organización, versiones e instrucciones opcionales.

**Nivel avanzado:** configuración de modelos, extensiones, respaldos y diagnósticos.

Conservar las rutas y funcionalidades heredadas cuando sea necesario para compatibilidad, pero evitar enlaces principales hacia pantallas redundantes.

Antes de retirar una pantalla, inventariar sus dependencias y redirigir o integrar sus acciones sin pérdida de datos.

No mantener dos interfaces independientes para realizar la misma operación.

## 11. PRUEBAS E2E CENTRADAS EN USUARIOS

Las pruebas deben comprobar el valor del producto y no solamente que se rendericen componentes.

Caso A: prompt sin variables.

Abrir → encontrar → seleccionar → copiar.

Caso B: prompt con variables.

Buscar → seleccionar → completar → copiar → verificar texto exacto.

Caso C: reutilización frecuente.

Utilizar el mismo prompt varias veces con valores diferentes sin modificar el original.

Caso D: favorito.

Marcar favorito → localizar rápidamente → utilizar.

Caso E: grupos.

Crear grupo → asignar prompt → filtrar → utilizar → eliminar grupo → comprobar que prompt permanece.

Caso F: creación.

Crear plantilla desde título y texto → guardar → reutilizar inmediatamente.

Caso G: IA opcional.

Mejorar con Qwen3 → revisar propuesta → rechazar o aceptar → comprobar versiones.

Caso H: ejecución.

Seleccionar prompt → completar → usar Qwen3 → respuesta real → copiar/guardar → cerrar.

Caso I: proveedor externo.

Seleccionar → compilar → elegir ChatGPT/Claude → verificar integración autorizada o fallback exacto.

Caso J: independencia.

Apagar o simular inaccesibilidad de Ollama → buscar, crear, completar y copiar sin problemas.

Validar teclado, navegador Edge, ambos temas, ES/EN, 320/390/768/1024/1440 px y ventanas de altura reducida.

Comprobar geometría, scroll, overlays, foco y ausencia de controles tapados.

Añadir regresiones para cada defecto reproducido.

## 12. CRITERIOS DE ACEPTACIÓN

No considerar terminado hasta verificar:

1. El flujo principal está disponible desde una única pantalla.
2. Un prompt sin variables se puede encontrar y copiar sin entrar en un editor ni atravesar configuraciones.
3. Un prompt con variables muestra los campos directamente tras seleccionarlo.
4. La acción principal es evidente sin necesidad de aprender la aplicación.
5. Crear un prompt nuevo requiere únicamente título y plantilla.
6. Categorías, grupos, IA y perfiles son opcionales.
7. Ninguna capacidad avanzada interrumpe el flujo básico.
8. No hay superposiciones, overflow inesperado ni controles inaccesibles en los tamaños evaluados.
9. Los datos y relaciones existentes permanecen intactos.
10. El flujo Qwen local y los fallbacks externos siguen funcionando conforme a sus límites comprobados.
11. La suite previa y las nuevas regresiones pasan sin reducir cobertura ni modificar evidencias anteriores.
12. El resultado final es visualmente más simple, no solamente distinto.

Registrar para los escenarios A–J número de acciones, navegación necesaria y tiempo observado. Comparar contra baseline cuando pueda reproducirse.

No inventar mejoras porcentuales ni afirmar que la usabilidad fue validada por personas si solamente hubo automatización.

## 13. DOCUMENTACIÓN Y ENTREGA

Actualizar la documentación existente y producir un informe final de GOAL 05 con:

- Auditoría UX antes/después.
- Pantallas y controles eliminados o consolidados.
- Funcionalidades preservadas.
- Capturas comparables en Edge.
- Matriz de paridad.
- Pruebas ejecutadas.
- Resultados E2E.
- Evidencia SQLite.
- Riesgos y bloqueos pendientes.
- Comandos de reproducción.

Usar resultados `PASS`, `FAIL`, `BLOCKED` y `NOT_TESTED`.

No instalar software, descargar modelos, publicar cambios o acceder a datos privados sin autorización.

No marcar proveedores externos como funcionales si los sitios auténticos no pudieron validarse.

## 14. ORDEN DE IMPLEMENTACIÓN

1. Auditar UX y flujos actuales.
2. Diseñar estructura simplificada sobre el sistema existente.
3. Consolidar navegación y biblioteca.
4. Integrar variables y acciones principales en una única experiencia.
5. Simplificar creación y edición.
6. Ocultar progresivamente funciones avanzadas sin perderlas.
7. Validar grupos, Qwen y proveedores.
8. Ejecutar regresión integral.
9. Revisar capturas y corregir fricción visual real.
10. Entregar evidencia de aceptación.

**No crees más pantallas para resolver este objetivo. La meta es reducir la cantidad de decisiones, controles y pasos necesarios para utilizar QuickPrompt.**

Comienza ahora con el flujo más importante: buscar un prompt, completar sus variables y copiarlo. Ese recorrido debe determinar el diseño de toda la aplicación.