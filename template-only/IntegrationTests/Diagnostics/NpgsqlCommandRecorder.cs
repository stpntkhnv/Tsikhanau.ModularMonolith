using System.Collections.Concurrent;
using System.Diagnostics;
using Tsikhanau.Monolith.Infrastructure.Diagnostics;

namespace Tsikhanau.Monolith.IntegrationTests.Diagnostics;

internal sealed class NpgsqlCommandRecorder : IDisposable
{
    private const String NpgsqlSourceName = "Npgsql";
    private const String QueryTextTag = "db.query.text";
    private const String StatementTag = "db.statement";

    private readonly ConcurrentQueue<RecordedCommand> _commands = new();
    private readonly HashSet<String> _listenedSourceNames;
    private readonly ActivityListener _activityListener;
    private Boolean _isRecording;

    public NpgsqlCommandRecorder(params String[] measuredSourceNames)
    {
        _listenedSourceNames = [NpgsqlSourceName, AppDiagnostics.SourceName, .. measuredSourceNames];
        _activityListener = new ActivityListener
        {
            ShouldListenTo = activitySource => _listenedSourceNames.Contains(activitySource.Name),
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = Record,
        };

        ActivitySource.AddActivityListener(_activityListener);
    }

    public IReadOnlyList<RecordedCommand> Commands => _commands.ToList();

    public IReadOnlyList<RecordedCommand> Unparented =>
        _commands
            .Where(command => !command.HasParent)
            .ToList();

    public IReadOnlyList<RecordedCommand> Parented =>
        _commands
            .Where(command => command.HasParent)
            .ToList();

    private Boolean IsRecording => Volatile.Read(ref _isRecording);

    public void Start() => Volatile.Write(ref _isRecording, true);

    public void Stop() => Volatile.Write(ref _isRecording, false);

    public void Dispose() => _activityListener.Dispose();

    private static Boolean IsCommandOrBatch(Activity activity) =>
        activity.Source.Name == NpgsqlSourceName && ReadQueryText(activity).Length > 0;

    private static String ReadQueryText(Activity activity) =>
        activity.GetTagItem(QueryTextTag) as String ?? activity.GetTagItem(StatementTag) as String ?? String.Empty;

    private static RecordedCommand CreateRecordedCommand(Activity activity) =>
        new(activity.DisplayName, ReadQueryText(activity), HasParent: activity.ParentId is not null);

    private void Record(Activity activity)
    {
        if (IsRecording && IsCommandOrBatch(activity))
        {
            _commands.Enqueue(CreateRecordedCommand(activity));
        }
    }
}
