using Microsoft.Data.Sqlite;
using QuickPrompt.Local;

namespace QuickPrompt.Web.Infrastructure;
public sealed partial class LocalStore
{
    private static void ValidateTransferActivity(WebTransferActivity a)
    {
        if(a is null || a.Id==Guid.Empty || a.Provider is not(ProviderId.ChatGptWeb or ProviderId.ClaudeWeb) || !Enum.IsDefined(a.Action) || !Enum.IsDefined(a.State) || !Enum.IsDefined(a.Phase) || string.IsNullOrWhiteSpace(a.Code) || a.Code.Length>100 || string.IsNullOrWhiteSpace(a.AdapterVersion) || a.AdapterVersion.Length>30 || !double.IsFinite(a.DurationMs) || a.DurationMs<0 || a.Revision is <1 || a.Phase!=WebTransferSession.PhaseFor(new(a.Id,a.State,a.Code,a.DurationMs,a.AdapterVersion)))throw new ValidationException("Registro de transferencia inválido / Invalid transfer record.");
    }
    public void RecordTransfer(WebTransferActivity value)=>Write((db,tx)=>{
        ValidateTransferActivity(value);if(GetOne<WebTransferActivity>(db,"web-transfer",value.Id,tx)==null)Put(db,tx,"web-transfer",value.Id.ToString(),value);PruneTransfers(db,tx);return true;
    });
    private static void PruneTransfers(SqliteConnection db,SqliteTransaction tx)
    {using var c=db.CreateCommand();c.Transaction=tx;c.CommandText="DELETE FROM Documents WHERE Kind='web-transfer' AND Id NOT IN(SELECT Id FROM Documents WHERE Kind='web-transfer' ORDER BY json_extract(Payload,'$.createdAt') DESC,Id LIMIT 200)";c.ExecuteNonQuery();}
    public List<WebTransferActivity> Transfers()=>Read(db=>Get<WebTransferActivity>(db,"web-transfer").OrderByDescending(x=>x.CreatedAt).ToList());
}
