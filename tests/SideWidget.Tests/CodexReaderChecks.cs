using Microsoft.Data.Sqlite;
using SideWidget.Infrastructure;

static class CodexReaderChecks
{
    public static void Run()
    {
        void Check(bool value,string name){if(!value)throw new Exception("Codex reader: "+name);}
        var directory=Path.Combine(Path.GetTempPath(),"sidewidget-codex-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            void Execute(string file,string sql)
            {
                using var connection=new SqliteConnection("Data Source="+Path.Combine(directory,file)+";Pooling=False");connection.Open();
                using var command=connection.CreateCommand();command.CommandText=sql;command.ExecuteNonQuery();
            }
            var rollout=Path.Combine(directory,"rollout.jsonl");File.WriteAllText(rollout,"");
            Execute("state_5.sqlite","CREATE TABLE threads(id TEXT,title TEXT,source TEXT,archived INTEGER,history_mode TEXT,rollout_path TEXT,updated_at INTEGER,updated_at_ms INTEGER,recency_at_ms INTEGER);"+
                "INSERT INTO threads VALUES('chat','Chat','vscode',0,'paginated','"+rollout.Replace("'","''")+"',1,1000,1000);");
            Execute("thread_history_1.sqlite","CREATE TABLE thread_turns(thread_id TEXT,turn_id TEXT,rollout_ordinal INTEGER,status TEXT,error_json TEXT,started_at INTEGER,completed_at INTEGER,rollout_end_ordinal INTEGER);"+
                "CREATE UNIQUE INDEX idx_thread_turns_page ON thread_turns(thread_id,rollout_ordinal);"+
                "CREATE TABLE thread_history_projection_state(thread_id TEXT,next_rollout_byte_offset INTEGER);"+
                "INSERT INTO thread_history_projection_state VALUES('chat',0);"+
                "INSERT INTO thread_turns VALUES('chat','old',1,'completed',NULL,1,2,3);"+
                "INSERT INTO thread_turns VALUES('chat','new',4,'inProgress',NULL,3,NULL,NULL);");
            Execute("queue_1.sqlite","CREATE TABLE queued_items(thread_id TEXT);");
            var reader=new CodexStateReader(directory);
            Check(reader.Read().Threads.Single().Status=="running","latest active turn overrides old success");
            Execute("thread_history_1.sqlite","UPDATE thread_turns SET status='completed',completed_at=4,rollout_end_ordinal=5 WHERE turn_id='new';");
            Check(reader.Read().Threads.Single().Status=="completed","explicit projected success");
            File.WriteAllText(rollout,"new work\n");
            Check(reader.Read().Threads.Single().Status=="unknown","lagging projection cannot report old success");
            File.WriteAllText(rollout,"");
            Execute("queue_1.sqlite","INSERT INTO queued_items VALUES('chat');");
            Check(reader.Read().Threads.Single().HasQueuedInput,"queued input blocks success");
            Execute("thread_history_1.sqlite","UPDATE thread_turns SET status='interrupted' WHERE turn_id='new';");
            Check(reader.Read().Threads.Single().Status=="interrupted","interruption never success");
            var api="{\"rateLimitsByLimitId\":{\"codex\":{\"limitId\":\"codex\",\"primary\":{\"usedPercent\":100,\"windowDurationMins\":300,\"resetsAt\":1795092281}},\"other\":{\"limitId\":\"other\",\"secondary\":{\"usedPercent\":17,\"windowDurationMins\":10080}}}}";
            var live=CodexRateLimitsReader.ParseApiResult(api,DateTimeOffset.UnixEpoch);
            Check(live?.Buckets.Count==2&&live.Buckets[0].Primary?.UsedPercent==100&&live.Buckets[1].Secondary?.UsedPercent==17,"official camelCase buckets retained independently");
            Check(CodexRateLimitsReader.ParseApiResult("{\"rateLimits\":{\"primary\":{\"usedPercent\":100,\"windowDurationMins\":0}}}",DateTimeOffset.UtcNow)==null,"invalid official window rejected");
            var weekly=CodexRateLimitsReader.ParseApiResult(api.Replace("300","10080"),DateTimeOffset.UnixEpoch);
            Check(weekly?.Buckets[0].Primary?.WindowMinutes==10080&&weekly.CheckedAt==DateTimeOffset.UnixEpoch,"actual window and API observation timestamp preserved");
            Check(CodexRateLimitsReader.ParseApiResult(api.Replace("100,","101,"),DateTimeOffset.UtcNow)?.Buckets.All(b=>b.Id!="codex")==true,"invalid quota bucket rejected");
            Execute("state_5.sqlite","UPDATE threads SET history_mode='future-format';");
            Check(reader.Read().Threads.Single().Status=="unknown","unsupported history format fails closed");
            File.Delete(Path.Combine(directory,"queue_1.sqlite"));
            Check(!reader.Read().Available,"missing queue metadata cannot report safe completion");
            Console.WriteLine("PASS Codex readers: latest turn, projection lag, queue, interruption, real quota and source freshness");
        }
        finally {Directory.Delete(directory,true);}
    }
}
