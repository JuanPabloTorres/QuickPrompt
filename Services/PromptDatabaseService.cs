using Microsoft.Maui.Storage;
using QuickPrompt.Models;
using QuickPrompt.Tools;
using SQLite;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace QuickPrompt.Services
{
    public class PromptDatabaseService
    {
        private readonly SQLiteAsyncConnection _database;

        public PromptDatabaseService()
        {
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "QuickPrompt.db3");
            _database = new SQLiteAsyncConnection(dbPath);
            InitializeDatabase();
        }

        /// <summary>
        /// Inicializa la base de datos y crea las tablas necesarias si no existen.
        /// </summary>
        private async void InitializeDatabase()
        {
            await _database.CreateTableAsync<PromptTemplate>();
        }

        // =============================== 🔹 OPERACIONES CRUD PRINCIPALES ===============================

        /// <summary>
        /// Guarda o actualiza un prompt en la base de datos.
        /// </summary>
        public async Task<int> SavePromptAsync(PromptTemplate prompt)
        {
            return await _database.InsertOrReplaceAsync(prompt);
        }

        /// <summary>
        /// Obtiene un prompt por su ID.
        /// </summary>
        public Task<PromptTemplate> GetPromptByIdAsync(Guid id)
        {
            return _database.Table<PromptTemplate>().FirstOrDefaultAsync(p => p.Id == id);
        }

        /// <summary>
        /// Elimina un prompt de la base de datos.
        /// </summary>
        public async Task<bool> DeletePromptAsync(Guid id)
        {
            var prompt = await GetPromptByIdAsync(id);

            return prompt != null && await _database.DeleteAsync(prompt) > 0;
        }

        /// <summary>
        /// Actualiza un prompt existente con nuevos valores.
        /// </summary>
        public async Task<int> UpdatePromptAsync(Guid id, string newTitle, string newTemplate, string newDescription, Dictionary<string, string> newVariables)
        {
            var prompt = await GetPromptByIdAsync(id);
            if (prompt == null)
                throw new KeyNotFoundException(AppMessagesEng.Prompts.PromptNotFound);

            prompt.Title = newTitle;
            prompt.Template = newTemplate;
            prompt.Description = newDescription;
            prompt.Variables = newVariables;

            return await _database.UpdateAsync(prompt);
        }

        // =============================== 🔹 OBTENER LISTA DE PROMPTS ===============================

        /// <summary>
        /// Obtiene todos los prompts guardados.
        /// </summary>
        public Task<List<PromptTemplate>> GetAllPromptsAsync()
        {
            return _database.Table<PromptTemplate>().ToListAsync();
        }

        /// <summary>
        /// Obtiene los prompts con paginación y opcionalmente aplica un filtro.
        /// </summary>
        public Task<List<PromptTemplate>> GetPromptsByBlockAsync(int offset, int limit, string filter = "")
        {
            return GetPromptsByFilterAsync(offset, limit, filter, onlyFavorites: false);
        }

        /// <summary>
        /// Obtiene los prompts favoritos con paginación y opcionalmente aplica un filtro.
        /// </summary>
        public Task<List<PromptTemplate>> GetFavoritesPromptsByBlockAsync(int offset, int limit, string filter = "")
        {
            return GetPromptsByFilterAsync(offset, limit, filter, onlyFavorites: true);
        }

        /// <summary>
        /// Obtiene los prompts según el filtro y el estado de favoritos.
        /// </summary>
        private async Task<List<PromptTemplate>> GetPromptsByFilterAsync(int offset, int limit, string filter, bool onlyFavorites)
        {
            var query = _database.Table<PromptTemplate>();

            if (onlyFavorites)
                query = query.Where(p => p.IsFavorite);

            if (!string.IsNullOrWhiteSpace(filter))
                query = query.Where(p => p.Title.ToLower().Contains(filter.ToLower()));

            return await query.Skip(offset).Take(limit).ToListAsync();
        }

        // =============================== 🔹 OPERACIONES SOBRE FAVORITOS ===============================

        /// <summary>
        /// Actualiza el estado de favorito de un prompt.
        /// </summary>
        public async Task<int> UpdateFavoriteStatusAsync(Guid id, bool isFavorite)
        {
            var prompt = await GetPromptByIdAsync(id);
            if (prompt == null)
                throw new KeyNotFoundException(AppMessagesEng.Prompts.PromptNotFound);

            prompt.IsFavorite = isFavorite;
            return await _database.UpdateAsync(prompt);
        }

        /// <summary>
        /// Obtiene todos los prompts marcados como favoritos.
        /// </summary>
        public async Task<IEnumerable<PromptTemplate>> GetFavoritePromptsAsync()
        {
            return await _database.Table<PromptTemplate>().Where(p => p.IsFavorite).ToListAsync();
        }

        // =============================== 🔹 OBTENER CONTADOR DE PROMPTS ===============================

        /// <summary>
        /// Obtiene la cantidad total de prompts almacenados en la base de datos.
        /// </summary>
        public Task<int> GetTotalPromptsCountAsync() => GetTotalCountAsync("", onlyFavorites: false);

        /// <summary>
        /// Obtiene la cantidad total de prompts que coinciden con un filtro.
        /// </summary>
        public Task<int> GetTotalPromptsCountAsync(string filter) => GetTotalCountAsync(filter, onlyFavorites: false);

        /// <summary>
        /// Obtiene la cantidad total de prompts favoritos almacenados.
        /// </summary>
        public Task<int> GetFavoriteTotalPromptsCountAsync() => GetTotalCountAsync("", onlyFavorites: true);

        /// <summary>
        /// Obtiene la cantidad total de prompts favoritos que coinciden con un filtro.
        /// </summary>
        public Task<int> GetFavoriteTotalPromptsCountAsync(string filter) => GetTotalCountAsync(filter, onlyFavorites: true);

        /// <summary>
        /// Método genérico para contar la cantidad de prompts con filtro y favoritos.
        /// </summary>
        private async Task<int> GetTotalCountAsync(string filter, bool onlyFavorites)
        {
            var query = _database.Table<PromptTemplate>();

            if (onlyFavorites)
                query = query.Where(p => p.IsFavorite);

            if (!string.IsNullOrWhiteSpace(filter))
                query = query.Where(p => p.Title.ToLower().Contains(filter.ToLower()));

            return await query.CountAsync();
        }
    }
}