using System;
using System.Text;
using UnityEngine;
using XLua;

[Hotfix]
public class HfTarget
{
    private int bias = 10;
    public int Add(int a, int b) { return a + b; }
    public static string Name() { return "cs"; }
    public Vector3 Scale(Vector3 v, float f) { return v * f; }
    public int Bias { get { return bias; } }
    public void Out(int a, out int tripled, ref int acc) { tripled = a * 2; acc += a; }
}

[Hotfix]
public class HfGeneric<T>
{
    public T Echo(T v) { return v; }
}

[Hotfix(HotfixFlag.IntKey)]
public class HfIntKey
{
    private int k = 3;
    public int Twice(int a) { return a * 2; }
    public static int Zero() { return 0; }
}

[CSharpCallLua]
public delegate int HfListed(int a, int b);

// Same signature as HfListed but not generated: the runtime has to find a bridge function by signature.
public delegate int HfUnlisted(int a, int b);

public static class HotfixSmoke
{
    static int pass, fail;
    static StringBuilder sb = new StringBuilder();

    static void Check(string name, object actual, object expected)
    {
        bool ok = Equals(actual, expected);
        if (ok) pass++; else fail++;
        sb.AppendLine("HOTFIXTEST " + (ok ? "ok  " : "FAIL") + " " + name + " = " + actual + (ok ? "" : " (expected " + expected + ")"));
    }

    static void Step(string name, Action action)
    {
        try { action(); }
        catch (Exception e) { fail++; sb.AppendLine("HOTFIXTEST FAIL " + name + " threw " + e.GetType().Name + ": " + e.Message.Split('\n')[0]); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Run()
    {
        var env = new LuaEnv();
        // A player build writes the IntKey id map during the build, after assets are packed, so read it from beside the executable.
        env.AddLoader((ref string file) =>
        {
            string map = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), "hotfix_id_map.lua.txt");
            return file == "hotfix_id_map" && !Application.isEditor && System.IO.File.Exists(map) ? System.IO.File.ReadAllBytes(map) : null;
        });
        var t = new HfTarget();
        Step("before", () =>
        {
            Check("before.Add", t.Add(1, 2), 3);
            Check("before.Name", HfTarget.Name(), "cs");
        });
        Step("instance", () =>
        {
            env.DoString("xlua.hotfix(CS.HfTarget, 'Add', function(self, a, b) return a + b + self.bias end)");
            Check("instance.Add (reads private field)", t.Add(1, 2), 13);
        });
        Step("static", () =>
        {
            env.DoString("xlua.hotfix(CS.HfTarget, 'Name', function() return 'lua' end)");
            Check("static.Name", HfTarget.Name(), "lua");
        });
        Step("struct", () =>
        {
            env.DoString("xlua.hotfix(CS.HfTarget, 'Scale', function(self, v, f) return CS.UnityEngine.Vector3(v.x * f + 1, v.y, v.z) end)");
            Check("struct.Scale.x", t.Scale(new Vector3(1, 1, 1), 2).x, 3f);
        });
        Step("property", () =>
        {
            env.DoString("xlua.hotfix(CS.HfTarget, 'get_Bias', function(self) return 99 end)");
            Check("property.Bias", t.Bias, 99);
        });
        Step("outref", () =>
        {
            env.DoString("xlua.hotfix(CS.HfTarget, 'Out', function(self, a, acc) return a * 3, acc + 100 end)");
            int tripled; int acc = 1;
            t.Out(5, out tripled, ref acc);
            Check("outref.out", tripled, 15);
            Check("outref.ref", acc, 101);
        });
        Step("generic", () =>
        {
            env.DoString("xlua.hotfix(CS.HfGeneric(CS.System.Int32), 'Echo', function(self, v) return v + 1 end)");
            Check("generic.Echo", new HfGeneric<int>().Echo(1), 2);
        });
        Step("unpatch", () =>
        {
            env.DoString("xlua.hotfix(CS.HfTarget, 'Add', nil)");
            Check("unpatch.Add", t.Add(1, 2), 3);
        });
        Step("delegates", () =>
        {
            env.DoString("function hf_mul(a, b) return a * b end");
            Check("delegates.listed", env.Global.Get<HfListed>("hf_mul")(6, 7), 42);
            Check("delegates.unlisted (matched by signature)", env.Global.Get<HfUnlisted>("hf_mul")(6, 7), 42);
        });
        Step("intkey", () =>
        {
            var k = new HfIntKey();
            Check("intkey.before.Twice", k.Twice(5), 10);
            env.DoString(@"
                (require 'xlua.util').auto_id_map()
                xlua.hotfix(CS.HfIntKey, 'Twice', function(self, a) return a * 2 + self.k end)
                xlua.hotfix(CS.HfIntKey, 'Zero', function() return 7 end)
            ");
            Check("intkey.Twice (reads private field)", k.Twice(5), 13);
            Check("intkey.Zero", HfIntKey.Zero(), 7);
        });
        sb.AppendLine("HOTFIXTEST SUMMARY pass=" + pass + " fail=" + fail);
        Debug.Log(sb.ToString());
    }
}
