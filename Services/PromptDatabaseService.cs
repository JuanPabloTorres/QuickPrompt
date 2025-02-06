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

    public async Task<List<PromptTemplate>> GetPromptsByBlockAsync(int offset, int limit, int currentTotal)
    {
        // Calcular cuántos elementos se deben omitir
        int _toSkip = offset * limit;

        // Obtener el total de prompts
        int _totalCount = await GetTotalPromptsCountAsync();

        // Ajustar _toSkip si excede el total de prompts
        if (_toSkip > _totalCount)
        {
            //_toSkip = Math.Max(0, _totalCount - limit);  // Ajustar para evitar desbordes

            _toSkip = (offset - 1) * limit;

            _toSkip = _toSkip - currentTotal;

            //var _diferenciaEntreLoqueTengo = _totalCount - currentTotal;

            //_toSkip += _diferenciaEntreLoqueTengo;
        }

        // Consultar la base de datos con paginación
        var _prompts = await _database.Table<PromptTemplate>()
                                      .Skip(_toSkip)          // Saltar los registros necesarios
                                      .Take(limit)            // Tomar el bloque de datos
                                      .ToListAsync();

        return _prompts;
    }

    //// Leer los prompts con paginación
    //public async Task<List<PromptTemplate>> GetPromptsByBlockAsync(int offset, int limit)
    //{
    //    var _toSkip = (offset * limit);

    // var _totalCount = await GetTotalPromptsCountAsync();

    // if (_toSkip > 0) { if (_totalCount <= _toSkip) { if (_toSkip % _totalCount != 0) { _toSkip =
    // _toSkip - limit; } }

    // }

    // var _prompts = await _database.Table<PromptTemplate>().Skip(_toSkip).Take(limit).OrderBy(v=>v.Title).ToListAsync();

    //    return _prompts;
    //}

    // Leer los prompts con paginación y filtro
    public Task<List<PromptTemplate>> GetPromptsByBlockAsync(int offset, int limit, string filter = "")
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            // Si no hay filtro, devuelve el bloque directamente
            return _database.Table<PromptTemplate>().Skip(offset).Take(limit).ToListAsync();
        }
        else
        {
            // Si hay filtro, busca por título y devuelve el bloque
            return _database.Table<PromptTemplate>().Where(p => p.Title.Contains(filter)).Skip(offset).Take(limit).ToListAsync();
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
}