using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.Concurrent;

namespace Phraseback.Client;

/// <summary>Single-flight commands with an independent notification reader. Failed transports are never reused.</summary>
public sealed class EngineClient : IAsyncDisposable
{
    private readonly Process process;
    private readonly SemaphoreSlim gate = new(1);
    private readonly Task diagnostics;
    private readonly Task reader;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<Envelope>> replies = new();
    private readonly object notificationGate = new();
    private readonly Queue<OperationStatus> saved = new();
    private readonly SemaphoreSlim notificationReady = new(0, 1);
    private OperationStatus? latest;
    private OperationStatus? organization;
    private readonly SemaphoreSlim organizationReady = new(0, 1);
    private Exception? transportFailure;
    private string? session;
    private long nextId;
    private bool closed;
    private Task? disposal;
    private readonly TaskCompletionSource disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int ProcessId => process.Id;
    public Task Disconnected => disconnected.Task;

    private EngineClient(Process child)
    {
        process = child;
        // Drain diagnostics without recording user content or blocking the engine.
        diagnostics = Task.Run(async () => { while (await process.StandardError.ReadLineAsync() is not null) { } });
        reader = ReadAsync();
    }

    public static async Task<EngineClient> StartAsync(string executable, string dataRoot, Action<int>? supervise = null)
    {
        var info = new ProcessStartInfo(executable)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        info.ArgumentList.Add("--data-root");
        info.ArgumentList.Add(Path.GetFullPath(dataRoot));
        var child = Process.Start(info) ?? throw new IOException("The engine could not start.");
        var client = new EngineClient(child);
        try
        {
            supervise?.Invoke(child.Id);
            await client.RequestAsync<JsonElement>("hello", new { notifications = true });
            return client;
        }
        catch { await client.DisposeAsync(); throw; }
    }

    public async Task<T> RequestAsync<T>(string method, object parameters, CancellationToken cancellation = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        await gate.WaitAsync(deadline.Token);
        try
        {
            ObjectDisposedException.ThrowIf(closed, this);
            var payload = JsonSerializer.SerializeToElement(parameters, Contract.Json);
            CommandPayloads.ValidateRequest(method, payload);
            var id = ++nextId;
            var body = JsonSerializer.SerializeToUtf8Bytes(new { protocol = Contract.Major, id, method, @params = parameters }, Contract.Json);
            if (body.Length > Contract.MaximumMessageBytes) throw new IOException("Request exceeds the protocol limit.");
            var completion = new TaskCompletionSource<Envelope>(TaskCreationOptions.RunContinuationsAsynchronously);
            replies[id] = completion;
            var header = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(header, body.Length);
            try
            {
                await process.StandardInput.BaseStream.WriteAsync(header, deadline.Token);
                await process.StandardInput.BaseStream.WriteAsync(body, deadline.Token);
                await process.StandardInput.BaseStream.FlushAsync(deadline.Token);
                var response = await completion.Task.WaitAsync(deadline.Token);
                if (response.Protocol != Contract.Major || response.Id != id || string.IsNullOrEmpty(response.Session)
                    || (session is not null && response.Session != session)) throw new IOException("Engine protocol identity mismatch.");
                session = response.Session;
                if (response.Error is not null) throw new EngineException(response.Error.Code, response.Error.Message);
                // Null is a valid result (for example, no foreground operation).
                var result = response.Result ?? JsonSerializer.SerializeToElement<object?>(null);
                CommandPayloads.ValidateResponse(method, payload, result);
                return result.Deserialize<T>(Contract.Json)!;
            }
            catch (EngineException) { throw; }
            catch { closed = true; Kill(); throw; }
            finally { replies.TryRemove(id, out _); }
        }
        finally { gate.Release(); }
    }

    private async Task ReadAsync()
    {
        try
        {
            var header = new byte[4];
            while (true)
            {
                await process.StandardOutput.BaseStream.ReadExactlyAsync(header);
                var length = BinaryPrimitives.ReadInt32LittleEndian(header);
                if (length is <= 0 or > Contract.MaximumMessageBytes) throw new IOException("Invalid response length.");
                var payload = new byte[length];
                await process.StandardOutput.BaseStream.ReadExactlyAsync(payload);
                using var document = JsonDocument.Parse(payload);
                if (document.RootElement.TryGetProperty("event", out _))
                {
                    var notification = document.RootElement.Deserialize<EngineNotification>(Contract.Json)!;
                    if (notification.Protocol != Contract.Major || string.IsNullOrEmpty(session) || notification.Session != session)
                        throw new IOException("Engine notification identity mismatch.");
                    if (notification.Event is not ("operation_status" or "operation_saved" or "organization_status")) throw new IOException("Unknown engine notification.");
                    var status = notification.Data.Deserialize<OperationStatus>(Contract.Json) ?? throw new IOException("Invalid operation notification.");
                    lock (notificationGate)
                    {
                        if (notification.Event == "organization_status")
                        {
                            organization = status;
                            if (organizationReady.CurrentCount == 0) organizationReady.Release();
                            continue;
                        }
                        if (notification.Event == "operation_saved")
                        {
                            if (saved.Count >= 128) throw new IOException("The shell cannot keep up with persistence acknowledgements.");
                            saved.Enqueue(status);
                        }
                        else latest = status; // Progress is coalesced; completion is retained until consumed.
                        if (notificationReady.CurrentCount == 0) notificationReady.Release();
                    }
                }
                else
                {
                    if (!document.RootElement.TryGetProperty("result", out _) || !document.RootElement.TryGetProperty("error", out _))
                        throw new IOException("Incomplete engine response envelope.");
                    var response = document.RootElement.Deserialize<Envelope>(Contract.Json) ?? throw new IOException("Invalid engine response.");
                    // Establish the session before the next notification is read, not
                    // in the request continuation, which may be scheduled later.
                    if (response.Protocol != Contract.Major || string.IsNullOrEmpty(response.Session)
                        || (session is not null && session != response.Session)) throw new IOException("Engine response identity mismatch.");
                    session = response.Session;
                    if (!replies.TryGetValue(response.Id, out var completion) || !completion.TrySetResult(response))
                        throw new IOException("Unexpected or duplicate engine response.");
                }
            }
        }
        catch (Exception ex)
        {
            closed = true;
            lock (notificationGate)
            {
                transportFailure = ex;
                if (organizationReady.CurrentCount == 0) organizationReady.Release();
                if (notificationReady.CurrentCount == 0) notificationReady.Release();
            }
            foreach (var completion in replies.Values) completion.TrySetException(new IOException("Engine disconnected; reopen to recover saved progress.", ex));
            disconnected.TrySetResult();
            Kill();
        }
    }

    public async Task<OperationStatus> NextOperationAsync(string id, CancellationToken cancellation = default)
    {
        while (true)
        {
            lock (notificationGate)
            {
                if (transportFailure is not null) throw new IOException("Engine disconnected during the operation.", transportFailure);
                while (saved.TryDequeue(out var durable)) { if (durable.Id == id) return durable; }
                if (latest is { } status)
                {
                    latest = null;
                    if (status.Id == id) return status;
                }
            }
            await notificationReady.WaitAsync(cancellation);
        }
    }

    private void Kill()
    {
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (InvalidOperationException) { }
    }

    public async Task<OperationStatus> NextOrganizationAsync(string id, CancellationToken cancellation = default)
    {
        while (true)
        {
            lock (notificationGate)
            {
                if (transportFailure is not null) throw new IOException("Engine disconnected during organizing.", transportFailure);
                if (organization is { } status)
                {
                    organization = null;
                    if (status.Id == id) return status;
                }
            }
            await organizationReady.WaitAsync(cancellation);
        }
    }

    public async Task<Snapshot> RequestSnapshotAsync(string method, object parameters, CancellationToken cancellation = default, Action<OperationStatus>? openingProgress = null)
    {
        var request = JsonSerializer.SerializeToNode(parameters, Contract.Json)!.AsObject();
        request["paged"] = true;
        if (method == "open_project")
        {
            // Each command still has a bounded transport deadline. Recovery itself
            // runs on an engine worker and may legitimately exceed ten seconds.
            cancellation.ThrowIfCancellationRequested();
            var operation = await RequestAsync<OperationStatus>("prepare_open", request);
            try
            {
                while (!operation.Finished)
                {
                    openingProgress?.Invoke(operation);
                    await Task.Delay(75, cancellation);
                    var next = await RequestAsync<OperationStatus>("operation_status", new { });
                    if (next.Id != operation.Id) throw new IOException("Recording recovery identity changed.");
                    operation = next;
                }
                cancellation.ThrowIfCancellationRequested();
                if (operation.State != "completed") throw new EngineException("open_failed", operation.Error ?? "Recording could not be opened.");
            }
            catch (OperationCanceledException)
            {
                // Never leave an abandoned recovery writing after the caller cancels.
                await RequestAsync<JsonElement>("cancel_operation", new { id = operation.Id });
                throw;
            }
        }
        var snapshot = await RequestAsync<Snapshot>(method, request, cancellation);
        async Task<T[]> ReadPages<T>(string kind, int count)
        {
            var items = new List<T>(count);
            while (items.Count < count)
            {
                var page = await RequestAsync<MetadataPage<T>>("metadata_page", new { recording_id = snapshot.RecordingId, revision = snapshot.Revision, kind, offset = items.Count, limit = 128 }, cancellation);
                if (page.Revision != snapshot.Revision || page.Items.Length == 0 || items.Count + page.Items.Length > count)
                    throw new IOException("Invalid or obsolete metadata page.");
                items.AddRange(page.Items);
            }
            return items.ToArray();
        }
        var frames = await ReadPages<Frame>("frames", snapshot.FrameCount);
        var steps = await ReadPages<Step>("steps", snapshot.StepCount);
        return snapshot with { Project = snapshot.Project with { Frames = frames, Steps = steps } };
    }

    public ValueTask DisposeAsync() => new(disposal ??= DisposeCoreAsync());

    private async Task DisposeCoreAsync()
    {
        if (!closed)
        {
            using var shutdownDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try { await RequestAsync<JsonElement>("shutdown", new { }, shutdownDeadline.Token); }
            catch (Exception ex) when (ex is IOException or EngineException or OperationCanceledException or InvalidOperationException) { }
        }
        closed = true;
        try
        {
            process.StandardInput.Close();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) { Kill(); }
        finally { await reader; await diagnostics; process.Dispose(); }
    }
}
