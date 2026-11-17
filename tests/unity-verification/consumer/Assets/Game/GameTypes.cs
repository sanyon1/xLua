using XLua;

namespace Game
{
    [LuaCallCSharp]
    public class Calc
    {
        public int Bias = 1;
        public int Add(int a, int b) { return a + b + Bias; }
        public static string Name { get { return "calc"; } }
    }

    [LuaCallCSharp]
    [GCOptimize]
    public struct Vec2i
    {
        public int X;
        public int Y;
        public Vec2i(int x, int y) { X = x; Y = y; }
    }

    [CSharpCallLua]
    public delegate int IntBinOp(int a, int b);

    [CSharpCallLua]
    public delegate Vec2i VecOp(Vec2i v);

    [CSharpCallLua]
    public interface IGreeter
    {
        string Greet(string name);
    }
}
