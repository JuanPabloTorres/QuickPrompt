# GOAL 06 — QuickPrompt Next: Simple, Smart, Connected

Actúa como arquitecto principal .NET, diseñador senior de producto, ingeniero de IA local y especialista en automatización segura con Playwright.

Proyecto: `C:\Projects\Repos\QuickPrompt-Web`.

## MISIÓN

Evolucionar QuickPrompt Web para recuperar y mejorar la experiencia original móvil:

Guardar una vez → encontrar → completar variables si existen → copiar o utilizar con un proveedor.

QuickPrompt es una biblioteca universal de prompts, no un chatbot ni una plataforma de agentes genéricos.

## 1. AUDITORÍA

Revisa el proyecto móvil original `JuanPabloTorres/QuickPrompt` y el estado actual de QuickPrompt Web.

Compara funciones reales:

- Creación y edición.
- Seleccionar texto y convertirlo en variable.
- Deshacer variables.
- Plantillas sin variables.
- Biblioteca, categorías, favoritos y filtros.
- Generación del prompt final.
- Compartir e importar/exportar JSON.
- Selección de proveedores y ejecución local.

Reproduce los flujos reales en Edge. Conserva datos, migraciones, componentes útiles y contratos aprobados.

No muevas arquitectura ni elimines código antes de identificar dependencias y establecer paridad.

## 2. UX SIMPLIFICADA

Diseña una interfaz principal con biblioteca, búsqueda y prompt seleccionado.

Elimina dashboards, paneles redundantes, configuraciones expuestas y controles decorativos.

Escritorio: lista y área de uso.\
Móvil: biblioteca y detalle secuenciales.

Crear un prompt requiere solamente título y contenido.

Usar un prompt existente nunca debe exigir entrar en una pantalla de edición.

El diseño debe ser moderno, limpio, rápido y ligeramente inspirado en JARVIS: inteligencia contextual, feedback discreto, iconografía SVG uniforme y navegación inmediata.

Mantén Tailwind y la identidad de QuickPrompt.

## 3. MOTOR DE PROMPTS

Implementa o verifica un editor robusto con:

- Selección textual.
- Crear variable.
- Deshacer variable.
- Renombrar.
- Undo/Redo.
- Marcadores repetidos.
- Plantillas sin variables.
- Validación y compilación determinista.
- Compatibilidad con la sintaxis legada.
- Texto multilineal y caracteres especiales.

Todas las acciones de destino deben utilizar el mismo CompiledPrompt inmutable.

## 4. ORGANIZACIÓN Y COMPARTIR

Conserva categorías, grupos personalizados, favoritos, filtros y búsqueda.

Permite compartir una plantilla mediante JSON portátil sin incluir datos sensibles.

Distingue exportación individual de respaldo completo.

Implementa importación con preview, validación, detección de conflictos y transacciones.

Preserva los formatos previos y la integridad SQLite.

## 5. QUICK ASSIST LOCAL

Integra Qwen3 mediante Ollama existente para:

- Crear una plantilla desde una intención.
- Mejorar una plantilla.
- Sugerir variables.
- Buscar por intención cuando aporte valor.
- Proponer organización.
- Ejecutar un prompt localmente.

No utilices el LLM para operaciones deterministas.

Usa planes tipados, validación de argumentos, permisos y límites de pasos. El usuario revisa modificaciones persistentes antes de aceptarlas.

La biblioteca debe funcionar sin Ollama.

## 6. PROVEEDORES

Diseña adaptadores separados para Ollama, ChatGPT, Claude y futuros proveedores.

Implementa:

- Copiar prompt.
- Abrir sitio seleccionado.
- Integración MV3 únicamente donde esté permitida.
- Estados reales de disponibilidad.
- Inserción verificada cuando sea viable.
- Ejecución por API oficial solamente con autorización adicional.

No automatices autenticaciones, eludas bloqueos, recopiles conversaciones ni declares una inserción exitosa sin evidencia real.

En Claude web conserva el modo manual salvo autorización aplicable.

Enviar a un proveedor es una acción separada de preparar un prompt y requiere aprobación explícita.

## 7. PLAYWRIGHT Y AUTOMATIZACIÓN

Establece dos modos:

A. QA automatizado sobre QuickPrompt Web.\
B. Navegador supervisado sobre sitios autorizados.

Para B:

- Allowlist de orígenes.
- Perfiles aislados.
- Límite de pasos y tiempo.
- Acciones tipadas.
- Capturas y verificación de estado.
- Cancelación y recuperación.
- Sin acceso libre a cookies ni secretos.
- Sin ejecución de scripts arbitrarios propuestos por el LLM.
- Confirmación humana para acciones externas sensibles.

Cuando una operación interna puede resolverse mediante servicios de dominio, no utilices Playwright para simular clics.

## 8. CRITERIOS DE ACEPTACIÓN

Ejecuta casos reales de:

1. Crear y guardar prompt sin variables.
2. Crear y deshacer variables.
3. Reutilizar prompt con valores diferentes.
4. Buscar por título, texto, categoría y grupo.
5. Favoritos y recientes.
6. Importar y compartir JSON.
7. Ejecutar Qwen3 con streaming.
8. Mejorar y aprobar/rechazar propuestas.
9. Usar copiar/abrir proveedor externo.
10. Automatización Playwright en la aplicación propia.
11. Persistencia y recuperación tras reinicio.
12. Ausencia de Ollama.
13. UI en cinco tamaños y dos temas.
14. Navegación con teclado y validaciones de accesibilidad.

Mantén la suite previa y amplíala con regresiones de defectos encontrados.

Evalúa el flujo con usuarios reales cuando sea posible; no confundas pruebas automatizadas con aceptación humana.

Objetivos propuestos:

- Copiar un prompt sin variables en menos de 10 segundos.
- Completar dos variables y copiar en menos de 20 segundos.
- Cero pérdidas de datos y envíos externos no autorizados.
- Sin controles superpuestos en viewports evaluados.

## 9. ENTREGA

Trabaja incrementalmente:

- Auditoría y baseline.
- UX y editor.
- Biblioteca y datos.
- Quick Assist.
- Proveedores y extensión.
- Playwright y aceptación integral.

Documenta requisitos, decisiones, arquitectura, cambios, evidencias, resultados, regresiones y bloqueos.

No instales SDK, paquetes, modelos, servicios pagos ni publiques cambios sin autorización.

No elimines ni reemplaces SQLite ni alteres datos privados del usuario.

Distingue PASS, FAIL, BLOCKED y NOT_TESTED.

Comienza por auditar el flujo móvil original y verificarlo contra la web existente. Prioriza el flujo principal completo antes de implementar automatización avanzada.

El producto se considerará funcional cuando cualquier persona pueda guardar y reutilizar sus prompts rápidamente, con o sin variables, sin depender de un modelo ni navegar múltiples pantallas.
