using Microsoft.Maui.Storage;
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

    // Leer los prompts con paginación y filtro
    public async Task<List<PromptTemplate>> GetPromptsByBlockAsync(int offset, int limit, string filter = "")
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            // Si no hay filtro, devuelve el bloque directamente
            return await _database.Table<PromptTemplate>().Skip(offset).Take(limit).ToListAsync();
        }
        else
        {
            // Si hay filtro, busca por título y devuelve el bloque
            return await _database.Table<PromptTemplate>().Where(p => p.Title.ToLower().Contains(filter.ToLower())).Skip(offset).Take(limit).ToListAsync();
        }
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

    // Actualizar un prompt existente (incluyendo variables)
    public async Task<int> UpdatePromptAsync(Guid id, string newTitle, string newTemplate, string newDescription, Dictionary<string, string> newVariables)
    {
        var existingPrompt = await GetPromptByIdAsync(id);

        if (existingPrompt == null)
        {
            throw new KeyNotFoundException($"No se encontró un prompt con el ID: {id}");
        }

        // Actualizar las propiedades del prompt existente
        existingPrompt.Title = newTitle;
        existingPrompt.Template = newTemplate;
        existingPrompt.Description = newDescription;
        existingPrompt.Variables = newVariables;

        // Guardar los cambios en la base de datos
        return await _database.UpdateAsync(existingPrompt);
    }

    //Obtiene el total de registros de prompts almacenados en la base de datos.
    public Task<int> GetTotalPromptsCountAsync()
    {
        return _database.Table<PromptTemplate>().CountAsync();
    }

    //Obtiene el total de registros de prompts almacenados en la base de datos.
    public Task<int> GetTotalPromptsCountAsync(string filter)
    {
        return _database.Table<PromptTemplate>().Where(p => p.Title.ToLower().Contains(filter.ToLower())).CountAsync();
    }

    // Actualizar el estado de favorito de un prompt
    public async Task<int> UpdateFavoriteStatusAsync(Guid id, bool isFavorite)
    {
        var existingPrompt = await GetPromptByIdAsync(id);

        if (existingPrompt == null)
        {
            throw new KeyNotFoundException($"No se encontró un prompt con el ID: {id}");
        }

        // Actualizar el estado de favorito
        existingPrompt.IsFavorite = isFavorite;

        // Guardar los cambios en la base de datos
        return await _database.UpdateAsync(existingPrompt);
    }

    // Método para obtener los prompts marcados como favoritos como IEnumerable
    public async Task<IEnumerable<PromptTemplate>> GetFavoritePromptsAsync()
    {
        var favorites = await _database.Table<PromptTemplate>().Where(p => p.IsFavorite == true).ToListAsync();

        return favorites.AsEnumerable(); // Convertir la lista en IEnumerable
    }
}