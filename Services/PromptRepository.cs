using QuickPrompt.Models;
using QuickPrompt.Models.Enums;
using QuickPrompt.Services.ServiceInterfaces;
using QuickPrompt.Tools;
using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Services
{
    // Repository Implementation
    public class PromptRepository : IPromptRepository
    {
        private  SQLiteAsyncConnection _database;

        private readonly DatabaseConnectionProvider _dbProvider;

        public PromptRepository(DatabaseConnectionProvider provider)
        {
            _dbProvider = provider;

            _database = _dbProvider.GetConnection();

            Task.Run(async () => await InitializeDatabaseAsync());
        }

     

        /// <summary>
        /// Inicializa la base de datos y crea las tablas necesarias si no existen.
        /// </summary>
        public async Task InitializeDatabaseAsync()
        {
            // Crear tabla si no existe
            await _database.CreateTableAsync<PromptTemplate>();

            // Verificar columnas y agregarlas si faltan
            var tableInfo = await _database.GetTableInfoAsync(nameof(PromptTemplate));

            if (!tableInfo.Any(c => c.Name == "CreatedAt"))
                await _database.ExecuteAsync("ALTER TABLE PromptTemplate ADD COLUMN CreatedAt TEXT DEFAULT ''");

            if (!tableInfo.Any(c => c.Name == "UpdatedAt"))
                await _database.ExecuteAsync("ALTER TABLE PromptTemplate ADD COLUMN UpdatedAt TEXT DEFAULT ''");

            if (!tableInfo.Any(c => c.Name == "IsDeleted"))
                await _database.ExecuteAsync("ALTER TABLE PromptTemplate ADD COLUMN IsDeleted INTEGER DEFAULT 0");

            if (!tableInfo.Any(c => c.Name == "Category"))
                await _database.ExecuteAsync("ALTER TABLE PromptTemplate ADD COLUMN Category TEXT");

            // Actualiza valores nulos con timestamps válidos
            await _database.ExecuteAsync("UPDATE PromptTemplate SET CreatedAt = datetime('now') WHERE CreatedAt IS NULL OR CreatedAt = ''");

            await _database.ExecuteAsync("UPDATE PromptTemplate SET UpdatedAt = datetime('now') WHERE UpdatedAt IS NULL OR UpdatedAt = ''");

            // Si la base de datos existe y no tiene los datos iniciales, insertarlos
            if (DatabaseExists())
            {
                int count = await _database.Table<PromptTemplate>().CountAsync();

                // Si no hay datos, insertar los prompts por defecto
                if (count == 0)
                    await InsertDefaultPromptsAsync();

                if (count > 0)
                {
                    var prompts = await _database.Table<PromptTemplate>().ToListAsync();

                    foreach (var prompt in prompts)
                    {
                        if (prompt.Category == 0 || !Enum.IsDefined(typeof(PromptCategory), prompt.Category))
                        {
                            prompt.Category = PromptCategory.General;

                            await _database.UpdateAsync(prompt);
                        }
                    }
                }
            }
        }

        public async Task InsertDefaultPromptsAsync()
        {
            var existingTitles = (await _database.Table<PromptTemplate>().ToListAsync())
                                 .Select(p => p.Title)
                                 .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var promptsToInsert = GetDefaultPrompts()
                .Where(p => !existingTitles.Contains(p.Title))
                .ToList();

            if (promptsToInsert.Count > 0)
                await _database.InsertAllAsync(promptsToInsert);
        }

        /// <summary>
        /// Inserta prompts útiles por defecto si la base de datos está vacía.
        /// </summary>
        private List<PromptTemplate> GetDefaultPrompts()
        {
            var defaultPrompts = new List<PromptTemplate>
            {
                new PromptTemplate
                {
                    Id = Guid.NewGuid(),
                    Title = "Daily Workout Plan",
                    Description = "Generate a structured workout plan for different fitness levels.",
                    Template = "Create a <workout> plan for <fitness level> with a duration of <time> minutes.",
                    Variables = new Dictionary<string, string>
                    {
                        { "workout", "Strength training" },
                        { "fitness level", "Beginner" },
                        { "time", "30" }
                    },
                    IsFavorite = true,
                    CreatedAt = DateTime.Now
                },
                new PromptTemplate
                {
                    Id = Guid.NewGuid(),
                    Title = "Essay Outline Generator",
                    Description = "Provides an outline for an essay based on a given topic.",
                    Template = "Create an essay outline on <topic> including introduction, main points, and conclusion.",
                    Variables = new Dictionary<string, string>
                    {
                        { "topic", "Artificial Intelligence in Healthcare" }
                    },
                    IsFavorite = true,
                    CreatedAt = DateTime.Now
                },
                new PromptTemplate
                {
                    Id = Guid.NewGuid(),
                    Title = "Travel Itinerary Planner",
                    Description = "Suggests an itinerary for a trip based on location and duration.",
                    Template = "Plan a <days>-day trip to <destination>, including places to visit and activities.",
                    Variables = new Dictionary<string, string>
                    {
                        { "days", "5" },
                        { "destination", "Paris" }
                    },
                    IsFavorite = true,
                    CreatedAt = DateTime.Now
                },
                new PromptTemplate
                {
                    Id = Guid.NewGuid(),
                    Title = "Healthy Meal Plan",
                    Description = "Suggests a healthy meal plan based on dietary preference.",
                    Template = "Create a <days>-day meal plan for a <diet> diet, including breakfast, lunch, and dinner.",
                    Variables = new Dictionary<string, string>
                    {
                        { "days", "7" },
                        { "diet", "Vegan" }
                    },
                    IsFavorite = true,
                    CreatedAt = DateTime.Now
                },
                new PromptTemplate
                {
                    Id = Guid.NewGuid(),
                    Title = "Interview Question Generator",
                    Description = "Generates interview questions for a specific job role.",
                    Template = "Generate interview questions for a <job role> position in <industry>.",
                    Variables = new Dictionary<string, string>
                    {
                        { "job role", "Software Engineer" },
                        { "industry", "Technology" }
                    },
                    IsFavorite = true,
                    CreatedAt = DateTime.Now
                },
                new PromptTemplate
                {
                    Id = Guid.NewGuid(),
                    Title = "Resume Summary Generator",
                    Description = "Generates a professional summary for a resume based on key strengths.",
                    Template = "Create a professional resume summary for <job role> highlighting <key strengths>.",
                    Variables = new Dictionary<string, string>
                    {
                        { "job role", "Project Manager" },
                        { "key strengths", "Leadership, strategic planning, and problem-solving" }
                    },
                    IsFavorite = true,
                    CreatedAt = DateTime.Now
                },
                new PromptTemplate
                {
                    Id = Guid.NewGuid(),
                    Title = "Product Description Writer",
                    Description = "Generates a compelling product description based on features and target audience.",
                    Template = "Write a compelling product description for <product name> focusing on <key features> for <target audience>.",
                    Variables = new Dictionary<string, string>
                    {
                        { "product name", "Smartphone X" },
                        { "key features", "AI camera, long battery life, and fast charging" },
                        { "target audience", "Tech enthusiasts" }
                    },
                    IsFavorite = true,
                    CreatedAt = DateTime.Now
                },
                new PromptTemplate
                {
                    Id = Guid.NewGuid(),
                    Title = "Social Media Post Generator",
                    Description = "Creates an engaging social media post based on topic and platform.",
                    Template = "Generate a social media post about <topic> for <platform> with an engaging tone.",
                    Variables = new Dictionary<string, string>
                    {
                        { "topic", "Sustainable living tips" },
                        { "platform", "Instagram" }
                    },
                    IsFavorite = true,
                    CreatedAt = DateTime.Now
                },
                new PromptTemplate
{
    Id = Guid.NewGuid(),
    Title = "Sports Player Analysis",
    Description = "Provides a performance analysis of a specific athlete.",
    Template = "Analyze the recent performance of <playerName> in <sport> including stats, strengths, and areas to improve.",
    Variables = new Dictionary<string, string>
    {
        { "playerName", "LeBron James" },
        { "sport", "basketball" }
    },
    IsFavorite = true,
    CreatedAt = DateTime.Now
},
                new PromptTemplate
{
    Id = Guid.NewGuid(),
    Title = "Medical Symptom Explanation",
    Description = "Explains a medical symptom in layman's terms.",
    Template = "Explain what it means when someone experiences <symptom> and suggest possible causes.",
    Variables = new Dictionary<string, string>
    {
        { "symptom", "chest pain" }
    },
    IsFavorite = true,
    CreatedAt = DateTime.Now
},
                new PromptTemplate
{
    Id = Guid.NewGuid(),
    Title = "Educational Topic Summary",
    Description = "Summarizes a complex educational topic in simple terms.",
    Template = "Summarize the topic of <topicName> for a <gradeLevel> student.",
    Variables = new Dictionary<string, string>
    {
        { "topicName", "photosynthesis" },
        { "gradeLevel", "6th grade" }
    },
    IsFavorite = true,
    CreatedAt = DateTime.Now
},
                new PromptTemplate
{
    Id = Guid.NewGuid(),
    Title = "Tech Concept Explainer",
    Description = "Explains a tech concept in a simplified manner.",
    Template = "Explain the concept of <concept> in simple terms with an example.",
    Variables = new Dictionary<string, string>
    {
        { "concept", "blockchain" }
    },
    IsFavorite = true,
    CreatedAt = DateTime.Now
},
                new PromptTemplate
{
    Id = Guid.NewGuid(),
    Title = "Quick Recipe Generator",
    Description = "Generates a simple recipe based on ingredients.",
    Template = "Give me a quick recipe using <ingredient1> and <ingredient2> that can be made in under 30 minutes.",
    Variables = new Dictionary<string, string>
    {
        { "ingredient1", "chicken" },
        { "ingredient2", "broccoli" }
    },
    IsFavorite = true,
    CreatedAt = DateTime.Now
},new PromptTemplate
{
    Id = Guid.NewGuid(),
    Title = "Business Pitch Creator",
    Description = "Generates a short pitch for a startup idea.",
    Template = "Create a 1-minute pitch for a startup focused on <industry> solving <problem> for <target audience>.",
    Variables = new Dictionary<string, string>
    {
        { "industry", "health tech" },
        { "problem", "access to remote consultations" },
        { "target audience", "rural patients" }
    },
    IsFavorite = true,
    CreatedAt = DateTime.Now
},
new PromptTemplate
{
    Id = Guid.NewGuid(),
    Title = "YouTube Video Script",
    Description = "Creates an engaging script for a YouTube video.",
    Template = "Write a YouTube script for a video about <topic> with an <tone> tone, lasting about <duration> minutes.",
    Variables = new Dictionary<string, string>
    {
        { "topic", "How to use ChatGPT for research" },
        { "tone", "educational" },
        { "duration", "10" }
    },
    IsFavorite = true,
    CreatedAt = DateTime.Now
},
new PromptTemplate
{
    Id = Guid.NewGuid(),
    Title = "Personal Budget Plan",
    Description = "Creates a monthly budget based on income and goals.",
    Template = "Generate a monthly budget plan for someone earning <income> with a goal to save for <goal>.",
    Variables = new Dictionary<string, string>
    {
        { "income", "$3000" },
        { "goal", "a new laptop" }
    },
    IsFavorite = true,
    CreatedAt = DateTime.Now
},
new PromptTemplate
{
    Id = Guid.NewGuid(),
    Title = "Job Application Email",
    Description = "Writes a professional email to apply for a job.",
    Template = "Write a job application email for a <position> role at <company>, highlighting <keySkill>.",
    Variables = new Dictionary<string, string>
    {
        { "position", "UX Designer" },
        { "company", "Spotify" },
        { "keySkill", "user-centered design" }
    },
    IsFavorite = true,
    CreatedAt = DateTime.Now
},
new PromptTemplate
{
    Id = Guid.NewGuid(),
    Title = "Motivational Quote Generator",
    Description = "Generates a unique motivational quote based on a theme.",
    Template = "Create an original motivational quote about <theme>.",
    Variables = new Dictionary<string, string>
    {
        { "theme", "resilience" }
    },
    IsFavorite = true,
    CreatedAt = DateTime.Now
},
new PromptTemplate
{
    Id = Guid.NewGuid(),
    Title = "Event Invitation Text",
    Description = "Creates a short invitation message for an event.",
    Template = "Write a friendly invitation message for a <eventType> happening on <date> at <location>.",
    Variables = new Dictionary<string, string>
    {
        { "eventType", "birthday party" },
        { "date", "July 22" },
        { "location", "Sunset Park" }
    },
    IsFavorite = true,
    CreatedAt = DateTime.Now
},
new PromptTemplate
{
    Id = Guid.NewGuid(),
    Title = "Weekly Reflection Prompt",
    Description = "Helps users reflect on their week with guided questions.",
    Template = "Reflect on your week: What went well in <area>, what was challenging, and what will you improve?",
    Variables = new Dictionary<string, string>
    {
        { "area", "work" }
    },
    IsFavorite = true,
    CreatedAt = DateTime.Now
},
new PromptTemplate
{
    Id = Guid.NewGuid(),
    Title = "Creative Story Starter",
    Description = "Gives an opening line for a creative short story.",
    Template = "Start a story with this setting: <setting> and a character named <character> who has a secret.",
    Variables = new Dictionary<string, string>
    {
        { "setting", "a forgotten village in the mountains" },
        { "character", "Lena" }
    },
    IsFavorite = true,
    CreatedAt = DateTime.Now
},new PromptTemplate
{
    Id = Guid.NewGuid(),
    Title = "Language Translator",
    Description = "Translates a phrase or sentence into the desired target language.",
    Template = "Translate the following text from <source language> to <target language>: <text>",
    Variables = new Dictionary<string, string>
    {
        { "source language", "English" },
        { "target language", "Spanish" },
        { "text", "Good morning, how are you?" }
    },
    IsFavorite = true,
    CreatedAt = DateTime.Now
}
            };

            return defaultPrompts;
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
        /// Elimina todos los prompts de la base de datos.
        /// </summary>
        public async Task<bool> DeleteAllPromptsAsync()
        {
            var prompts = await _database.Table<PromptTemplate>().ToListAsync();

            return prompts.Any() && await _database.DeleteAllAsync<PromptTemplate>() > 0;
        }

        /// <summary>
        /// Actualiza un prompt existente con nuevos valores.
        /// </summary>
        public async Task<PromptTemplate> UpdatePromptAsync(Guid id, string newTitle, string newTemplate, string newDescription, Dictionary<string, string> newVariables, PromptCategory selectedCategory)
        {
            var prompt = await GetPromptByIdAsync(id);

            if (prompt == null)
                throw new KeyNotFoundException(AppMessagesEng.Prompts.PromptNotFound);

            prompt.Title = newTitle;

            prompt.Template = newTemplate;

            prompt.Description = newDescription;

            prompt.Variables = newVariables;

            prompt.Category = selectedCategory;

            var _resultUpdated = await _database.UpdateAsync(prompt);

            bool isPromptUpdated = _resultUpdated > 0;

            if (isPromptUpdated)
            {
                return prompt;
            }
            else
            {
                return null;
            }
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
        public Task<List<PromptTemplate>> GetPromptsByBlockAsync(int offset, int limit, Filters dateFilter = Filters.All, string filterText = "", string selectedCategory = null)
        {
            return GetPromptsByFilterAsync(offset, limit, filterText, dateFilter, selectedCategory);
        }

        /// <summary>
        /// Obtiene los prompts según el filtro y el estado de favoritos.
        /// </summary>
        private async Task<List<PromptTemplate>> GetPromptsByFilterAsync(int offset, int limit, string filterText, Filters filter, string selectedCategory)
        {
            // Obtener todos los prompts de forma asincrónica
            var allPrompts = await _database.Table<PromptTemplate>().ToListAsync();

            // Aplicar filtro de fecha si corresponde

            DateTime today = DateTime.Today;

            DateTime end = today.AddDays(1); // Exclusivo para el día siguiente

            allPrompts = filter switch
            {
                Filters.Today => allPrompts
                    .Where(p => p.CreatedAt >= today && p.CreatedAt < end)
                    .ToList(),

                Filters.Last7Days => allPrompts
                    .Where(p => p.CreatedAt >= today.AddDays(-7) && p.CreatedAt < end)
                    .ToList(),

                Filters.Favorites => allPrompts
                    .Where(p => p.IsFavorite)
                    .ToList(),

                Filters.NonFavorites => allPrompts.Where(p => !p.IsFavorite).ToList(),

                _ => allPrompts // "All" o cualquier otro valor no filtra
            };

            // Aplicar filtro de texto si se proporciona
            if (!string.IsNullOrWhiteSpace(filterText))
            {
                string lowerFilter = filterText.ToLower();
                allPrompts = allPrompts
                    .Where(p => p.Title?.ToLower().Contains(lowerFilter) == true)
                    .ToList();
            }

            // ✅ Filtrado por categoría
            if (!string.IsNullOrWhiteSpace(selectedCategory) && selectedCategory != "All")
            {
                if (Enum.TryParse<PromptCategory>(selectedCategory, out var parsedCategory))
                {
                    allPrompts = allPrompts.Where(p => p.Category == parsedCategory).ToList();
                }
            }

            // Paginación
            return allPrompts.Skip(offset).Take(limit).ToList();
        }

        // =============================== 🔹 OPERACIONES SOBRE FAVORITOS ===============================

        /// <summary>
        /// Actualiza el estado de favorito de un prompt en la base de datos.
        /// </summary>

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

        // =============================== 🔹 OBTENER CONTADOR DE PROMPTS ===============================

        /// <summary>
        /// Obtiene la cantidad total de prompts favoritos que coinciden con un filtro.
        /// </summary>
        public Task<int> GetTotalPromptsCountAsync(string filterText, Filters dateFilter, string category) => GetTotalCountAsync(dateFilter, filterText, category);

        private async Task<int> GetTotalCountAsync(Filters filterType, string filterText = null, string category = null)
        {
            // Cargar todos los datos y aplicar filtros en memoria para evitar problemas con
            // funciones no soportadas en SQLite
            var prompts = await _database.Table<PromptTemplate>().ToListAsync();

            // Filtrado por fecha

            DateTime today = DateTime.Today;

            DateTime end = today.AddDays(1); // Fin del día actual

            prompts = filterType switch
            {
                Filters.Today => prompts.Where(p => p.CreatedAt >= today && p.CreatedAt < end).ToList(),
                Filters.Last7Days => prompts.Where(p => p.CreatedAt >= today.AddDays(-7) && p.CreatedAt < end).ToList(),
                Filters.Favorites => prompts.Where(p => p.IsFavorite).ToList(),
                Filters.NonFavorites => prompts.Where(p => !p.IsFavorite).ToList(),

                _ => prompts
            };

            // Filtrado por texto
            if (!string.IsNullOrWhiteSpace(filterText))
            {
                string lowerFilter = filterText.ToLower();

                prompts = prompts.Where(p => p.Title?.ToLower().Contains(lowerFilter) == true).ToList();
            }

            // ✅ Filtrado por categoría
            if (!string.IsNullOrWhiteSpace(category) && category != "All")
            {
                if (Enum.TryParse<PromptCategory>(category, out var parsedCategory))
                {
                    prompts = prompts.Where(p => p.Category == parsedCategory).ToList();
                }
            }

            return prompts.Count;
        }

        /// <summary>
        /// Borra completamente la base de datos eliminando todas las tablas.
        /// </summary>
        public async Task RestoreDatabaseAsync()
        {
            // Cierra cualquier operación pendiente antes de borrar
            //await _database.CloseAsync();

            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "QuickPrompt.db3");

            if (File.Exists(dbPath))
                File.Delete(dbPath);

            // Recrear la instancia de conexión y reiniciar base de datos
            var newConnection = _database;

            await InitializeDatabaseAsync();

        }
        public async Task RestoreDatabaseAsync(SQLiteAsyncConnection newConnection)
        {
            _database = newConnection;

            await InitializeDatabaseAsync(); // usa la nueva conexión para tabla FinalPrompt
        }


        /// <summary>
        /// Verifica si el archivo de base de datos ya existe en el dispositivo.
        /// </summary>
        public static bool DatabaseExists()
        {
            string dbPath = Path.Combine(FileSystem.AppDataDirectory, "QuickPrompt.db3");

            return File.Exists(dbPath);
        }
    }
}