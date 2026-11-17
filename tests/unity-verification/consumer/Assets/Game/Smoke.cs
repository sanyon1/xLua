using System;
using System.IO;
using System.Text;
using UnityEngine;
using XLua;

namespace Game
{
    // Runs once in a player build and writes what it observed next to the executable.
    public static class Smoke
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Run()
        {
            if (Application.isEditor) return;
            var sb = new StringBuilder();
            try
            {
                sb.AppendLine("registered_before_luaenv=" + GenRegistry.Registered);
                sb.AppendLine("gen_assembly=" + string.Join(",", System.Array.ConvertAll(GenRegistry.GenAssemblies, a => a.GetName().Name)));
                var env = new LuaEnv();
                sb.AppendLine("gen_flag=" + DelegateBridge.Gen_Flag);
                env.DoString(@"
                    local c = CS.Game.Calc()
                    c.Bias = 10
                    sum = c:Add(1, 2)
                    name = CS.Game.Calc.Name
                    function mul(a, b) return a * b end
                    function swap(v) return CS.Game.Vec2i(v.Y, v.X) end
                    greeter = { Greet = function(self, n) return 'hello ' .. n end }
                ");
                sb.AppendLine("lua_calls_cs=" + env.Global.Get<int>("sum") + "," + env.Global.Get<string>("name"));
                var mul = env.Global.Get<IntBinOp>("mul");
                sb.AppendLine("delegate=" + mul(6, 7) + " target=" + mul.Target.GetType().FullName);
                var swap = env.Global.Get<VecOp>("swap");
                var v = swap(new Vec2i(3, 4));
                sb.AppendLine("struct=" + v.X + "," + v.Y);
                var greeter = env.Global.Get<IGreeter>("greeter");
                sb.AppendLine("interface=" + greeter.Greet("xlua") + " type=" + greeter.GetType().FullName);
                env.DoString(@"
                    local m = CS.Game2.Mover():Move(CS.Game.Vec2i(1, 2))
                    other = m.X .. ',' .. m.Y
                    app = CS.AppThing.Twice(21)
                    local f = CS.AppThing.Flip(CS.Game.Vec2i(5, 6))
                    flip = f.X .. ',' .. f.Y
                ");
                sb.AppendLine("other_assembly=" + env.Global.Get<string>("other") + " app=" + env.Global.Get<int>("app") + " flip=" + env.Global.Get<string>("flip"));
                sb.AppendLine("result=OK");
            }
            catch (Exception e)
            {
                sb.AppendLine("result=FAIL");
                sb.AppendLine(e.ToString());
            }
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(Application.dataPath), "smoke.txt"), sb.ToString());
            Debug.Log(sb.ToString());
            Application.Quit();
        }
    }
}
