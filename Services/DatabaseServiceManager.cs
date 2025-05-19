using QuickPrompt.Services.ServiceInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickPrompt.Services
{
    public class DatabaseServiceManager
    {
        private readonly DatabaseConnectionProvider _provider;

        private readonly IPromptRepository _promptRepo;

        private readonly IFinalPromptRepository _finalRepo;

        public DatabaseServiceManager(DatabaseConnectionProvider provider,
                                       IPromptRepository promptRepo,
                                       IFinalPromptRepository finalRepo)
        {
            _provider = provider;

            _promptRepo = promptRepo;

            _finalRepo = finalRepo;
        }

        public async Task RestoreAsync()
        {
            await _provider.RestoreDatabaseAsync(async conn =>
            {
                await _promptRepo.RestoreDatabaseAsync(conn);

                await _finalRepo.RestoreDatabaseAsync(conn);
            });
        }

    }

}
