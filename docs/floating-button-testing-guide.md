# ?? Floating Button Testing Guide

## ? **Build Status: SUCCESSFUL**
- Branch: `dev`
- Commit: `a0bb8f4`
- All XAML controls regenerated correctly
- Both buttons (`FloatingVariableButton` and `FloatingUndoVariableButton`) are now available

---

## ?? **CÓMO PROBAR EL BOTÓN ROJO (Remove Variable)**

### **Requisitos Previos**
1. ? App ejecutándose en Debug mode (F5)
2. ? Visual Studio Output window abierto
3. ? Output window en modo "Debug"
4. ? Estás en la página "Create Prompt"

---

### **?? TEST 1: Crear y Luego Deshacer Variable**

#### **Paso 1: Crear una Variable**
```
1. En MainPage, campo "Prompt Template"
2. Escribe: "Create a blog post"
3. Selecciona: "blog"
4. ? Botón AZUL debe aparecer: "Make Variable"
5. Tap en botón azul
6. Ingresa nombre: "format"
7. ? Texto cambia a: "Create a <format> post"
```

#### **Paso 2: Deshacer la Variable**
```
8. Selecciona COMPLETAMENTE: "<format>" (incluye < y >)
   ?? CRÍTICO: Debes seleccionar desde < hasta >
   
9. Mantén la selección por 1 segundo
10. ? Botón ROJO debe aparecer: "Remove Variable"
11. Tap en botón rojo
12. Confirma "Remove"
13. ? Texto cambia a: "Create a format post"
```

**Expected Output Logs:**
```
[Editor] Focused - Starting selection monitoring
[Timer] Selection monitoring started
[VM PropertyChanged] Cursor: 10, Length: 8
[Selection] Cursor: 10, Length: 8, Text Length: 23
[Selection] Text: '<format>' (len: 8)
[IsExistingVariable] '<format>': brackets=True, content=True, noNested=True
[Detection] IsVariable: True
>>> SHOWING RED BUTTON (Remove Variable) <<<
```

---

### **?? TEST 2: Variable Existente Desde el Principio**

#### **Escenario**
```
1. Escribe directamente: "Write <article> about <topic>"
2. Selecciona: "<topic>" (incluye < y >)
3. ? Botón ROJO debe aparecer
4. Tap ? Confirma "Remove"
5. ? Resultado: "Write <article> about topic"
```

**Expected Output:**
```
[Selection] Text: '<topic>' (len: 7)
[IsExistingVariable] '<topic>': brackets=True, content=True, noNested=True
[Detection] IsVariable: True
>>> SHOWING RED BUTTON (Remove Variable) <<<
```

---

### **?? TEST 3: Alternar Entre Botones**

#### **Escenario**
```
1. Escribe: "Create <format> post"
2. Selecciona: "post" ? ? Botón AZUL
3. Deselecciona
4. Selecciona: "<format>" ? ? Botón ROJO
5. Deselecciona
6. Selecciona: "Create" ? ? Botón AZUL
```

---

## ?? **TROUBLESHOOTING**

### **Problema 1: Botón ROJO No Aparece**

#### **Causa Posible: Selección Incompleta**
```
? Selección: "<forma" (sin >)
? Solución: Incluye el > en la selección
```

#### **Causa Posible: Editor Sin Foco**
```
? Timer no corriendo
? Solución: Tap en el Editor antes de seleccionar
```

#### **Causa Posible: Modo Visual Activo**
```
? Estás en modo "?? Visual"
? Solución: Cambia a modo "?? Text"
```

---

### **Problema 2: Ningún Log en Output Window**

#### **Verificaciones**
```
1. ? Output window está abierto (View ? Output)
2. ? Dropdown dice "Debug" (no "Build" ni otro)
3. ? App está en Debug mode (F5, no Ctrl+F5)
4. ? Has dado tap en el Editor (para darle foco)
```

---

## ?? **VALIDACIONES IMPLEMENTADAS**

### **Para Botón AZUL (Crear Variable)** ??
```csharp
? Texto no empieza con <
? Texto no termina con >
? Selección en límites de palabra
? Longitud > 0
```

### **Para Botón ROJO (Deshacer Variable)** ??
```csharp
? Texto empieza con <
? Texto termina con >
? Longitud > 2 (al menos <x>)
? Sin brackets anidados
```

---

## ?? **EJEMPLOS DE SELECCIÓN**

| Texto Original | Selección | Botón Esperado | Razón |
|----------------|-----------|----------------|-------|
| `Create post` | `post` | ?? AZUL | Texto normal |
| `Create <format>` | `<format>` | ?? ROJO | Variable existente |
| `Write <topic>` | `topic` | ?? AZUL | Sin brackets |
| `Write <topic>` | `<topic` | ? Ninguno | Incompleto |
| `Write <topic>` | `topic>` | ? Ninguno | Incompleto |
| `Text <var>` | `<var>` | ?? ROJO | Variable válida |

---

## ?? **CÓMO SELECCIONAR CORRECTAMENTE EN MÓVIL**

### **En Android/iOS**
```
1. Mantén presionado el dedo en el <
2. Arrastra hasta después del >
3. Suelta cuando todo esté resaltado
4. Los "handles" de selección deben estar:
   - Handle izquierdo: antes de <
   - Handle derecho: después de >
```

### **En Windows**
```
1. Click y mantén en el <
2. Arrastra hasta después del >
3. Suelta
4. Todo debe estar resaltado: [<topic>]
```

---

## ?? **DEBUG CHECKLIST**

Si el botón ROJO sigue sin aparecer, verifica esto en el Output window:

### **1. Timer Iniciado** ?
```
Busca: "[Timer] Selection monitoring started"
Si NO lo ves ? El Editor no tiene foco
```

### **2. Selección Detectada** ?
```
Busca: "[Selection] Text: '<topic>' (len: 7)"
Si len: 0 ? La selección no se captura
Si texto diferente ? Selección incorrecta
```

### **3. Variable Detectada** ?
```
Busca: "[IsExistingVariable] '<topic>': brackets=True, content=True, noNested=True"
Si alguno es False ? Validación falló
```

### **4. Decisión de Botón** ?
```
Busca: ">>> SHOWING RED BUTTON (Remove Variable) <<<"
Si ves otro mensaje ? El botón elegido fue diferente
```

---

## ?? **CONFIRMACIÓN VISUAL**

Cuando funcione correctamente, deberías ver:

### **UI**
```
????????????????????????????????????????
? Create a <format> post               ?  ? Editor
?                                      ?
?  ???????????????????????            ?
?  ? ? Remove Variable  ?  ? Botón ROJO (arriba-izquierda)
?  ???????????????????????            ?
?                                      ?
????????????????????????????????????????
```

### **Output Window**
```
[Selection] Text: '<format>' (len: 8)
[IsExistingVariable] '<format>': brackets=True, content=True, noNested=True
[Detection] IsVariable: True
>>> SHOWING RED BUTTON (Remove Variable) <<<
```

---

## ?? **SI SIGUE SIN FUNCIONAR**

Por favor comparte:

1. ? **Texto completo que escribiste**
2. ? **Exactamente qué seleccionaste** (incluye < y >?)
3. ? **TODOS los logs del Output window** (copia todo)
4. ? **Plataforma de testing** (Android/iOS/Windows)
5. ? **Screenshot de la selección** (si es posible)

Con esa información puedo diagnosticar el problema exacto.

---

## ? **ESTADO ACTUAL**

```
Branch: dev
Commit: a0bb8f4
Build: ? SUCCESS
XAML: ? Regenerated
Controls: ? FloatingVariableButton + FloatingUndoVariableButton
Logging: ? Comprehensive
Status: ? READY TO TEST
```

---

**¡El código está listo! Por favor prueba siguiendo estos pasos y comparte el resultado.** ??
