using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Tools;

public static class AppMessages
{
    // **Mensajes de Error Genéricos**
    public const string GenericError = "Ocurrió un error. Por favor, inténtalo nuevamente.";
    public const string NetworkError = "No se pudo conectar. Verifica tu conexión a Internet.";
    public const string UnknownError = "Ha ocurrido un error desconocido.";
    public const string UnauthorizedError = "No tienes permiso para realizar esta acción.";
    public const string ValidationError = "Algunos campos no son válidos. Por favor, verifica e intenta nuevamente.";

    // **Mensajes Relacionados con Prompts**
    public static class Prompts
    {
        public const string PromptDeleteError = "No se pudo eliminar el prompt. Intenta nuevamente.";
        public const string PromptEmptyTitleError = "El título del prompt no puede estar vacío.";
        public const string PromptFilterError = "Ocurrió un problema al filtrar los prompts. Por favor, inténtalo nuevamente.";
        public const string PromptLoadError = "Ocurrió un problema al cargar los prompts. Por favor, inténtalo nuevamente.";
        public const string PromptNotFound = "El prompt no se encontró en la base de datos.";
        public const string PromptSavedSuccess = "El prompt ha sido guardado exitosamente.";
        public const string PromptSaveError = "Ocurrió un problema al guardar el prompt. Por favor, inténtalo nuevamente.";
        public const string PromptVariablesError = "Debes completar todas las variables antes de generar el prompt.";        
        public const string PromptGenerateError = "Ocurrió un problema al generar el prompt. Por favor, inténtalo nuevamente.";
        public const string PromptEmptyAndUnSelected = "El campo del prompt está vacío y no hay ningún texto seleccionado. Por favor, inténtelo nuevamente.";
        public const string PromptDevelopmentMessage = "Esta funcionalidad está en desarrollo. Disculpe los inconvenientes.";
    }

    // **Mensajes de Aviso**
    public static class Warnings
    {
        public const string SelectWordError = "Selecciona una palabra o frase para convertirla en variable.";
        public const string WordAlreadyHasBraces = "La palabra o frase ya está rodeada por llaves.";
      
    }
}
