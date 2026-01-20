# ?? Guía de Pruebas: Detección de Variables Mejorada

## ?? Problema Resuelto

**Problema original:** Cuando el usuario seleccionaba `<variable>` (texto envuelto en angle brackets), el botón rojo "Remove Variable" NO aparecía. En su lugar, aparecía el botón azul "Make Variable".

**Causa raíz:** La lógica de detección estaba usando `Trim()` pero no estaba verificando correctamente si el texto seleccionado completo era una variable.

## ? Nueva Lógica Implementada

### Método `IsTextAVariable(string text)`

Este método detecta si un texto es una variable válida con la siguiente lógica:

```csharp
1. Trim del texto (eliminar espacios al inicio/final)
2. Verificar longitud mínima: 3 caracteres (<x>)
3. Verificar que empiece con '<'
4. Verificar que termine con '>'
5. Extraer contenido interno (sin los brackets)
6. Verificar que el contenido NO esté vacío
7. Verificar que NO haya brackets anidados (< o > dentro)
```

### Métodos Helper

```csharp
ShowCreateButton()  // Muestra botón azul "Make Variable"
ShowUndoButton()    // Muestra botón rojo "Remove Variable"
HideAllButtons()    // Oculta ambos botones
```

## ?? Casos de Prueba

### ? Caso 1: Seleccionar Variable Completa
**Texto en el editor:**
```
Write about <topic> for <audience>
```

**Acción:** Seleccionar exactamente `<topic>` (incluidos los brackets)

**Resultado Esperado:**
- ? Botón ROJO "Remove Variable" debe aparecer
- ? Botón azul debe estar oculto

**Logging esperado:**
```
[IsTextAVariable] Original: '<topic>' (len=7)
[IsTextAVariable] Trimmed: '<topic>' (len=7)
[IsTextAVariable] Starts with '<': True
[IsTextAVariable] Ends with '>': True
[IsTextAVariable] Inner content: 'topic'
[IsTextAVariable] Has nested brackets: False
[IsTextAVariable] TRUE: Valid variable
>>> SHOWING RED BUTTON (Remove Variable) <<<
```

---

### ? Caso 2: Seleccionar Variable con Espacios
**Texto en el editor:**
```
  <variable_name>  
```

**Acción:** Seleccionar `  <variable_name>  ` (con espacios)

**Resultado Esperado:**
- ? Botón ROJO "Remove Variable" debe aparecer
- Se hace trim automáticamente

**Logging esperado:**
```
[IsTextAVariable] Original: '  <variable_name>  ' (len=19)
[IsTextAVariable] Trimmed: '<variable_name>' (len=15)
[IsTextAVariable] TRUE: Valid variable
```

---

### ? Caso 3: Seleccionar Texto Normal
**Texto en el editor:**
```
This is a test phrase
```

**Acción:** Seleccionar `test`

**Resultado Esperado:**
- ? Botón rojo oculto
- ? Botón AZUL "Make Variable" debe aparecer

**Logging esperado:**
```
[IsTextAVariable] Original: 'test' (len=4)
[IsTextAVariable] Trimmed: 'test' (len=4)
[IsTextAVariable] Starts with '<': False
[IsTextAVariable] Ends with '>': False
[IsTextAVariable] FALSE: No angle brackets
>>> SHOWING BLUE BUTTON (Make Variable) <<<
```

---

### ? Caso 4: Seleccionar Solo Parte de Variable
**Texto en el editor:**
```
Write about <topic> today
```

**Acción:** Seleccionar solo `topic` (SIN los brackets)

**Resultado Esperado:**
- ? Botón rojo oculto
- ? Botón AZUL "Make Variable" debe aparecer

**Logging esperado:**
```
[IsTextAVariable] Original: 'topic' (len=5)
[IsTextAVariable] FALSE: No angle brackets
>>> SHOWING BLUE BUTTON (Make Variable) <<<
```

---

### ? Caso 5: Variable con Guiones Bajos
**Texto en el editor:**
```
Create a <content_type> about <main_subject>
```

**Acción:** Seleccionar `<content_type>`

**Resultado Esperado:**
- ? Botón ROJO "Remove Variable" debe aparecer

**Logging esperado:**
```
[IsTextAVariable] Original: '<content_type>' (len=14)
[IsTextAVariable] Inner content: 'content_type'
[IsTextAVariable] TRUE: Valid variable
>>> SHOWING RED BUTTON (Remove Variable) <<<
```

---

### ? Caso 6: Brackets Anidados (Inválido)
**Texto en el editor:**
```
<<nested>>
```

**Acción:** Seleccionar `<<nested>>`

**Resultado Esperado:**
- ? Botón rojo oculto
- ? Botón AZUL "Make Variable" debe aparecer (tratar como texto normal)

**Logging esperado:**
```
[IsTextAVariable] Original: '<<nested>>' (len=10)
[IsTextAVariable] Trimmed: '<<nested>>' (len=10)
[IsTextAVariable] Inner content: '<nested>'
[IsTextAVariable] Has nested brackets: True
[IsTextAVariable] FALSE: Nested brackets found
>>> SHOWING BLUE BUTTON (Make Variable) <<<
```

---

### ? Caso 7: Solo Brackets Vacíos
**Texto en el editor:**
```
<>
```

**Acción:** Seleccionar `<>`

**Resultado Esperado:**
- ? Botón rojo oculto
- ? Botón AZUL "Make Variable" debe aparecer

**Logging esperado:**
```
[IsTextAVariable] Original: '<>' (len=2)
[IsTextAVariable] Trimmed: '<>' (len=2)
[IsTextAVariable] FALSE: Too short
>>> SHOWING BLUE BUTTON (Make Variable) <<<
```

---

### ? Caso 8: Variable de Un Carácter
**Texto en el editor:**
```
<x>
```

**Acción:** Seleccionar `<x>`

**Resultado Esperado:**
- ? Botón ROJO "Remove Variable" debe aparecer

**Logging esperado:**
```
[IsTextAVariable] Original: '<x>' (len=3)
[IsTextAVariable] Inner content: 'x'
[IsTextAVariable] TRUE: Valid variable
>>> SHOWING RED BUTTON (Remove Variable) <<<
```

---

### ? Caso 9: Multiple Variables - Primera
**Texto en el editor:**
```
Write <format> about <topic> for <audience>
```

**Acción:** Seleccionar `<format>`

**Resultado Esperado:**
- ? Botón ROJO "Remove Variable" debe aparecer

---

### ? Caso 10: Multiple Variables - Última
**Texto en el editor:**
```
Write <format> about <topic> for <audience>
```

**Acción:** Seleccionar `<audience>`

**Resultado Esperado:**
- ? Botón ROJO "Remove Variable" debe aparecer

---

## ?? Flujo Completo de Interacción

### Escenario 1: Crear Variable desde Cero

1. **Escribir texto:** `marketing strategy`
2. **Seleccionar:** `marketing strategy`
3. **Verificar:** Botón AZUL aparece
4. **Click:** Botón azul
5. **Ingresar nombre:** `topic`
6. **Resultado:** Texto se convierte en `<topic>`

### Escenario 2: Remover Variable Existente

1. **Texto inicial:** `Write about <subject> today`
2. **Seleccionar:** `<subject>` (completo con brackets)
3. **Verificar:** Botón ROJO aparece
4. **Click:** Botón rojo
5. **Confirmar:** "Remove"
6. **Resultado:** Texto se convierte en `Write about subject today`

### Escenario 3: Modificar Variable Existente

1. **Texto inicial:** `Create <old_name>`
2. **Seleccionar:** `<old_name>`
3. **Botón ROJO aparece**
4. **Opción A - Remover:** Click botón rojo ? `Create old_name`
5. **Opción B - Editar en Visual Mode:** Cambiar a modo Visual ? Click en chip ? Editar nombre

---

## ?? Tabla de Verdad de Detección

| Texto Seleccionado | Empieza con `<` | Termina con `>` | Longitud ? 3 | Sin anidados | Resultado |
|-------------------|-----------------|----------------|-------------|-------------|-----------|
| `<var>`           | ?              | ?             | ?          | ?          | ?? UNDO   |
| `var`             | ?              | ?             | ?          | ?          | ?? CREATE |
| `<va`             | ?              | ?             | ?          | ?          | ?? CREATE |
| `var>`            | ?              | ?             | ?          | ?          | ?? CREATE |
| `<>`              | ?              | ?             | ?          | ?          | ?? CREATE |
| `<<x>>`           | ?              | ?             | ?          | ?          | ?? CREATE |
| `  <var>  `       | ?              | ?             | ?          | ?          | ?? UNDO   |
| `<x>`             | ?              | ?             | ?          | ?          | ?? UNDO   |

---

## ?? Debugging

### Habilitar Logging

El código ya incluye logging extensivo. Para ver los logs:

**Visual Studio:**
1. Abrir ventana "Output"
2. Seleccionar "Debug" en el dropdown
3. Ejecutar la app en modo Debug

**Logs clave a buscar:**
```
[Selection] Editor.Cursor: X, Editor.Length: Y
[IsTextAVariable] Original: 'texto' (len=N)
[IsTextAVariable] TRUE/FALSE: [razón]
>>> SHOWING RED/BLUE BUTTON <<<
```

### Si el botón NO aparece:

1. Verificar que el Editor esté enfocado
2. Verificar los logs de `[CheckTextSelection]`
3. Verificar que `SelectionLength > 0`
4. Verificar el output de `IsTextAVariable`

### Si aparece el botón incorrecto:

1. Revisar el texto exacto seleccionado en logs
2. Verificar caracteres invisibles (espacios, tabs)
3. Usar logging de `[IsTextAVariable]` para ver cada paso

---

## ? Checklist de Testing

- [ ] Seleccionar `<variable>` completo ? Botón rojo
- [ ] Seleccionar `variable` sin brackets ? Botón azul
- [ ] Seleccionar parte de `<variable>` (solo texto) ? Botón azul
- [ ] Seleccionar texto con espacios al inicio/final ? Funcionamiento correcto
- [ ] Seleccionar variable de un carácter `<x>` ? Botón rojo
- [ ] Seleccionar brackets vacíos `<>` ? Botón azul
- [ ] Seleccionar brackets anidados `<<x>>` ? Botón azul
- [ ] Crear variable desde texto ? Funciona correctamente
- [ ] Remover variable existente ? Funciona correctamente
- [ ] Timer de detección funciona cada 300ms
- [ ] Botones se ocultan al desenfocar el Editor
- [ ] Logging muestra información útil para debugging

---

## ?? Notas de Implementación

### Cambios Principales

1. **Método `IsTextAVariable()`:**
   - Reemplaza la lógica inline de detección
   - Logging detallado paso a paso
   - Validaciones claras y ordenadas

2. **Métodos Helper de UI:**
   - `ShowCreateButton()` - Simplifica mostrar botón azul
   - `ShowUndoButton()` - Simplifica mostrar botón rojo
   - `HideAllButtons()` - Centraliza el ocultamiento

3. **Eliminación de código duplicado:**
   - `IsExistingVariable()` ahora llama a `IsTextAVariable()`
   - Lógica centralizada en un solo lugar

### Beneficios

? **Claridad:** Fácil de entender qué hace cada paso  
? **Debugging:** Logging exhaustivo para troubleshooting  
? **Mantenibilidad:** Un solo lugar para modificar la lógica  
? **Testabilidad:** Casos de prueba bien definidos  
? **Robustez:** Manejo de edge cases

---

**Última actualización:** 2024  
**Archivo modificado:** `Pages\MainPage.xaml.cs`  
**Estado:** ? Listo para testing
