using QuickPrompt.Models;
using SQLite;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace QuickPrompt.Services;

public class PromptDatabaseService
{
    private readonly SQLiteAsyncConnection _database;

    public PromptDatabaseService()
    {
        string dbPath = Path.Combine(FileSystem.AppDataDirectory, "QuickPrompt.db3");

        _database = new SQLiteAsyncConnection(dbPath);

        _database.CreateTableAsync<PromptTemplate>().Wait();  // Crear tabla si no existe
    }

    // Crear o actualizar un prompt
    public Task<int> SavePromptAsync(PromptTemplate prompt)
    {
        return _database.InsertOrReplaceAsync(prompt);  // Insertar si es nuevo, actualizar si ya existe
    }

    // Leer todos los prompts guardados
    public Task<List<PromptTemplate>> GetAllPromptsAsync()
    {
        return _database.Table<PromptTemplate>().ToListAsync();  // Retorna la lista completa de prompts
    }

    // Eliminar un prompt
    public Task<int> DeletePromptAsync(PromptTemplate prompt)
    {
        return _database.DeleteAsync(prompt);  // Eliminar el prompt seleccionado
    }

    // Obtener un prompt por ID
    public Task<PromptTemplate> GetPromptByIdAsync(Guid id)
    {
        return _database.Table<PromptTemplate>().FirstOrDefaultAsync(p => p.Id == id);  // Retorna el prompt con el ID especificado
    }
}
