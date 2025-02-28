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
            // Llamar a la inicialización de la base de datos de manera asíncrona
            Task.Run(async () => await InitializeDatabaseAsync());
        }

        /// <summary>
        /// Inicializa la base de datos y crea las tablas necesarias si no existen.
        /// </summary>
        private async Task InitializeDatabaseAsync()
        {
            await _database.CreateTableAsync<PromptTemplate>();

            await InsertDefaultPromptsAsync();
        }

        /// <summary>
        /// Inserta prompts útiles por defecto si la base de datos está vacía.
        /// </summary>
        private async Task InsertDefaultPromptsAsync()
        {
            int existingCount = await _database.Table<PromptTemplate>().CountAsync();

            if (existingCount > 0)
                return; // No agregar si ya hay prompts en la base de datos

            var defaultPrompts = new List<PromptTemplate>
            {
                new PromptTemplate
                {
                    Id = Guid.NewGuid(),
                    Title = "Daily Workout Plan",
                    Description = "Generate a structured workout plan for different fitness levels.",
                    Template = "Create a {workout} plan for {fitness level} with a duration of {time} minutes.",
                    Variables = new Dictionary<string, string>
                    {
                        { "workout", "Strength training" },
                        { "fitness level", "Beginner" },
                        { "time", "30" }
                    },
                    IsFavorite = true
                },
                new PromptTemplate
                {
                    Id = Guid.NewGuid(),
                    Title = "Essay Outline Generator",
                    Description = "Provides an outline for an essay based on a given topic.",
                    Template = "Create an essay outline on {topic} including introduction, main points, and conclusion.",
                    Variables = new Dictionary<string, string>
                    {
                        { "topic", "Artificial Intelligence in Healthcare" }
                    },
                    IsFavorite = true
                },
                new PromptTemplate
                {
                    Id = Guid.NewGuid(),
                    Title = "Travel Itinerary Planner",
                    Description = "Suggests an itinerary for a trip based on location and duration.",
                    Template = "Plan a {days}-day trip to {destination}, including places to visit and activities.",
                    Variables = new Dictionary<string, string>
                    {
                        { "days", "5" },
                        { "destination", "Paris" }
                    },
                    IsFavorite = true
                },
                new PromptTemplate
                {
                    Id = Guid.NewGuid(),
                    Title = "Healthy Meal Plan",
                    Description = "Suggests a healthy meal plan based on dietary preference.",
                    Template = "Create a {days}-day meal plan for a {diet} diet, including breakfast, lunch, and dinner.",
                    Variables = new Dictionary<string, string>
                    {
                        { "days", "7" },
                        { "diet", "Vegan" }
                    },
                    IsFavorite = true
                },
                new PromptTemplate
                {
                    Id = Guid.NewGuid(),
                    Title = "Interview Question Generator",
                    Description = "Generates interview questions for a specific job role.",
                    Template = "Generate interview questions for a {job role} position in {industry}.",
                    Variables = new Dictionary<string, string>
                    {
                        { "job role", "Software Engineer" },
                        { "industry", "Technology" }
                    },
                    IsFavorite = true
                }
            };

            await _database.InsertAllAsync(defaultPrompts);
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
        /// Actualiza el estado de favorito de un prompt en la base de datos.
        /// </summary>
        /// <param name="id">El identificador único del prompt.</param>
        /// <param name="isFavorite">Estado de favorito a establecer (true si es favorito, false si no lo es).</param>
        /// <returns>True si la actualización fue exitosa, False en caso contrario.</returns>
        public async Task<bool> UpdateFavoriteStatusAsync(Guid id, bool isFavorite)
        {
            // Obtener el prompt de la base de datos
            var prompt = await GetPromptByIdAsync(id) ??
                         throw new KeyNotFoundException(AppMessagesEng.Prompts.PromptNotFound);

            // Evitar actualizar si el estado no ha cambiado
            if (prompt.IsFavorite == isFavorite)
                return false; // No es necesario actualizar

            // Actualizar el estado de favorito
            prompt.IsFavorite = isFavorite;

            // Guardar cambios en la base de datos
            int rowsAffected = await _database.UpdateAsync(prompt);

            return rowsAffected > 0; // Retorna true si al menos una fila fue afectada
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