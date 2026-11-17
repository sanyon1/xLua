using XLua;

namespace HfAsm
{
    [LuaCallCSharp]
    [GCOptimize]
    public struct Pt
    {
        public int X;
        public int Y;
        public Pt(int x, int y) { X = x; Y = y; }
    }

    [LuaCallCSharp]
    public enum Mode { Off = 0, On = 1 }

    [LuaCallCSharp]
    public class Box
    {
        public Pt Corner = new Pt(1, 2);
        public Mode Mode = Mode.On;
        public Pt Move(Pt d) { return new Pt(Corner.X + d.X, Corner.Y + d.Y); }
    }

    [CSharpCallLua]
    public delegate Pt PtOp(Pt p);

    [CSharpCallLua]
    public interface INamed { string Name(); }

    [Hotfix]
    public class HfAsmTarget
    {
        private int bias = 5;
        public int Add(int a, int b) { return a + b; }
        public Pt Shift(Pt p) { return p; }
    }

    [Hotfix(HotfixFlag.IntKey)]
    public class HfAsmIntKey
    {
        private int k = 4;
        public int Twice(int a) { return a * 2; }
    }
}
