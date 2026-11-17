using System;
using System.Text;
using UnityEngine;
using XLua;

// Lives in Assembly-CSharp and uses a value type of another generated assembly.
[LuaCallCSharp]
public class HfUser
{
    public HfAsm.Pt Echo(HfAsm.Pt p) { return new HfAsm.Pt(p.Y, p.X); }
    public HfAsm.Mode Flip(HfAsm.Mode m) { return m == HfAsm.Mode.On ? HfAsm.Mode.Off : HfAsm.Mode.On; }
}

public static class MultiAsmSmoke
{
    static int pass, fail;
    static StringBuilder sb = new StringBuilder();

    static void Check(string name, object actual, object expected)
    {
        bool ok = Equals(actual, expected);
        if (ok) pass++; else fail++;
        sb.AppendLine("MULTITEST " + (ok ? "ok  " : "FAIL") + " " + name + " = " + actual + (ok ? "" : " (expected " + expected + ")"));
    }

    static void Step(string name, Action action)
    {
        try { action(); }
        catch (Exception e) { fail++; sb.AppendLine("MULTITEST FAIL " + name + " threw " + e.GetType().Name + ": " + e.Message.Split('\n')[0]); }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Run()
    {
        var env = new LuaEnv();
        env.AddLoader((ref string file) =>
        {
            string map = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), file + ".lua.txt");
            return file.StartsWith("hotfix_id_map") && !Application.isEditor && System.IO.File.Exists(map) ? System.IO.File.ReadAllBytes(map) : null;
        });
        Step("units", () =>
        {
            var names = string.Join(",", Array.ConvertAll(GenRegistry.GenAssemblies, a => a.GetName().Name));
            sb.AppendLine("MULTITEST gen assemblies: " + names);
            Check("units.HfAsm registered", names.Contains("HfAsm"), true);
            Check("units.Assembly-CSharp registered", names.Contains("Assembly-CSharp"), true);
        });
        Step("wraps", () =>
        {
            env.DoString(@"
                local b = CS.HfAsm.Box()
                local p = b:Move(CS.HfAsm.Pt(10, 20))
                m_x, m_y = p.X, p.Y
                m_mode = b.Mode == CS.HfAsm.Mode.On
                local e = CS.HfUser():Echo(CS.HfAsm.Pt(3, 4))
                m_ex, m_ey = e.X, e.Y
                m_flip = CS.HfUser():Flip(CS.HfAsm.Mode.On) == CS.HfAsm.Mode.Off
                function m_op(p) return CS.HfAsm.Pt(p.X * 2, p.Y * 2) end
                m_named = { Name = function(self) return 'named' end }
            ");
            Check("wraps.struct in own assembly", env.Global.Get<int>("m_x") + "," + env.Global.Get<int>("m_y"), "11,22");
            Check("wraps.enum in own assembly", env.Global.Get<bool>("m_mode"), true);
            Check("wraps.struct used from Assembly-CSharp", env.Global.Get<int>("m_ex") + "," + env.Global.Get<int>("m_ey"), "4,3");
            Check("wraps.enum used from Assembly-CSharp", env.Global.Get<bool>("m_flip"), true);
            var r = env.Global.Get<HfAsm.PtOp>("m_op")(new HfAsm.Pt(2, 3));
            Check("wraps.delegate of other assembly", r.X + "," + r.Y, "4,6");
            Check("wraps.interface of other assembly", env.Global.Get<HfAsm.INamed>("m_named").Name(), "named");
        });
        Step("hotfix", () =>
        {
            var t = new HfAsm.HfAsmTarget();
            env.DoString(@"
                xlua.hotfix(CS.HfAsm.HfAsmTarget, 'Add', function(self, a, b) return a + b + self.bias end)
                xlua.hotfix(CS.HfAsm.HfAsmTarget, 'Shift', function(self, p) return CS.HfAsm.Pt(p.X + 1, p.Y + 1) end)
            ");
            Check("hotfix.other assembly instance", t.Add(1, 2), 8);
            Check("hotfix.other assembly struct", t.Shift(new HfAsm.Pt(1, 1)).X, 2);
        });
        Step("intkey", () =>
        {
            var a = new HfAsm.HfAsmIntKey();
            var b = new HfIntKey();
            env.DoString(@"
                (require 'xlua.util').auto_id_map()
                xlua.hotfix(CS.HfAsm.HfAsmIntKey, 'Twice', function(self, a) return a * 2 + self.k end)
            ");
            Check("intkey.other assembly", a.Twice(5), 14);
            env.DoString("xlua.hotfix(CS.HfIntKey, 'Twice', function(self, a) return a * 2 + 100 end)");
            Check("intkey.Assembly-CSharp after other assembly", b.Twice(5), 110);
            Check("intkey.other assembly unaffected", a.Twice(5), 14);
        });
        sb.AppendLine("MULTITEST SUMMARY pass=" + pass + " fail=" + fail);
        Debug.Log(sb.ToString());
    }
}
