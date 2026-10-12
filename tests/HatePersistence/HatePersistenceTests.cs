using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using FullSerializer;
using GiveUpNation;
using HarmonyLib;
using PavonisInteractive.TerraInvicta;

internal static class Program
{
    private static int checks;
    private static readonly List<Exception> errors = new List<Exception>();
    private static HateHistorySaveData snapshot, staged;
    private static bool importing;
    private static readonly Type SaveType = typeof(SaveStructure);
    private static readonly fsSerializer Serializer = (fsSerializer)AccessTools.Field(typeof(StringSerializationAPI), "_serializer").GetValue(null);
    private static readonly SaveStructure EmptyGame = new SaveStructure
    {
        gamestates = new Dictionary<Type, Dictionary<GameStateID, TIGameState>>()
    };
    private static void Check(bool ok, string name)
    {
        if (!ok) throw new Exception(name);
        checks++;
        Console.WriteLine("PASS " + name);
    }
    private static HateHistorySaveData Sample(string cause = "Jealousy and Deescelation")
    {
        var history = new HateHistory();
        history.Add("1 October 2030", cause, 3.25f, 20.125f, 21.75f);
        history.Add("2 October 2030", "Periodic hate", -1, 21.75f, 21.5f);
        return history.Snapshot("42");
    }
    private static string Serialize() => fsJsonPrinter.PrettyJson(StringSerializationAPI.Serialize(SaveType, EmptyGame));
    private static SaveStructure Deserialize(string text) => (SaveStructure)StringSerializationAPI.Deserialize(SaveType, text);
    private static void Reject(fsData data, string name)
    {
        try { HateHistorySaveCodec.Decode(data); }
        catch (FormatException) { Check(true, name); return; }
        throw new Exception("Accepted invalid section: " + name);
    }
    private static fsData Section() => HateHistorySaveCodec.Encode(Sample());
    private static int Main()
    {
        try { Run(); return 0; }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
    private static void Run()
    {
        CodecChecks();
        NativeSerializerChecks();
        ScopeChecks();
        Check(MainErrors.Count == 0, "no unexpected persistence adapter failures");
        Console.WriteLine($"{checks} persistence checks passed (installed serializer/native SaveStructure.Load; synthetic saves, no live game).");
#if NETFRAMEWORK
        Console.WriteLine("Production hook lifecycle exercised through real Harmony detours on .NET Framework fixtures.");
#else
        Console.WriteLine("Production hook lifecycle invoked directly; bundled Harmony detours require the separate net48 run.");
#endif
    }
    private static List<Exception> MainErrors => GiveUpNation.Main.Errors;

    private static void CodecChecks()
    {
        var restored = HateHistorySaveCodec.Decode(Section());
        Check(restored.FactionId == "42" && restored.Entries.Length == 2, "schema round-trip and faction identity");
        Check(restored.Entries[0].Before == 20.125f && restored.Entries[0].Requested == 3.25f, "raw float values preserved");
        var ring = new HateHistory();
        ring.Restore(restored);
        Check(ring.Recent(0).Cause == "Periodic hate", "chronological order restored");
        var copy = ring.Snapshot("42");
        ring.Add("later", "new timeline", 1, 21.5f, 22.5f);
        Check(copy.Entries.Length == 2 && copy.Entries[1].After == 21.5f, "save snapshot detached from later changes");
        for (int i = 0; i < 200; i++) ring.Add("date", i.ToString(), 1, i, i + 1);
        var capped = HateHistorySaveCodec.Decode(HateHistorySaveCodec.Encode(ring.Snapshot("42")));
        Check(capped.Entries.Length == 100 && capped.Entries[0].Before == 100 && capped.Entries[99].Before == 199,
            "full circular buffer persists latest 100 in order");
        var culture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            var json = fsJsonPrinter.PrettyJson(Section());
            Check(HateHistorySaveCodec.Decode(fsJsonParser.Parse(json)).Entries[0].Before == 20.125f,
                "JSON round-trip independent of decimal-comma locale");
        }
        finally { CultureInfo.CurrentCulture = culture; }
        var bounded = HateHistorySaveCodec.Decode(HateHistorySaveCodec.Encode(Sample(new string('x', 2000))));
        Check(bounded.Entries[0].Cause.Length == 512, "saved cause length bounded");
        var unicode = Sample("Alien <target> \"name\" \\ path\n日本語");
        Check(HateHistorySaveCodec.Decode(fsJsonParser.Parse(fsJsonPrinter.PrettyJson(HateHistorySaveCodec.Encode(unicode))))
            .Entries[0].Cause == unicode.Entries[0].Cause, "JSON escaping and Unicode preserved");
        Reject(new fsData("bad"), "non-object section rejected");
        var data = Section(); data.AsDictionary["version"] = new fsData(99L);
        Reject(data, "future schema rejected safely");
        data = Section(); data.AsDictionary["version"] = new fsData("1");
        Reject(data, "wrong schema type rejected");
        data = Section(); data.AsDictionary.Remove("factionId");
        Reject(data, "missing faction rejected");
        data = Section(); data.AsDictionary["factionId"] = new fsData("");
        Reject(data, "empty faction rejected");
        data = Section(); data.AsDictionary["hateHistory"] = new fsData("bad");
        Reject(data, "invalid entry collection rejected");
        data = Section(); var rows = data.AsDictionary["hateHistory"].AsList;
        while (rows.Count < 101) rows.Add(rows[0]);
        Reject(data, "oversized entry collection rejected");
        data = Section(); data.AsDictionary["hateHistory"].AsList[1] = new fsData(false);
        Reject(data, "bad later entry rejects whole section");
        foreach (var number in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue })
        {
            data = Section(); data.AsDictionary["hateHistory"].AsList[0].AsDictionary["before"] = new fsData(number);
            Reject(data, "non-finite/overflow number rejected: " + number);
        }
        data = Section(); data.AsDictionary["hateHistory"].AsList[0].AsDictionary["date"] = new fsData(new string('x', 129));
        Reject(data, "oversized date rejected");
        data = Section(); data.AsDictionary["hateHistory"].AsList[0].AsDictionary.Remove("after");
        Reject(data, "missing numeric field rejected");
        data = Section(); data.AsDictionary["unrelatedFutureField"] = new fsData(true);
        Check(HateHistorySaveCodec.Decode(data).Entries.Length == 2, "unknown fields within current schema tolerated");
        Check(!fsJsonPrinter.PrettyJson(Section()).Contains("$type"), "payload contains no mod type metadata");
    }

    private static void NativeSerializerChecks()
    {
        string baseline = Serialize();
        var processor = new HateHistorySaveProcessor(SaveType, () => snapshot, () => importing,
            data => staged = data, e => errors.Add(e));
        Serializer.AddProcessor(processor);
        try
        {
            Check(Serialize() == baseline, "unscoped serialization unchanged");
            snapshot = Sample();
            string enhanced = Serialize();
            var root = fsJsonParser.Parse(enhanced);
            root.AsDictionary.Remove(HateHistorySaveCodec.Key);
            Check(fsJsonPrinter.PrettyJson(root) == baseline, "native save fields unchanged by extension");
            staged = null; importing = false;
            Deserialize(enhanced);
            Check(staged == null, "preview/deserialization outside actual load does not import");
            importing = true;
            var game = Deserialize(enhanced);
            Check(game.gamestates != null && staged.Entries.Length == 2, "actual native deserialize imports history");
            Deserialize(baseline);
            Check(staged == null, "old save without section starts empty");
            foreach (var broken in new[] { new fsData("broken"), new fsData(new Dictionary<string, fsData> { ["version"] = new fsData(999L) }) })
            {
                root = fsJsonParser.Parse(enhanced);
                root.AsDictionary[HateHistorySaveCodec.Key] = broken;
                Check(Deserialize(fsJsonPrinter.PrettyJson(root)).gamestates != null && staged == null,
                    "bad mod section does not break native save loading");
            }
            Check(errors.Count == 2, "malformed sections reported");
            var scratch = Path.Combine(Path.GetTempPath(), "ti-history-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(scratch);
            try
            {
                string plain = Path.Combine(scratch, "fixture.json");
                string gzip = Path.Combine(scratch, "fixture.json.gz");
                File.WriteAllText(plain, enhanced);
                using (var stream = File.Create(gzip))
                using (var zip = new GZipStream(stream, CompressionMode.Compress))
                using (var writer = new StreamWriter(zip)) writer.Write(enhanced);
                foreach (string path in new[] { plain, gzip })
                {
                    staged = null;
                    var loaded = SaveStructure.Load(path);
                    Check(loaded.gamestates != null && staged.Entries[0].Cause == snapshot.Entries[0].Cause,
                        "native file loader round-trip: " + Path.GetExtension(path));
                }
                Serializer.RemoveProcessor<HateHistorySaveProcessor>();
                foreach (string path in new[] { plain, gzip })
                    Check(SaveStructure.Load(path).gamestates != null, "native file loads without mod: " + Path.GetExtension(path));
                var withoutMod = Deserialize(enhanced);
                string resaved = fsJsonPrinter.PrettyJson(StringSerializationAPI.Serialize(SaveType, withoutMod));
                Check(!resaved.Contains(HateHistorySaveCodec.Key), "saving without mod discards unknown extension");
                Serializer.AddProcessor(processor);
            }
            finally { Directory.Delete(scratch, true); }
            var unrelated = StringSerializationAPI.Serialize(typeof(Dictionary<string, string>), new Dictionary<string, string> { ["x"] = "y" });
            Check(!unrelated.AsDictionary.ContainsKey(HateHistorySaveCodec.Key), "unrelated serialized types untouched");
            // Processor failures must not change base save data or reject native data.
            root = fsJsonParser.Parse(baseline);
            root.AsDictionary[HateHistorySaveCodec.Key] = new fsData("existing");
            processor.OnAfterSerialize(SaveType, EmptyGame, ref root);
            Check(root.AsDictionary[HateHistorySaveCodec.Key].AsString == "existing", "existing section never overwritten");
        }
        finally
        {
            Serializer.RemoveProcessor<HateHistorySaveProcessor>();
            snapshot = staged = null; importing = false;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static string NativeSaveFixture(bool fail)
    {
        if (fail) throw new IOException("fixture save failure");
        return Serialize();
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool NativeLoadFixture(string data, int outcome)
    {
        Deserialize(data);
        if (outcome == 2) throw new IOException("fixture load failure");
        return outcome == 1;
    }
    private static string SaveFixture(bool fail)
    {
#if NETFRAMEWORK
        return NativeSaveFixture(fail);
#else
        object[] state = { null };
        AccessTools.Method(typeof(AlienHateSavePersistence), "BeforeSave").Invoke(null, state);
        try { return NativeSaveFixture(fail); }
        finally { AccessTools.Method(typeof(AlienHateSavePersistence), "FinishSave").Invoke(null, state); }
#endif
    }
    private static bool LoadFixture(string data, int outcome)
    {
#if NETFRAMEWORK
        return NativeLoadFixture(data, outcome);
#else
        object[] state = { null };
        AccessTools.Method(typeof(AlienHateSavePersistence), "BeforeLoad").Invoke(null, state);
        bool result = false;
        Exception failure = null;
        try { return result = NativeLoadFixture(data, outcome); }
        catch (Exception e) { failure = e; throw; }
        finally { AccessTools.Method(typeof(AlienHateSavePersistence), "FinishLoad").Invoke(null, new[] { (object)result, failure, state[0] }); }
#endif
    }
    private static void ScopeChecks()
    {
        // Execute the production prefix/finalizer using real Harmony detours on
        // safe fixture entry points. Never invoke Unity/gameplay native methods.
#if NETFRAMEWORK
        var patcher = new Harmony("GiveUpNation.Tests.SaveScopes");
        var saveMethod = AccessTools.Method(typeof(Program), nameof(NativeSaveFixture));
        var loadMethod = AccessTools.Method(typeof(Program), nameof(NativeLoadFixture));
        HarmonyMethod Hook(string name) => new HarmonyMethod(typeof(AlienHateSavePersistence), name);
#endif
        var saveField = AccessTools.Field(typeof(AlienHateSavePersistence), "saving");
        var loadField = AccessTools.Field(typeof(AlienHateSavePersistence), "loading");
        var processor = new HateHistorySaveProcessor(SaveType,
            () => ((AlienHateSavePersistence.SaveFrame)saveField.GetValue(null))?.Snapshot,
            () => loadField.GetValue(null) != null,
            data => ((AlienHateSavePersistence.LoadFrame)loadField.GetValue(null)).Snapshot = data,
            e => MainErrors.Add(e));
        Serializer.AddProcessor(processor);
        try
        {
#if NETFRAMEWORK
            patcher.Patch(saveMethod, prefix: Hook("BeforeSave"), finalizer: Hook("FinishSave"));
            patcher.Patch(loadMethod, prefix: Hook("BeforeLoad"), finalizer: Hook("FinishLoad"));
#endif
            AlienHateHistory.Export = Sample();
            string text = SaveFixture(false);
            Check(text.Contains(HateHistorySaveCodec.Key) && saveField.GetValue(null) == null, "save scope captures snapshot and releases it");
            try { SaveFixture(true); throw new Exception("save exception swallowed"); }
            catch (IOException) { Check(saveField.GetValue(null) == null, "save exception preserved and scope cleared"); }
            Check(!Serialize().Contains(HateHistorySaveCodec.Key), "failed save does not leak snapshot to later serialization");
            Check(LoadFixture(text, 1) && AlienHateHistory.Restored != null && loadField.GetValue(null) == null,
                "successful load commits staged history and clears scope");
            Check(!LoadFixture(text, 0) && AlienHateHistory.Restored == null && loadField.GetValue(null) == null,
                "false load result discards staged history");
            try { LoadFixture(text, 2); throw new Exception("load exception swallowed"); }
            catch (IOException)
            {
                Check(AlienHateHistory.Restored == null && loadField.GetValue(null) == null, "load exception preserved and staged history discarded");
            }
            AlienHateHistory.Restored = null;
            Deserialize(text);
            Check(AlienHateHistory.Restored == null, "preview after failed load cannot commit history");
            Exception workerError = null;
            var worker = new Thread(() =>
            {
                try { LoadFixture(text, 1); }
                catch (Exception e) { workerError = e; }
            });
            worker.Start(); worker.Join();
            Check(workerError == null && AlienHateHistory.Restored != null && loadField.GetValue(null) == null,
                "worker-thread load uses its own scope and hands off history");
            object[] args = { null };
            AccessTools.Method(typeof(AlienHateSavePersistence), "BeforeLoad").Invoke(null, args);
            var frame = (AlienHateSavePersistence.LoadFrame)args[0];
            frame.Snapshot = Sample();
            AlienHateSavePersistence.Reset();
            AlienHateHistory.Restored = null;
            AccessTools.Method(typeof(AlienHateSavePersistence), "FinishLoad").Invoke(null, new object[] { true, null, frame });
            Check(AlienHateHistory.Restored == null, "disable/re-enable invalidates in-flight load frame");
            object[] olderArgs = { null }, newerArgs = { null };
            var begin = AccessTools.Method(typeof(AlienHateSavePersistence), "BeforeLoad");
            var finish = AccessTools.Method(typeof(AlienHateSavePersistence), "FinishLoad");
            begin.Invoke(null, olderArgs);
            begin.Invoke(null, newerArgs);
            var older = (AlienHateSavePersistence.LoadFrame)olderArgs[0];
            var newer = (AlienHateSavePersistence.LoadFrame)newerArgs[0];
            older.Snapshot = Sample("older load"); newer.Snapshot = Sample("newer load");
            finish.Invoke(null, new object[] { true, null, newer });
            finish.Invoke(null, new object[] { true, null, older });
            Check(AlienHateHistory.Restored.Entries[0].Cause == "newer load", "superseded load cannot overwrite newer history");
            begin.Invoke(null, olderArgs);
            older = (AlienHateSavePersistence.LoadFrame)olderArgs[0]; older.Snapshot = Sample();
            AccessTools.Method(typeof(AlienHateSavePersistence), "ClearCampaign").Invoke(null, null);
            finish.Invoke(null, new object[] { true, null, older });
            Check(AlienHateHistory.Restored == null, "campaign clear invalidates pending load frame");
        }
        finally
        {
#if NETFRAMEWORK
            patcher.UnpatchAll(patcher.Id);
#endif
            Serializer.RemoveProcessor<HateHistorySaveProcessor>();
            AlienHateSavePersistence.Reset();
        }
        // Validate the exact installed API boundary without starting a campaign.
        Check(AccessTools.Method(typeof(GameStateManager), "SaveAllGameStates").ReturnType == typeof(bool)
            && AccessTools.Method(typeof(GameStateManager), "LoadAllGameStates").ReturnType == typeof(bool), "installed save/load signatures match hooks");
        // Registering on the real game methods requires Unity's Mono runtime:
        // desktop CLR rejects their Unity ECall dependencies during preparation.
    }
}

namespace GiveUpNation
{
    internal static class Main
    {
        internal static bool Enabled = true;
        internal static readonly List<Exception> Errors = new List<Exception>();
        internal static void Log(Exception e) { Errors.Add(e); }
    }
    // Only the live campaign adapter is a stand-in. Codec, processor, scopes,
    // Harmony, fsSerializer and SaveStructure.Load above are production code.
    internal static class AlienHateHistory
    {
        internal static HateHistorySaveData Export, Restored;
        internal static HateHistorySaveData ExportForSave() => Export;
        internal static void BeginLoad() { Restored = null; }
        internal static void FinishLoad(HateHistorySaveData data) { Restored = data; }
        internal static void ClearCampaign() { Restored = null; }
    }
}
