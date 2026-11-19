using Microsoft.Data.Sqlite;
using SideWidget.Core;

namespace SideWidget.Infrastructure;

/// <summary>Reads local Codex metadata only. Never migrates or repairs Codex databases.</summary>
public sealed class CodexStateReader(string directory)
{
    internal static SqliteConnection OpenReadOnly(string directory,string name)
    {
        var path=Path.Combine(directory,name);
        if(!File.Exists(path))throw new FileNotFoundException("Codex metadata unavailable.");
        var connection=new SqliteConnection(new SqliteConnectionStringBuilder
        {DataSource=path,Mode=SqliteOpenMode.ReadOnly,Pooling=false,DefaultTimeout=1}.ToString());
        try {connection.Open();return connection;}catch{connection.Dispose();throw;}
    }

    public CodexThreadsSnapshot Read()=>Read(null);

    public CodexThreadsSnapshot Read(IReadOnlyList<string>? selectedIds)
    {
        var now=DateTimeOffset.UtcNow;
        try
        {
            using var state=OpenReadOnly(directory,"state_5.sqlite");
            using var history=OpenReadOnly(directory,"thread_history_1.sqlite");
            using var queue=OpenReadOnly(directory,"queue_1.sqlite");
            using var historyTransaction=history.BeginTransaction(deferred:true);
            using var threads=state.CreateCommand();
            threads.CommandText="SELECT id,title,history_mode,rollout_path,COALESCE(updated_at_ms,updated_at*1000) FROM threads WHERE archived=0 AND source IN ('vscode','cli')";
            if(selectedIds is not null)
            {
                threads.CommandText+=" AND id IN (SELECT value FROM json_each($selected))";
                threads.Parameters.AddWithValue("$selected",System.Text.Json.JsonSerializer.Serialize(selectedIds));
            }
            threads.CommandText+=" ORDER BY recency_at_ms DESC,id DESC";
            using var turns=history.CreateCommand();turns.Transaction=historyTransaction;
            turns.CommandText="SELECT turn_id,status,error_json IS NOT NULL,completed_at,rollout_end_ordinal FROM thread_turns WHERE thread_id=$id ORDER BY rollout_ordinal DESC LIMIT 1";
            var turnIdParameter=turns.Parameters.AddWithValue("$id","");
            using var projection=history.CreateCommand();projection.Transaction=historyTransaction;
            projection.CommandText="SELECT next_rollout_byte_offset FROM thread_history_projection_state WHERE thread_id=$id";
            var projectionId=projection.Parameters.AddWithValue("$id","");
            using var queued=queue.CreateCommand();
            queued.CommandText="SELECT EXISTS(SELECT 1 FROM queued_items WHERE thread_id=$id)";
            var queuedId=queued.Parameters.AddWithValue("$id","");
            var result=new List<CodexThreadInfo>();
            using var rows=threads.ExecuteReader();
            while(rows.Read())
            {
                var id=rows.GetString(0);var title=rows.GetString(1);var mode=rows.GetString(2);
                turnIdParameter.Value=id;projectionId.Value=id;queuedId.Value=id;
                string? turnId=null;var status="unknown";
                using(var turn=turns.ExecuteReader())
                {
                    if(turn.Read()&&mode=="paginated")
                    {
                        turnId=turn.GetString(0);
                        status=turn.GetString(1) switch
                        {
                            "inProgress"=>"running",
                            "failed"=>"failed",
                            "interrupted"=>"interrupted",
                            "completed" when !turn.GetBoolean(2)&&!turn.IsDBNull(3)&&!turn.IsDBNull(4)=>"completed",
                            _=>"unknown"
                        };
                    }
                }
                // Projection can lag a newly appended turn. Old successful turn must not win.
                if(status=="completed")
                {
                    var offset=projection.ExecuteScalar();
                    if(offset is not long position||rows.IsDBNull(3)||turnId is null||
                       !CodexTurnEvents.ConfirmsCompletion(rows.GetString(3),position,turnId))status="unknown";
                }
                DateTimeOffset? updated=null;
                if(!rows.IsDBNull(4))updated=DateTimeOffset.FromUnixTimeMilliseconds(rows.GetInt64(4));
                var hasQueued=Convert.ToInt64(queued.ExecuteScalar())!=0;
                result.Add(new(id,title,status,turnId,updated,hasQueued));
            }
            return new(true,now,result);
        }
        catch(Exception ex) when(ex is SqliteException or IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            return new(false,now,[],"Метаданные Codex недоступны или формат изменился.");
        }
    }
}
