# UNIFIED INJECTION STRATEGY - FINAL IMPLEMENTATION

## Cambios Realizados

### 1. WebViewInjectionService.cs - BuildPersistentInjectionScript()

**Estrategia Unificada:**
```
Polling Input (15 × 200ms) ? Native Setter ? Full Events ? Polling Submit (10 × 200ms)
```

#### A. Polling de Input (Step 1)
```javascript
// 15 intentos × 200ms = 3 segundos max
// Selectores ordenados por especificidad:
// 1. Selector del descriptor (específico del proveedor)
// 2. GPT: #prompt-textarea
// 3. Patterns comunes: textarea[placeholder*="Ask"]
// 4. Genéricos: textarea, [contenteditable="true"]
// 5. ARIA: [role="textbox"]

// Validación: elemento existe Y es visible (offsetWidth/Height > 0)
```

**Por qué:**
- DOM tarda en cargar (especialmente Gemini/Copilot)
- Un solo intento falla si página aún está cargando
- Múltiples selectores = resiliente a cambios de UI

#### B. Native Value Setter (Step 2)
```javascript
const nativeSetter = Object.getOwnPropertyDescriptor(
    window.HTMLTextAreaElement.prototype, 'value'
).set;
nativeSetter.call(input, prompt);
```

**Por qué:**
- React/Vue interceptan `input.value = X` con getters/setters
- Llamar al setter nativo directamente bypasea la interceptación
- Frameworks detectan el cambio correctamente

#### C. Full Event Simulation (Step 2 continuación)
```javascript
// Orden crítico:
// 1. focus, focusin
// 2. compositionstart, compositionupdate, compositionend (IME simulation)
// 3. keydown, keypress, keyup (keyboard simulation)
// 4. input (primary change detection)
// 5. change (validation trigger)
```

**Por qué cada evento:**

| Evento | Para qué Framework | Por qué es crítico |
|--------|-------------------|-------------------|
| `focus` | Todos | Activa event listeners |
| `compositionstart/end` | React, Vue | Detecta entrada de texto IME |
| `keydown/keyup` | Grok, Copilot | Habilita botones (validación on-keypress) |
| `input` | Todos | Detección de cambio principal |
| `change` | Angular, formularios | Trigger final de validación |

**Sin estos eventos:**
- ? Grok: Botón submit permanece disabled
- ? Gemini: Framework no detecta cambio
- ? Copilot: Validación no se dispara

#### D. Submit Button Polling (Step 3)
```javascript
// 10 intentos × 200ms = 2 segundos max
// Espera a que botón se habilite después de validación

// Selectores intentados:
// 1. Descriptor del proveedor
// 2. GPT: button[data-testid="send-button"]
// 3. Standard: button[type="submit"]:not([disabled])
// 4. ARIA: button[aria-label*="Send"]:not([disabled])
// 5. Fallback: Buscar buttons con SVG + "send" en aria-label
```

**Por qué:**
- Validación de frameworks es asíncrona
- Botón puede estar disabled hasta que validación pase
- Esperar hasta 2 segundos antes de abandonar

### 2. AiEngineRegistry.cs - Delays y Selectores

#### Cambios:

| Proveedor | Selector Antes | Selector Después | Delay Antes | Delay Después |
|-----------|----------------|------------------|-------------|---------------|
| **GPT** | `#prompt-textarea` | Sin cambios ? | 2000ms | Sin cambios ? |
| **Grok** | `textarea[placeholder*='Ask']` | Sin cambios | 2000ms | Sin cambios |
| **Gemini** | `div[contenteditable='true'][aria-label*='Enter']` | `[contenteditable='true']` | 3000ms | **4000ms** |
| **Copilot** | `textarea[placeholder*='Ask']` ? `textarea` | Sin cambios | 3500ms | **4000ms** |

**Razonamiento:**

**Gemini:**
- Selector simplificado: `aria-label` puede no existir o cambiar
- Delay aumentado: Página muy compleja con AJAX pesado

**Copilot:**
- Delay aumentado: Similar a Gemini, página compleja

**GPT/Grok:**
- Sin cambios: Ya funcionan correctamente
- No tocar lo que funciona (principio defensivo)

---

## Flujo Completo por Proveedor

### ? GPT (Sin cambios - Baseline)
```
Wait 2000ms
    ?
Find: #prompt-textarea (attempt 1) ?
    ?
Set value with native setter
    ?
Dispatch all events
    ?
Find submit: button[data-testid="send-button"] (attempt 1) ?
    ?
Click submit
    ?
SUCCESS in ~2.5 seconds
```

### ?? Grok (Mejora: Full Events)
```
Wait 2000ms
    ?
Find: textarea[placeholder*='Ask'] (attempt 1-3) ?
    ?
Set value with native setter
    ?
Dispatch composition events ? CRÍTICO
    ?
Dispatch keyboard events ? CRÍTICO (habilita botón)
    ?
Dispatch input/change events
    ?
Wait 500ms for validation
    ?
Poll submit button (attempts 1-5)
    ?
Find enabled: button[type='submit'] ?
    ?
Click submit
    ?
SUCCESS in ~3 seconds
```

**Mejora clave:** Eventos de teclado disparan validación ? botón se habilita

### ??? Gemini (Fix: Polling + Simplified Selector)
```
Wait 4000ms ? Aumentado
    ?
Poll input (attempts 1-8) ? Aumentado de 3000ms
    ?
Find: [contenteditable='true'] (attempt 3-8) ?
    ?
Set textContent/innerText
    ?
Dispatch all events
    ?
Wait 500ms
    ?
Poll submit (attempts 1-5)
    ?
Find: button[aria-label*='Send'] ?
    ?
Click submit
    ?
SUCCESS in ~5-6 seconds
```

**Mejora clave:** 
- Selector simple sin aria-label específico
- Delay 4000ms da tiempo suficiente
- Polling encuentra elemento cuando esté listo

### ??? Copilot (Fix: Increased Delay + Polling)
```
Wait 4000ms ? Aumentado de 3500ms
    ?
Poll input (attempts 1-10)
    ?
Find: textarea (attempt 5-10) ?
    ?
Set value with native setter
    ?
Dispatch all events
    ?
Wait 500ms
    ?
Poll submit (attempts 1-8)
    ?
Find: button[type='submit'] ?
    ?
Click submit
    ?
SUCCESS in ~5-7 seconds
```

**Mejora clave:** 
- Delay 4000ms suficiente para carga completa
- Polling compensa tiempos variables de carga

---

## Verificación de Éxito

### Debug Output Esperado:

#### ? GPT (debe seguir funcionando):
```
[QuickPrompt] ===== INJECTION START =====
[QuickPrompt] Provider: ChatGPT
[QuickPrompt] [Input Search] Attempt 1 / 15
[QuickPrompt] [Input Search] FOUND with selector: #prompt-textarea
[QuickPrompt] [Injection] Type: TEXTAREA/INPUT
[QuickPrompt] [Injection] Used native setter
[QuickPrompt] [Injection] All events dispatched for TEXTAREA/INPUT
[QuickPrompt] [Submit Search] Attempt 1 / 10
[QuickPrompt] [Submit Search] FOUND enabled button with: button[data-testid="send-button"]
[QuickPrompt] [Submit Search] Submit clicked successfully
[QuickPrompt] ===== INJECTION SUCCESS =====
```

#### ? Grok (botón debe habilitarse):
```
[QuickPrompt] ===== INJECTION START =====
[QuickPrompt] Provider: Grok
[QuickPrompt] [Input Search] Attempt 1-2 / 15
[QuickPrompt] [Input Search] FOUND with selector: textarea[placeholder*="Ask"]
[QuickPrompt] [Injection] Type: TEXTAREA/INPUT
[QuickPrompt] [Injection] All events dispatched for TEXTAREA/INPUT
[QuickPrompt] [Submit Search] Attempt 1-3 / 10  ? Puede tardar
[QuickPrompt] [Submit Search] FOUND enabled button  ? Debe encontrarlo
[QuickPrompt] ===== INJECTION SUCCESS =====
```

#### ? Gemini (debe encontrar contenteditable):
```
[QuickPrompt] ===== INJECTION START =====
[QuickPrompt] Provider: Gemini
[QuickPrompt] [Input Search] Attempt 3-8 / 15  ? Puede tardar
[QuickPrompt] [Input Search] FOUND with selector: [contenteditable="true"]
[QuickPrompt] [Injection] Type: CONTENTEDITABLE
[QuickPrompt] [Injection] Content set, textContent length: 245
[QuickPrompt] [Submit Search] FOUND enabled button
[QuickPrompt] ===== INJECTION SUCCESS =====
```

#### ? Copilot (debe insertar):
```
[QuickPrompt] ===== INJECTION START =====
[QuickPrompt] Provider: Copilot
[QuickPrompt] [Input Search] Attempt 5-10 / 15  ? Puede tardar
[QuickPrompt] [Input Search] FOUND with selector: textarea
[QuickPrompt] [Injection] Type: TEXTAREA/INPUT
[QuickPrompt] [Submit Search] FOUND enabled button
[QuickPrompt] ===== INJECTION SUCCESS =====
```

### ? Si Falla (Clipboard Fallback):
```
[QuickPrompt] [Input Search] FAILED after 15 attempts  ? Input no encontrado
[Injection] Script result: error:Input element not found
[Injection] Falling back to clipboard
```

**Acción:** Revisar selector específico del proveedor en Chrome DevTools

---

## Testing Checklist

### Pre-Test:
- [ ] **Build exitoso** (sin errores de compilación)
- [ ] **Hot reload deshabilitado** (restart completo de app)
- [ ] **Debug Output visible** (ventana Output en Visual Studio)

### Test por Proveedor:

#### GPT (Baseline - NO debe romperse):
- [ ] Navegar a GPT
- [ ] Prompt se inserta en ~2 segundos
- [ ] Submit se clickea automáticamente
- [ ] Logs muestran "INJECTION SUCCESS"
- [ ] **Tiempo total: < 3 segundos**

#### Grok (Fix: Eventos completos):
- [ ] Navegar a Grok
- [ ] Prompt se inserta
- [ ] **Botón submit se habilita** ? Clave
- [ ] Submit se clickea automáticamente
- [ ] **Tiempo total: 3-4 segundos**

#### Gemini (Fix: Polling + Selector simple):
- [ ] Navegar a Gemini
- [ ] Esperar 4 segundos (delay aumentado)
- [ ] Prompt se inserta en contenteditable
- [ ] Submit se clickea
- [ ] **NO cae a clipboard** ? Clave
- [ ] **Tiempo total: 5-7 segundos**

#### Copilot (Fix: Delay aumentado):
- [ ] Navegar a Copilot
- [ ] Esperar 4 segundos
- [ ] Prompt se inserta en textarea
- [ ] Submit se clickea
- [ ] **NO cae a clipboard** ? Clave
- [ ] **Tiempo total: 5-7 segundos**

### Post-Test:
- [ ] Ningún proveedor cae a clipboard (salvo tras 3 retries fallidos)
- [ ] Logs coherentes sin errores JavaScript
- [ ] Tiempos aceptables (< 8 segundos por proveedor)

---

## Troubleshooting

### Problema: "Input element not found after 15 attempts"

**Diagnóstico:**
```javascript
// En Chrome DevTools console de la página del proveedor:
document.querySelectorAll('textarea').length
document.querySelectorAll('[contenteditable="true"]').length
```

**Si retorna 0:** Proveedor cambió UI radicalmente
**Si retorna > 0:** Selector está mal o elemento está en Shadow DOM/iframe

**Solución:**
```javascript
// Encontrar selector correcto:
var inputs = document.querySelectorAll('textarea, [contenteditable="true"]');
for (let i = 0; i < inputs.length; i++) {
    console.log(i, inputs[i], 'visible:', inputs[i].offsetWidth > 0);
}
// Actualizar selector en AiEngineRegistry.cs con el correcto
```

### Problema: "Button not found or not enabled after 10 attempts"

**Diagnóstico:**
```javascript
// En Chrome console:
document.querySelectorAll('button').length
// Buscar cuál es el botón de submit:
var buttons = document.querySelectorAll('button');
for (let btn of buttons) {
    console.log(btn, 'disabled:', btn.disabled, 'aria:', btn.getAttribute('aria-label'));
}
```

**Solución:** Actualizar `SubmitSelector` en AiEngineRegistry.cs

### Problema: GPT dejó de funcionar

**Causa:** Script modificado rompió GPT
**Solución:** Revertir cambios y probar solo con nuevos proveedores

---

## Arquitectura Final

```
Usuario ? PromptDetailsPage
    ?
BaseViewModel.SendPromptToAsync()
    ?
Shell.Navigation ? EngineWebViewPage
    ?
EngineWebViewPage.OnWebViewNavigated
    ?
TryInjectWithRetryAsync (hasta 3 intentos)
    ?
WebViewInjectionService.TryInjectAsync
    ?
await Task.Delay(descriptor.DelayMs)  ? 2000-4000ms según proveedor
    ?
TryPersistentInjectionAsync
    ?
webView.EvaluateJavaScriptAsync(BuildPersistentInjectionScript())
    ?
JavaScript Injection:
    1. Poll input (15 attempts)
    2. Native setter + full events
    3. Poll submit (10 attempts)
    4. Click submit
    ?
return 'success:complete' o 'error:...'
    ?
Si éxito: SetExecutionResult(Success)
Si falla 3 veces: Clipboard fallback
```

**Principios aplicados:**
- ? Defensivo (no rompe lo existente)
- ? Resiliente (polling + múltiples selectores)
- ? Completo (todos los eventos necesarios)
- ? Específico por proveedor (selectores ordenados)

---

## Commits Pendientes (NO pushed aún)

1. `91bce95` - Diagnostic logging
2. `7be9ee2` - Grok event simulation (ahora mejorado)
3. `cabee84` - Copilot fixes (ahora mejorado)
4. **NUEVO** - Unified injection strategy (este commit)

**Estado:** ? Listos para push después de verificación completa

---

## Resumen Ejecutivo

**Problema:** 3 de 4 proveedores no insertaban prompts correctamente

**Causa raíz:**
- Timing: DOM no listo cuando script ejecuta
- Eventos: Frameworks no detectaban cambios sin eventos completos
- Selectores: Demasiado específicos, se rompían con cambios de UI

**Solución:** Estrategia unificada con polling, multi-selector, native setter y eventos completos

**Resultado esperado:** 4 de 4 proveedores funcionando con auto-inserción y auto-submit

**Riesgo:** ?? Bajo - Cambios defensivos, GPT no afectado, rollback fácil si falla
