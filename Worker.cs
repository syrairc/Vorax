using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using EmberHUD;
using Vorax.Sim;

namespace Vorax;

// A settings change means a new Engine and a new Worker. No timeout on a looping strategy: only the generation check drops its late result.
public sealed class Worker(Engine engine) : IDisposable
{
    sealed record Key(string State, Strategy? Strategy);

    sealed record Job(VoraxState State, Strategy? Strategy, MapOptions Options, Key Key);

    readonly object gate = new();
    readonly SemaphoreSlim wake = new(0);
    readonly RunTracker tracker = new();
    Thread? thread;
    Job? pending;
    Key? submitted;
    Key? resultKey;
    Advice? advice;
    bool disposed;

    public Advice? Latest
    {
        get
        {
            lock (gate) return advice;
        }
    }

    internal bool ThreadAlive => thread?.IsAlive == true;

    public bool Matches(VoraxState state)
    {
        var stateKey = StateKey(state);
        lock (gate) return advice is not null && resultKey!.State == stateKey;
    }

    public void Submit(VoraxState state, Strategy? strategy)
    {
        var key = new Key(StateKey(state), strategy);
        lock (gate)
        {
            if (disposed) return;
            var options = tracker.Update(state);
            if (key == submitted) return;
            submitted = key;
            advice = null;
            pending = new(state, strategy, options, key);
            thread ??= StartThread();
        }

        wake.Release();
    }

    public void Dispose()
    {
        Thread? stopped;
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            pending = null;
            advice = null;
            stopped = thread;
        }

        wake.Release();
        stopped?.Join(2000);
    }

    Thread StartThread()
    {
        var created = new Thread(Loop) { IsBackground = true, Name = "Vorax advisor" };
        created.Start();
        return created;
    }

    void Loop()
    {
        while (true)
        {
            wake.Wait();
            Job job;
            lock (gate)
            {
                if (disposed) return;
                if (pending is null) continue;
                job = pending;
                pending = null;
            }

            var result = Compute(job);
            lock (gate)
            {
                if (disposed || job.Key != submitted) continue;
                advice = result;
                resultKey = job.Key;
            }
        }
    }

    Advice? Compute(Job job)
    {
        try { return engine.Advise(job.State, job.Strategy, job.Options); }
        catch (Exception error)
        {
            Log.Error(error, "vorax advisor");
            return new(Stage.None, AdviceSource.Simulator, "", new([], ""), null, null, $"{error.GetType().Name}: {error.Message}");
        }
    }

    static string StateKey(VoraxState state)
    {
        var key = new StringBuilder();
        key.Append(state.Phase).Append('|').Append(state.Round).Append('|').Append(state.Rerolls).Append('|').Append(state.SelectedCard).Append("|o");
        foreach (var card in state.Offered) key.Append(' ').Append(card.Slot).Append(':').Append(card.Id).Append(':').Append((int)card.Kind);
        key.Append("|i");
        foreach (var card in state.Instruments) key.Append(' ').Append(card.Id);
        key.Append("|m");
        foreach (var m in state.Monsters) key.Append(' ').Append(m.Slot).Append(':').Append(m.Species).Append(':').Append(m.Rarity).Append(':').Append(m.Activity).Append(':').Append(m.Count);
        key.Append("|s").AppendJoin(' ', state.SelectedMonsters);
        return key.ToString();
    }
}
